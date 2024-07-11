using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Os;
using LibHac.Sf;
using LibHac.Util;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IStorage = LibHac.Fs.IStorage;

namespace LibHac.FsSrv.Impl;

/// <summary>
/// Bulk moves save data from one save data space to another.
/// </summary>
/// <remarks><para>To use this class, call <see cref="Register"/> for each save data to be moved. After all save data
/// have been registered, repeatedly call <see cref="Process"/> until it returns 0 for the remaining size.</para>
/// <para>Based on nnSdk 18.3.0 (FS 18.0.0)</para></remarks>
public class SaveDataMover : ISaveDataMover
{
    private enum State
    {
        Initial,
        Registered,
        Copying,
        Finished,
        Fatal,
        Canceled
    }

    private SharedRef<ISaveDataTransferCoreInterface> _transferInterface;
    private Optional<StorageDuplicator> _duplicator;
    private SaveDataSpaceId _srcSpaceId;
    private Array128<ulong> _srcSaveIds;
    private SaveDataSpaceId _dstSpaceId;
    private Array128<ulong> _dstSaveIds;
    // private TransferMemory _transferMemory;
    private Memory<byte> _workBuffer;
    private ulong _transferMemorySize;
    private int _saveCount;
    private int _currentSaveIndex;
    private long _remainingSize;
    private State _state;
    private SdkMutex _mutex;

    public SaveDataMover(ref readonly SharedRef<ISaveDataTransferCoreInterface> transferInterface,
        SaveDataSpaceId sourceSpaceId, SaveDataSpaceId destinationSpaceId, NativeHandle transferMemoryHandle,
        ulong transferMemorySize)
    {
        _transferInterface = SharedRef<ISaveDataTransferCoreInterface>.CreateCopy(in transferInterface);
        _duplicator = new Optional<StorageDuplicator>();

        _srcSpaceId = sourceSpaceId;
        _srcSaveIds = default;
        _dstSpaceId = destinationSpaceId;
        _dstSaveIds = default;

        // Missing: Attach transfer memory

        _workBuffer = default;
        _transferMemorySize = transferMemorySize;
        _saveCount = 0;
        _currentSaveIndex = 0;
        _remainingSize = 0;
        _state = State.Initial;
        _mutex = new SdkMutex();
        transferMemoryHandle.Detach();
    }

    public void Dispose()
    {
        // Missing: Destroy transfer memory

        if (_duplicator.HasValue)
        {
            _duplicator.Value.Dispose();
            _duplicator.Clear();
        }

        _transferInterface.Destroy();
    }

    private Result OpenDuplicator()
    {
        Result res;

        using var srcFileStorage = new SharedRef<IStorage>();
        using (var srcInternalStorageAccessor = new SharedRef<SaveDataInternalStorageAccessor>())
        {
            res = SaveDataTransferUtilityGlobalMethods.OpenSaveDataInternalStorageAccessor(null,
                ref srcInternalStorageAccessor.Ref, _srcSpaceId, _srcSaveIds[_currentSaveIndex]);
            if (res.IsFailure()) return res.Miss();

            res = srcInternalStorageAccessor.Get.Initialize(coreInterface: _transferInterface.Get,
                isTemporaryTransferSave: false, hashSalt: default);
            if (res.IsFailure()) return res.Miss();

            res = srcInternalStorageAccessor.Get.OpenConcatenationStorage(ref srcFileStorage.Ref);
            if (res.IsFailure()) return res.Miss();
        }

        using var dstFileStorage = new SharedRef<IStorage>();
        using var dstFs = new SharedRef<IFileSystem>();
        using (var dstInternalStorageAccessor = new SharedRef<SaveDataInternalStorageAccessor>())
        {
            res = SaveDataTransferUtilityGlobalMethods.OpenSaveDataInternalStorageAccessor(null,
                ref dstInternalStorageAccessor.Ref, _dstSpaceId, _dstSaveIds[_currentSaveIndex]);
            if (res.IsFailure()) return res.Miss();

            res = dstInternalStorageAccessor.Get.Initialize(coreInterface: _transferInterface.Get,
                isTemporaryTransferSave: false, hashSalt: default);
            if (res.IsFailure()) return res.Miss();

            res = dstInternalStorageAccessor.Get.OpenConcatenationStorage(ref dstFileStorage.Ref);
            if (res.IsFailure()) return res.Miss();

            using SharedRef<IFileSystem> fs = dstInternalStorageAccessor.Get.GetSaveDataInternalFileSystem();
            dstFs.SetByMove(ref fs.Ref);
        }

        _duplicator.Set(new StorageDuplicator(in srcFileStorage, in dstFileStorage, in dstFs));
        res = _duplicator.Value.Initialize();
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    private Result Initialize()
    {
        // Missing: Map transfer memory

        _remainingSize = 0;
        for (int i = 0; i < _saveCount; i++)
        {
            Result res = _transferInterface.Get.ReadSaveDataFileSystemExtraDataCore(out SaveDataExtraData srcExtraData,
                _srcSpaceId, _srcSaveIds[i], isTemporarySaveData: false);
            if (res.IsFailure()) return res.Miss();

            if (!SaveDataProperties.IsValidSpaceIdForSaveDataMover(srcExtraData.Attribute.Type, _dstSpaceId))
                return ResultFs.InvalidArgument.Log();

            if (_srcSpaceId == _dstSpaceId)
                return ResultFs.InvalidArgument.Log();

            Unsafe.SkipInit(out IntegrityParam integrityParam);

            using (var srcInternalStorageAccessor = new SharedRef<SaveDataInternalStorageAccessor>())
            {
                res = SaveDataTransferUtilityGlobalMethods.OpenSaveDataInternalStorageAccessor(null,
                    ref srcInternalStorageAccessor.Ref, _srcSpaceId, _srcSaveIds[_currentSaveIndex]);
                if (res.IsFailure()) return res.Miss();

                res = srcInternalStorageAccessor.Get.Initialize(coreInterface: _transferInterface.Get,
                    isTemporaryTransferSave: false, hashSalt: default);
                if (res.IsFailure()) return res.Miss();

                res = srcInternalStorageAccessor.Get.GetIntegrityParam(ref integrityParam);
                if (res.IsFailure()) return res.Miss();

                using var srcFileStorage = new SharedRef<IStorage>();
                res = srcInternalStorageAccessor.Get.OpenConcatenationStorage(ref srcFileStorage.Ref);
                if (res.IsFailure()) return res.Miss();

                res = srcFileStorage.Get.GetSize(out long size);
                if (res.IsFailure()) return res.Miss();

                _remainingSize += size;
            }

            SaveDataAttribute attribute = srcExtraData.Attribute;
            res = SaveDataCreationInfo.Make(out SaveDataCreationInfo creationInfo, srcExtraData.DataSize,
                srcExtraData.JournalSize, srcExtraData.OwnerId, srcExtraData.Flags, _dstSpaceId);
            if (res.IsFailure()) return res.Miss();

            new SaveDataMetaPolicyForSaveDataTransferVersion2(srcExtraData.Attribute.Type).GenerateMetaInfo(out SaveDataMetaInfo metaInfo);

            res = _transferInterface.Get.CreateSaveDataFileSystemCore(in attribute, in creationInfo, in metaInfo,
                new Optional<HashSalt>(in integrityParam.IntegritySeed), leaveUnfinalized: true);
            if (res.IsFailure()) return res.Miss();

            res = _transferInterface.Get.GetSaveDataInfo(out SaveDataInfo info, _dstSpaceId, attribute);
            if (res.IsFailure()) return res.Miss();

            _dstSaveIds[i] = info.SaveDataId;
        }

        return OpenDuplicator().Ret();
    }

    public Result Register(ulong saveDataId)
    {
        bool isSuccess = false;

        try
        {
            using var scopedLock = new ScopedLock<SdkMutex>(ref _mutex);

            if (_saveCount >= _srcSaveIds.Length)
                return ResultFs.PreconditionViolation.Log();

            switch (_state)
            {
                case State.Initial:
                case State.Registered:
                    _srcSaveIds[_saveCount] = saveDataId;
                    _saveCount++;
                    _state = State.Registered;

                    isSuccess = true;
                    return Result.Success;
                case State.Copying:
                case State.Finished:
                case State.Fatal:
                case State.Canceled:
                    return ResultFs.PreconditionViolation.Log();
                default:
                    Abort.UnexpectedDefault();
                    return Result.Success;
            }
        }
        finally
        {
            if (!isSuccess)
                ChangeStateToFatal();
        }
    }

    public Result Process(out long outRemainingSize, long sizeToProcess)
    {
        UnsafeHelpers.SkipParamInit(out outRemainingSize);
        bool isSuccess = false;

        try
        {
            using var scopedLock = new ScopedLock<SdkMutex>(ref _mutex);

            if ((long)_transferMemorySize < sizeToProcess)
                return ResultFs.InvalidSize.Log();

            Result res;
            switch (_state)
            {
                case State.Initial:
                    return ResultFs.PreconditionViolation.Log();
                case State.Registered:
                    res = Initialize();
                    if (res.IsFailure()) return res.Miss();

                    _state = State.Copying;
                    goto case State.Copying;
                case State.Copying:
                    Assert.SdkAssert(!_workBuffer.IsEmpty);
                    Span<byte> workBuffer = _workBuffer.Span.Slice(0, (int)_transferMemorySize);

                    // Call the current duplicator with a size of zero to get the remaining size for this save before processing
                    res = _duplicator.Value.ProcessDuplication(out long remainingSizeBefore, workBuffer, 0);
                    if (res.IsFailure()) return res.Miss();

                    res = _duplicator.Value.ProcessDuplication(out long remainingSize, workBuffer, sizeToProcess);
                    if (res.IsFailure()) return res.Miss();

                    // Update the total remaining size with how much data was processed in this iteration
                    _remainingSize -= remainingSizeBefore - remainingSize;

                    if (remainingSize == 0)
                    {
                        // When finished copying this save, finalize and close the duplicator
                        res = _duplicator.Value.FinalizeObject();
                        if (res.IsFailure()) return res.Miss();

                        _duplicator.Value.Dispose();
                        _duplicator.Clear();

                        // Copy the extra data from the old save to the new save
                        res = _transferInterface.Get.ReadSaveDataFileSystemExtraDataCore(out SaveDataExtraData extraData,
                            _srcSpaceId, _srcSaveIds[_currentSaveIndex], isTemporarySaveData: false);
                        if (res.IsFailure()) return res.Miss();

                        res = _transferInterface.Get.WriteSaveDataFileSystemExtraDataCore(_dstSpaceId,
                            _dstSaveIds[_currentSaveIndex], in extraData, extraData.Attribute.Type,
                            updateTimeStamp: false);
                        if (res.IsFailure()) return res.Miss();

                        if (_currentSaveIndex == _saveCount - 1)
                        {
                            // If this was the last save to copy, finalize the entire move operation
                            FinalizeObject().IgnoreResult();
                            _state = State.Finished;
                        }
                        else
                        {
                            // If there are still saves to copy, open the duplicator for the next one
                            _currentSaveIndex++;

                            res = OpenDuplicator();
                            if (res.IsFailure()) return res.Miss();
                        }
                    }

                    outRemainingSize = _remainingSize;
                    isSuccess = true;

                    return Result.Success;
                case State.Finished:
                    outRemainingSize = 0;
                    isSuccess = true;

                    return Result.Success;
                case State.Fatal:
                    return ResultFs.PreconditionViolation.Log();
                case State.Canceled:
                    return ResultFs.PreconditionViolation.Log();
                default:
                    Abort.UnexpectedDefault();
                    return Result.Success;
            }
        }
        finally
        {
            if (!isSuccess)
                ChangeStateToFatal();
        }
    }

    private Result FinalizeObject()
    {
        Assert.SdkAssert(_mutex.IsLockedByCurrentThread());

        Result res = SetStates(_dstSpaceId, _dstSaveIds, SaveDataState.Normal);
        if (res.IsFailure()) return res.Miss();

        res = SetStates(_srcSpaceId, _srcSaveIds, SaveDataState.Processing);
        if (res.IsFailure()) return res.Miss();

        for (int i = 0; i < _saveCount; i++)
        {
            res = _transferInterface.Get.DeleteSaveDataFileSystemBySaveDataSpaceId(_srcSpaceId, _srcSaveIds[i]);
            if (res.IsFailure()) return res.Miss();
        }

        _state = State.Finished;
        return Result.Success;

        Result SetStates(SaveDataSpaceId spaceId, ReadOnlySpan<ulong> saveDataIds, SaveDataState state)
        {
            using var accessor = new UniqueRef<SaveDataIndexerAccessor>();
            Result r = _transferInterface.Get.OpenSaveDataIndexerAccessor(ref accessor.Ref, spaceId);
            if (r.IsFailure()) return r.Miss();

            ReadOnlySpan<ulong> ids = saveDataIds.Slice(0, _saveCount);
            for (int i = 0; i < ids.Length; i++)
            {
                r = accessor.Get.GetInterface().SetState(ids[i], state);
                if (r.IsFailure()) return r.Miss();
            }

            r = accessor.Get.GetInterface().Commit();
            if (r.IsFailure()) return r.Miss();

            return Result.Success;
        }
    }

    public Result Cancel()
    {
        Result DoCancel()
        {
            _duplicator.Value.Dispose();
            _duplicator.Clear();

            Result result = Result.Success;

            for (int i = 0; i < _saveCount; i++)
            {
                Result res = _transferInterface.Get.DeleteSaveDataFileSystemBySaveDataSpaceId(_dstSpaceId, _dstSaveIds[_saveCount]);
                if (!res.IsSuccess() && !ResultFs.TargetNotFound.Includes(res))
                {
                    if (result.IsSuccess())
                        result = res;
                }
            }

            _state = State.Canceled;
            return result.Ret();
        }

        using var scopedLock = new ScopedLock<SdkMutex>(ref _mutex);

        switch (_state)
        {
            case State.Initial:
            case State.Registered:
            case State.Copying:
            case State.Fatal:
                return DoCancel().Ret();
            case State.Finished:
            case State.Canceled:
                return ResultFs.PreconditionViolation.Log();
            default:
                Abort.UnexpectedDefault();
                return Result.Success;
        }
    }

    private void ChangeStateToFatal()
    {
        switch (_state)
        {
            case State.Initial:
            case State.Registered:
            case State.Copying:
                _state = State.Fatal;
                break;
            case State.Finished:
            case State.Fatal:
            case State.Canceled:
                break;
            default:
                Abort.UnexpectedDefault();
                break;
        }
    }
}