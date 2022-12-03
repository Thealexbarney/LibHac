using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Impl;
using LibHac.Fs.Shim;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.FsSystem;
using LibHac.Kvdb;
using LibHac.Ncm;
using LibHac.Os;
using LibHac.Sf;
using LibHac.Util;
using static LibHac.Fs.SaveData;
using static LibHac.Fs.StringTraits;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;
using IFile = LibHac.Fs.Fsa.IFile;
using IFileSf = LibHac.FsSrv.Sf.IFile;
using Path = LibHac.Fs.Path;
using Utility = LibHac.FsSystem.Utility;

namespace LibHac.FsSrv;

/// <summary>
/// Handles save-data-related calls for <see cref="FileSystemProxyImpl"/>.
/// </summary>
/// <remarks>FS will have one instance of this class for every connected process.
/// The FS permissions of the calling process are checked on every function call.
/// <br/>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
internal class SaveDataFileSystemService : ISaveDataTransferCoreInterface, ISaveDataMultiCommitCoreInterface
{
    private const int OpenEntrySemaphoreCount = 256;
    private const int SaveMountSemaphoreCount = 10;

    private const int SaveDataBlockSize = 0x4000;

    private WeakRef<SaveDataFileSystemService> _selfReference;
    private SaveDataFileSystemServiceImpl _serviceImpl;
    private ulong _processId;
    private Path.Stored _saveDataRootPath;
    private SemaphoreAdapter _openEntryCountSemaphore;
    private SemaphoreAdapter _saveDataMountCountSemaphore;

    private HorizonClient Hos => _serviceImpl.Hos;

    private SharedRef<SaveDataFileSystemService> GetSharedFromThis() =>
        SharedRef<SaveDataFileSystemService>.Create(in _selfReference);

    private SharedRef<ISaveDataMultiCommitCoreInterface> GetSharedMultiCommitInterfaceFromThis() =>
        SharedRef<ISaveDataMultiCommitCoreInterface>.Create(in _selfReference);

    public static SharedRef<SaveDataFileSystemService> CreateShared(SaveDataFileSystemServiceImpl serviceImpl, ulong processId)
    {
        // Create the service
        var saveService = new SaveDataFileSystemService(serviceImpl, processId);

        // Wrap the service in a ref-counter and give the service a weak self-reference
        using var sharedService = new SharedRef<SaveDataFileSystemService>(saveService);
        saveService._selfReference.Set(in sharedService);

        return SharedRef<SaveDataFileSystemService>.CreateMove(ref sharedService.Ref);
    }

    private class SaveDataOpenCountAdapter : IEntryOpenCountSemaphoreManager
    {
        private SharedRef<SaveDataFileSystemService> _saveService;

        public SaveDataOpenCountAdapter(ref SharedRef<SaveDataFileSystemService> saveService)
        {
            _saveService = SharedRef<SaveDataFileSystemService>.CreateMove(ref saveService);
        }

        public void Dispose()
        {
            _saveService.Destroy();
        }

        public Result TryAcquireEntryOpenCountSemaphore(ref UniqueRef<IUniqueLock> outSemaphore)
        {
            return _saveService.Get.TryAcquireSaveDataEntryOpenCountSemaphore(ref outSemaphore).Ret();
        }
    }

    private static Result CheckOpenSaveDataInfoReaderAccessControl(ProgramInfo programInfo, SaveDataSpaceId spaceId)
    {
        Assert.SdkNotNull(programInfo);

        switch (spaceId)
        {
            case SaveDataSpaceId.System:
            case SaveDataSpaceId.SdSystem:
            case SaveDataSpaceId.ProperSystem:
            case SaveDataSpaceId.SafeMode:
                if (!programInfo.AccessControl.CanCall(OperationType.OpenSaveDataInfoReaderForSystem))
                    return ResultFs.PermissionDenied.Log();
                break;
            case SaveDataSpaceId.User:
            case SaveDataSpaceId.Temporary:
            case SaveDataSpaceId.SdUser:
                if (!programInfo.AccessControl.CanCall(OperationType.OpenSaveDataInfoReader))
                    return ResultFs.PermissionDenied.Log();
                break;
            default:
                return ResultFs.InvalidSaveDataSpaceId.Log();
        }

        return Result.Success;
    }

    private static class SaveDataAccessibilityChecker
    {
        public delegate Result ExtraDataGetter(out SaveDataExtraData extraData);

        public static Result CheckCreate(in SaveDataAttribute attribute, ulong ownerId, ProgramInfo programInfo,
            ProgramId programId)
        {
            AccessControl accessControl = programInfo.AccessControl;

            if (SaveDataProperties.IsSystemSaveData(attribute.Type))
            {
                if (ownerId != programInfo.ProgramIdValue)
                {
                    // If the program doesn't own the created save data it needs either the permission to create
                    // any system save data or it needs explicit access to the owner's save data.
                    Accessibility accessibility = accessControl.GetAccessibilitySaveDataOwnedBy(ownerId);

                    bool canAccess =
                        accessControl.CanCall(OperationType.CreateSystemSaveData) &&
                        accessControl.CanCall(OperationType.CreateOthersSystemSaveData) || accessibility.CanWrite;

                    if (!canAccess)
                        return ResultFs.PermissionDenied.Log();
                }
                else
                {
                    bool canAccess = accessControl.CanCall(OperationType.CreateSystemSaveData);

                    if (!canAccess)
                        return ResultFs.PermissionDenied.Log();
                }
            }
            else if (attribute.Type == SaveDataType.Account && attribute.UserId == InvalidUserId)
            {
                // Trying to create a program's debug save.
                bool canAccess =
                    accessControl.CanCall(OperationType.CreateSaveData) ||
                    accessControl.CanCall(OperationType.DebugSaveData);

                if (!canAccess)
                    return ResultFs.PermissionDenied.Log();
            }
            else
            {
                Result res = GetAccessibilityForSaveData(out Accessibility accessibility, programInfo, ownerId);
                if (res.IsFailure()) return res.Miss();

                // If none of the above conditions apply, the program needs write access to the owner's save data.
                // The program also needs either permission to create any save data, or it must be creating its own
                // save data and have the permission to do so.
                bool canAccess = accessControl.CanCall(OperationType.CreateSaveData);

                if (accessibility.CanWrite &&
                    attribute.ProgramId == programId || attribute.ProgramId.Value == ownerId)
                {
                    canAccess |= accessControl.CanCall(OperationType.CreateOwnSaveData);
                }

                if (!canAccess)
                    return ResultFs.PermissionDenied.Log();
            }

            return Result.Success;
        }

        public static Result CheckOpenPre(in SaveDataAttribute attribute, ProgramInfo programInfo)
        {
            AccessControl accessControl = programInfo.AccessControl;

            if (attribute.Type == SaveDataType.Device)
            {
                Accessibility accessibility =
                    accessControl.GetAccessibilityFor(AccessibilityType.MountDeviceSaveData);

                bool canAccess = accessibility.CanRead && accessibility.CanWrite;

                if (!canAccess)
                    return ResultFs.PermissionDenied.Log();
            }
            else if (attribute.Type == SaveDataType.Account)
            {
                // We need debug save data permissions to open a debug save.
                bool canAccess = attribute.UserId != InvalidUserId ||
                                 accessControl.CanCall(OperationType.DebugSaveData);

                if (!canAccess)
                    return ResultFs.PermissionDenied.Log();
            }

            return Result.Success;
        }

        public static Result CheckOpen(in SaveDataAttribute attribute, ProgramInfo programInfo,
            ExtraDataGetter extraDataGetter)
        {
            AccessControl accessControl = programInfo.AccessControl;

            Result res = GetAccessibilityForSaveData(out Accessibility accessibility, programInfo, extraDataGetter);
            if (res.IsFailure()) return res.Miss();

            // Note: This is correct. Even if a program only has read accessibility to a save data,
            // Nintendo allows opening it with read/write accessibility as of FS 14.0.0
            if (accessibility.CanRead || accessibility.CanWrite)
                return Result.Success;

            // The program doesn't have permissions for this specific save data. Check if it has overall
            // permissions for other programs' save data.
            if (SaveDataProperties.IsSystemSaveData(attribute.Type))
            {
                accessibility = accessControl.GetAccessibilityFor(AccessibilityType.MountOthersSystemSaveData);
            }
            else
            {
                accessibility = accessControl.GetAccessibilityFor(AccessibilityType.MountOthersSaveData);
            }

            if (accessibility.CanRead && accessibility.CanWrite)
                return Result.Success;

            return ResultFs.PermissionDenied.Log();
        }

        public static Result CheckDelete(in SaveDataAttribute attribute, ProgramInfo programInfo,
            ExtraDataGetter extraDataGetter)
        {
            AccessControl accessControl = programInfo.AccessControl;

            // DeleteSystemSaveData permission is needed to delete system save data
            if (SaveDataProperties.IsSystemSaveData(attribute.Type) &&
                !accessControl.CanCall(OperationType.DeleteSystemSaveData))
            {
                return ResultFs.PermissionDenied.Log();
            }

            // The DeleteSaveData permission allows deleting any non-system save data
            if (accessControl.CanCall(OperationType.DeleteSaveData))
            {
                return Result.Success;
            }

            // Otherwise the program needs the DeleteOwnSaveData permission and write access to the save
            Result res = GetAccessibilityForSaveData(out Accessibility accessibility, programInfo, extraDataGetter);
            if (res.IsFailure()) return res.Miss();

            if (accessControl.CanCall(OperationType.DeleteOwnSaveData) && accessibility.CanWrite)
            {
                return Result.Success;
            }

            return ResultFs.PermissionDenied.Log();
        }

        public static Result CheckExtend(SaveDataSpaceId spaceId, in SaveDataAttribute attribute,
            ProgramInfo programInfo, ExtraDataGetter extraDataGetter)
        {
            AccessControl accessControl = programInfo.AccessControl;

            switch (spaceId)
            {
                case SaveDataSpaceId.System:
                case SaveDataSpaceId.SdSystem:
                case SaveDataSpaceId.ProperSystem:
                case SaveDataSpaceId.SafeMode:
                {
                    Result res = GetAccessibilityForSaveData(out Accessibility accessibility, programInfo,
                        extraDataGetter);
                    if (res.IsFailure()) return res.Miss();

                    // The program needs the ExtendSystemSaveData permission and either one of
                    // read/write access to the save or the ExtendOthersSystemSaveData permission
                    bool canAccess = accessControl.CanCall(OperationType.ExtendSystemSaveData) &&
                                     (accessibility.CanRead && accessibility.CanWrite ||
                                      accessControl.CanCall(OperationType.ExtendOthersSystemSaveData));

                    if (!canAccess)
                        return ResultFs.PermissionDenied.Log();

                    break;
                }
                case SaveDataSpaceId.User:
                case SaveDataSpaceId.SdUser:
                {
                    bool canAccess = accessControl.CanCall(OperationType.ExtendSaveData);

                    Result res = GetAccessibilityForSaveData(out Accessibility accessibility, programInfo,
                        extraDataGetter);
                    if (res.IsFailure()) return res.Miss();

                    if (attribute.ProgramId == programInfo.ProgramId || accessibility.CanRead)
                    {
                        canAccess |= accessControl.CanCall(OperationType.ExtendOwnSaveData);

                        bool canAccessDebugSave = accessControl.CanCall(OperationType.DebugSaveData)
                                                  && attribute.Type == SaveDataType.Account
                                                  && attribute.UserId == UserId.InvalidId;

                        canAccess |= canAccessDebugSave;

                        if (!canAccess)
                            return ResultFs.PermissionDenied.Log();
                    }

                    break;
                }
                default:
                    return ResultFs.InvalidSaveDataSpaceId.Log();
            }

            return Result.Success;
        }

        public static Result CheckReadExtraData(in SaveDataAttribute attribute, in SaveDataExtraData mask,
            ProgramInfo programInfo, ExtraDataGetter extraDataGetter)
        {
            AccessControl accessControl = programInfo.AccessControl;

            bool canAccess = accessControl.CanCall(OperationType.ReadSaveDataFileSystemExtraData);

            Result res = GetAccessibilityForSaveData(out Accessibility accessibility, programInfo, extraDataGetter);
            if (res.IsFailure()) return res.Miss();

            SaveDataExtraData emptyMask = default;
            SaveDataExtraData maskWithoutRestoreFlag = mask;
            maskWithoutRestoreFlag.Flags &= ~SaveDataFlags.Restore;

            // Only read access to the save is needed to read the restore flag
            if (SpanHelpers.AsReadOnlyByteSpan(in emptyMask)
                .SequenceEqual(SpanHelpers.AsReadOnlyByteSpan(in maskWithoutRestoreFlag)))
            {
                canAccess |= accessibility.CanRead;
            }

            if (SaveDataProperties.IsSystemSaveData(attribute.Type))
            {
                canAccess |= accessibility.CanRead;
            }
            else if (attribute.ProgramId == programInfo.ProgramId || accessibility.CanRead)
            {
                canAccess |= accessControl.CanCall(OperationType.ReadOwnSaveDataFileSystemExtraData);

                bool canAccessDebugSave = accessControl.CanCall(OperationType.DebugSaveData)
                                          && attribute.Type == SaveDataType.Account
                                          && attribute.UserId == UserId.InvalidId;

                canAccess |= canAccessDebugSave;
            }

            if (!canAccess)
                return ResultFs.PermissionDenied.Log();

            return Result.Success;
        }

        public static Result CheckWriteExtraData(in SaveDataAttribute attribute, in SaveDataExtraData mask,
            ProgramInfo programInfo, ExtraDataGetter extraDataGetter)
        {
            AccessControl accessControl = programInfo.AccessControl;

            if (mask.Flags != SaveDataFlags.None)
            {
                bool canAccess = accessControl.CanCall(OperationType.WriteSaveDataFileSystemExtraDataAll) ||
                                 accessControl.CanCall(OperationType.WriteSaveDataFileSystemExtraDataFlags);

                if (SaveDataProperties.IsSystemSaveData(attribute.Type))
                {
                    Result res = GetAccessibilityForSaveData(out Accessibility accessibility, programInfo,
                        extraDataGetter);
                    if (res.IsFailure()) return res.Miss();

                    canAccess |= accessibility.CanWrite;
                }

                if ((mask.Flags & ~SaveDataFlags.Restore) == 0)
                {
                    Result res = GetAccessibilityForSaveData(out Accessibility accessibility, programInfo,
                        extraDataGetter);
                    if (res.IsFailure()) return res.Miss();

                    canAccess |= accessibility.CanWrite;
                }

                if (!canAccess)
                    return ResultFs.PermissionDenied.Log();
            }

            if (mask.TimeStamp != 0)
            {
                bool canAccess = accessControl.CanCall(OperationType.WriteSaveDataFileSystemExtraDataAll) ||
                                 accessControl.CanCall(OperationType.WriteSaveDataFileSystemExtraDataTimeStamp);

                if (!canAccess)
                    return ResultFs.PermissionDenied.Log();
            }

            if (mask.CommitId != 0)
            {
                bool canAccess = accessControl.CanCall(OperationType.WriteSaveDataFileSystemExtraDataAll) ||
                                 accessControl.CanCall(OperationType.WriteSaveDataFileSystemExtraDataCommitId);

                if (!canAccess)
                    return ResultFs.PermissionDenied.Log();
            }

            SaveDataExtraData emptyMask = default;
            SaveDataExtraData maskWithoutFlags = mask;
            maskWithoutFlags.Flags = SaveDataFlags.None;
            maskWithoutFlags.TimeStamp = 0;
            maskWithoutFlags.CommitId = 0;

            // Full write access is needed for writing anything other than flags, timestamp or commit ID
            if (SpanHelpers.AsReadOnlyByteSpan(in emptyMask)
                .SequenceEqual(SpanHelpers.AsReadOnlyByteSpan(in maskWithoutFlags)))
            {
                bool canAccess = accessControl.CanCall(OperationType.WriteSaveDataFileSystemExtraDataAll);

                if (!canAccess)
                    return ResultFs.PermissionDenied.Log();
            }

            return Result.Success;
        }

        public static Result CheckFind(in SaveDataFilter filter, ProgramInfo programInfo)
        {
            bool canAccess;

            if (programInfo.ProgramId == filter.Attribute.ProgramId)
            {
                AccessControl accessControl = programInfo.AccessControl;
                canAccess = accessControl.CanCall(OperationType.FindOwnSaveDataWithFilter);

                bool canAccessDebugSave = accessControl.CanCall(OperationType.DebugSaveData)
                                          && filter.Attribute.Type == SaveDataType.Account
                                          && filter.Attribute.UserId == UserId.InvalidId;

                canAccess |= canAccessDebugSave;
            }
            else
            {
                canAccess = true;
            }

            if (!canAccess)
                return ResultFs.PermissionDenied.Log();

            return Result.Success;
        }

        public static Result CheckOpenProhibiter(ProgramId programId, ProgramInfo programInfo)
        {
            Result res = GetAccessibilityForSaveData(out Accessibility accessibility, programInfo, programId.Value);
            if (res.IsFailure()) return res.Miss();

            bool canAccess = programInfo.AccessControl.CanCall(OperationType.OpenSaveDataTransferProhibiter);

            if (programInfo.ProgramId == programId || accessibility.CanRead)
            {
                canAccess |= programInfo.AccessControl.CanCall(OperationType.OpenOwnSaveDataTransferProhibiter);
            }

            if (!canAccess)
                return ResultFs.PermissionDenied.Log();

            return Result.Success;
        }

        private static Result GetAccessibilityForSaveData(out Accessibility accessibility, ProgramInfo programInfo,
            ulong ownerId)
        {
            if (ownerId == programInfo.ProgramIdValue)
            {
                // A program always has full access to its own save data
                accessibility = new Accessibility(true, true);
            }
            else
            {
                accessibility = programInfo.AccessControl.GetAccessibilitySaveDataOwnedBy(ownerId);
            }

            return Result.Success;
        }

        private static Result GetAccessibilityForSaveData(out Accessibility accessibility, ProgramInfo programInfo,
            ExtraDataGetter extraDataGetter)
        {
            UnsafeHelpers.SkipParamInit(out accessibility);

            Result res = extraDataGetter(out SaveDataExtraData extraData);
            if (res.IsFailure())
            {
                if (ResultFs.TargetNotFound.Includes(res))
                {
                    accessibility = new Accessibility(false, false);
                    return Result.Success;
                }

                return res;
            }

            // Allow access when opening a directory save FS on a dev console
            if (IsDirectorySaveDataExtraData(in extraData) &&
                programInfo.AccessControl.CanCall(OperationType.DebugSaveData))
            {
                accessibility = new Accessibility(true, true);
                return Result.Success;
            }

            return GetAccessibilityForSaveData(out accessibility, programInfo, extraData.OwnerId).Ret();
        }
    }

    public SaveDataFileSystemService(SaveDataFileSystemServiceImpl serviceImpl, ulong processId)
    {
        _serviceImpl = serviceImpl;
        _processId = processId;
        using var path = new Path();
        _openEntryCountSemaphore = new SemaphoreAdapter(OpenEntrySemaphoreCount, OpenEntrySemaphoreCount);
        _saveDataMountCountSemaphore = new SemaphoreAdapter(SaveMountSemaphoreCount, SaveMountSemaphoreCount);
    }

    public void Dispose()
    {
        _openEntryCountSemaphore.Dispose();
        _saveDataMountCountSemaphore.Dispose();
        _selfReference.Destroy();
    }

    private Result GetProgramInfo(out ProgramInfo programInfo)
    {
        var registry = new ProgramRegistryImpl(_serviceImpl.FsServer);
        return registry.GetProgramInfo(out programInfo, _processId);
    }

    private Result GetProgramInfoByProgramId(out ProgramInfo programInfo, ulong programId)
    {
        var registry = new ProgramRegistryImpl(_serviceImpl.FsServer);
        return registry.GetProgramInfoByProgramId(out programInfo, programId);
    }

    private static SaveDataSpaceId ConvertToRealSpaceId(SaveDataSpaceId spaceId)
    {
        return spaceId == SaveDataSpaceId.ProperSystem || spaceId == SaveDataSpaceId.SafeMode
            ? SaveDataSpaceId.System
            : spaceId;
    }

    private static bool IsStaticSaveDataIdValueRange(ulong id)
    {
        return unchecked((long)id) < 0;
    }

    private static bool IsDirectorySaveDataExtraData(in SaveDataExtraData extraData)
    {
        return extraData.OwnerId == 0 && extraData.DataSize == 0 && extraData.JournalSize == 0;
    }

    private static void ModifySaveDataExtraData(ref SaveDataExtraData currentExtraData, in SaveDataExtraData extraData,
        in SaveDataExtraData extraDataMask)
    {
        Span<byte> currentExtraDataBytes = SpanHelpers.AsByteSpan(ref currentExtraData);
        ReadOnlySpan<byte> extraDataBytes = SpanHelpers.AsReadOnlyByteSpan(in extraData);
        ReadOnlySpan<byte> extraDataMaskBytes = SpanHelpers.AsReadOnlyByteSpan(in extraDataMask);

        for (int i = 0; i < Unsafe.SizeOf<SaveDataExtraData>(); i++)
        {
            currentExtraDataBytes[i] = (byte)(extraDataBytes[i] & extraDataMaskBytes[i] |
                                              currentExtraDataBytes[i] & ~extraDataMaskBytes[i]);
        }
    }

    private static void MaskExtraData(ref SaveDataExtraData extraData, in SaveDataExtraData extraDataMask)
    {
        Span<byte> extraDataBytes = SpanHelpers.AsByteSpan(ref extraData);
        ReadOnlySpan<byte> extraDataMaskBytes = SpanHelpers.AsReadOnlyByteSpan(in extraDataMask);

        for (int i = 0; i < Unsafe.SizeOf<SaveDataExtraData>(); i++)
        {
            extraDataBytes[i] &= extraDataMaskBytes[i];
        }
    }

    private static StorageLayoutType DecidePossibleStorageFlag(SaveDataType type, SaveDataSpaceId spaceId)
    {
        if (type == SaveDataType.Cache || type == SaveDataType.Bcat)
            return StorageLayoutType.Bis | StorageLayoutType.SdCard | StorageLayoutType.Usb;

        if (type == SaveDataType.System && (spaceId == SaveDataSpaceId.SdSystem || spaceId == SaveDataSpaceId.SdUser))
            return StorageLayoutType.SdCard | StorageLayoutType.Usb;

        return StorageLayoutType.Bis;
    }

    private static SaveDataFormatType GetSaveDataFormatType(in SaveDataAttribute attribute)
    {
        return SaveDataProperties.IsJournalingSupported(attribute.Type)
            ? SaveDataFormatType.Normal
            : SaveDataFormatType.NoJournal;
    }

    public Result GetFreeSpaceSizeForSaveData(out long outFreeSpaceSize, SaveDataSpaceId spaceId)
    {
        UnsafeHelpers.SkipParamInit(out outFreeSpaceSize);

        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.NonGameCard);
        using var fileSystem = new SharedRef<IFileSystem>();

        Result res = _serviceImpl.OpenSaveDataDirectoryFileSystem(ref fileSystem.Ref, spaceId);
        if (res.IsFailure()) return res.Miss();

        using var pathRoot = new Path();
        res = PathFunctions.SetUpFixedPath(ref pathRoot.Ref(), "/"u8);
        if (res.IsFailure()) return res.Miss();

        fileSystem.Get.GetFreeSpaceSize(out long freeSpaceSize, in pathRoot);
        if (res.IsFailure()) return res.Miss();

        outFreeSpaceSize = freeSpaceSize;
        return Result.Success;
    }

    public Result RegisterSaveDataFileSystemAtomicDeletion(InBuffer saveDataIds)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.Bis);

        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        // This function only operates on the system save data space, so the caller
        // must have permissions to delete system save data
        AccessControl accessControl = programInfo.AccessControl;

        bool canAccess = accessControl.CanCall(OperationType.DeleteSaveData) &&
                         accessControl.CanCall(OperationType.DeleteSystemSaveData);

        if (!canAccess)
            return ResultFs.PermissionDenied.Log();

        bool success = false;

        using var accessor = new UniqueRef<SaveDataIndexerAccessor>();
        res = OpenSaveDataIndexerAccessor(ref accessor.Ref, SaveDataSpaceId.System);
        if (res.IsFailure()) return res.Miss();

        ReadOnlySpan<ulong> ids = MemoryMarshal.Cast<byte, ulong>(saveDataIds.Buffer);

        try
        {
            // Try to set the state of all the save IDs as being marked for deletion.
            for (int i = 0; i < ids.Length; i++)
            {
                res = accessor.Get.GetInterface().SetState(ids[i], SaveDataState.MarkedForDeletion);
                if (res.IsFailure()) return res.Miss();
            }

            res = accessor.Get.GetInterface().Commit();
            if (res.IsFailure()) return res.Miss();

            success = true;
            return Result.Success;
        }
        finally
        {
            // Rollback the operation if something goes wrong.
            if (!success)
            {
                res = accessor.Get.GetInterface().Rollback();
                if (res.IsFailure())
                {
                    Hos.Diag.Impl.LogImpl(Log.EmptyModuleName, LogSeverity.Info,
                        "[fs] Error: Failed to rollback save data indexer.\n"u8);
                }
            }
        }
    }

    public Result DeleteSaveDataFileSystem(ulong saveDataId)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.Bis);

        return DeleteSaveDataFileSystemCommon(SaveDataSpaceId.System, saveDataId).Ret();
    }

    private Result DeleteSaveDataFileSystemCore(SaveDataSpaceId spaceId, ulong saveDataId, bool wipeSaveFile)
    {
        // Delete the save data's meta files
        Result res = _serviceImpl.DeleteAllSaveDataMetas(saveDataId, spaceId);
        if (res.IsFailure() && !ResultFs.PathNotFound.Includes(res))
            return res.Miss();

        // Delete the actual save data.
        using Path saveDataRootPath = _saveDataRootPath.DangerousGetPath();
        res = _serviceImpl.DeleteSaveDataFileSystem(spaceId, saveDataId, wipeSaveFile, in saveDataRootPath);
        if (res.IsFailure() && !ResultFs.PathNotFound.Includes(res))
            return res.Miss();

        return Result.Success;
    }

    public Result DeleteSaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.NonGameCard);

        return DeleteSaveDataFileSystemBySaveDataSpaceIdCore(spaceId, saveDataId).Ret();
    }

    private Result DeleteSaveDataFileSystemBySaveDataSpaceIdCore(SaveDataSpaceId spaceId, ulong saveDataId)
    {
        if (saveDataId != SaveIndexerId)
        {
            using var accessor = new UniqueRef<SaveDataIndexerAccessor>();
            Result res = OpenSaveDataIndexerAccessor(ref accessor.Ref, spaceId);
            if (res.IsFailure()) return res.Miss();

            res = accessor.Get.GetInterface().GetValue(out SaveDataIndexerValue value, saveDataId);
            if (res.IsFailure()) return res.Miss();

            if (value.SpaceId != ConvertToRealSpaceId(spaceId))
                return ResultFs.TargetNotFound.Log();
        }

        return DeleteSaveDataFileSystemCommon(spaceId, saveDataId).Ret();
    }

    public Result DeleteSaveDataFileSystemBySaveDataAttribute(SaveDataSpaceId spaceId,
        in SaveDataAttribute attribute)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.NonGameCard);

        Result res = GetSaveDataInfo(out SaveDataInfo info, spaceId, in attribute);
        if (res.IsFailure()) return res;

        return DeleteSaveDataFileSystemBySaveDataSpaceIdCore(spaceId, info.SaveDataId).Ret();
    }

    private Result DeleteSaveDataFileSystemCommon(SaveDataSpaceId spaceId, ulong saveDataId)
    {
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        using var accessor = new UniqueRef<SaveDataIndexerAccessor>();

        SaveDataSpaceId targetSpaceId;

        if (saveDataId != SaveIndexerId)
        {
            res = OpenSaveDataIndexerAccessor(ref accessor.Ref, spaceId);
            if (res.IsFailure()) return res.Miss();

            // Get the actual space ID of this save.
            if (spaceId == SaveDataSpaceId.ProperSystem || spaceId == SaveDataSpaceId.SafeMode)
            {
                targetSpaceId = spaceId;
            }
            else
            {
                res = accessor.Get.GetInterface().GetValue(out SaveDataIndexerValue value, saveDataId);
                if (res.IsFailure()) return res.Miss();

                targetSpaceId = value.SpaceId;
            }

            // Check if the caller has permission to delete this save.
            res = accessor.Get.GetInterface().GetKey(out SaveDataAttribute key, saveDataId);
            if (res.IsFailure()) return res.Miss();

            Result GetExtraData(out SaveDataExtraData data) =>
                _serviceImpl.ReadSaveDataFileSystemExtraData(out data, targetSpaceId, saveDataId, key.Type,
                    _saveDataRootPath.DangerousGetPath());

            res = SaveDataAccessibilityChecker.CheckDelete(in key, programInfo, GetExtraData);
            if (res.IsFailure()) return res.Miss();

            // Pre-delete checks successful. Put the save in the Processing state until deletion is finished.
            res = accessor.Get.GetInterface().SetState(saveDataId, SaveDataState.Processing);
            if (res.IsFailure()) return res.Miss();

            res = accessor.Get.GetInterface().Commit();
            if (res.IsFailure()) return res.Miss();
        }
        else
        {
            // Only the FS process may delete the save indexer's save data.
            if (!_serviceImpl.FsServer.IsCurrentProcess(_processId))
                return ResultFs.PermissionDenied.Log();

            targetSpaceId = spaceId;
        }

        // Do the actual deletion.
        res = DeleteSaveDataFileSystemCore(targetSpaceId, saveDataId, wipeSaveFile: false);
        if (res.IsFailure()) return res.Miss();

        // Remove the save data from the indexer.
        // The indexer doesn't track itself, so skip if deleting its save data.
        if (saveDataId != SaveIndexerId)
        {
            res = accessor.Get.GetInterface().Delete(saveDataId);
            if (res.IsFailure()) return res.Miss();

            res = accessor.Get.GetInterface().Commit();
            if (res.IsFailure()) return res.Miss();
        }

        return Result.Success;
    }

    public Result SwapSaveDataKeyAndState(SaveDataSpaceId spaceId, ulong saveDataId1, ulong saveDataId2)
    {
        throw new NotImplementedException();
    }

    public Result SetSaveDataState(SaveDataSpaceId spaceId, ulong saveDataId, SaveDataState state)
    {
        throw new NotImplementedException();
    }

    public Result SetSaveDataRank(SaveDataSpaceId spaceId, ulong saveDataId, SaveDataRank rank)
    {
        throw new NotImplementedException();
    }

    public Result FinalizeSaveDataCreation(ulong saveDataId, SaveDataSpaceId spaceId)
    {
        throw new NotImplementedException();
    }

    public Result CancelSaveDataCreation(ulong saveDataId, SaveDataSpaceId spaceId)
    {
        throw new NotImplementedException();
    }

    public Result SetSaveDataRootPath(in FspPath path)
    {
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.DebugSaveData))
            return ResultFs.PermissionDenied.Log();

        using var saveDataRootPath = new Path();

        if (path.Str[0] == NullTerminator)
        {
            res = saveDataRootPath.Initialize("."u8);
            if (res.IsFailure()) return res.Miss();
        }
        else
        {
            res = saveDataRootPath.InitializeWithReplaceUnc(path.Str);
            if (res.IsFailure()) return res.Miss();
        }

        var pathFlags = new PathFlags();
        pathFlags.AllowWindowsPath();
        pathFlags.AllowRelativePath();
        pathFlags.AllowEmptyPath();

        res = saveDataRootPath.Normalize(pathFlags);
        if (res.IsFailure()) return res.Miss();

        _saveDataRootPath.Initialize(in saveDataRootPath);

        return Result.Success;
    }

    public Result UnsetSaveDataRootPath()
    {
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.DebugSaveData))
            return ResultFs.PermissionDenied.Log();

        using var saveDataRootPath = new Path();
        res = saveDataRootPath.InitializeAsEmpty();
        if (res.IsFailure()) return res.Miss();

        _saveDataRootPath.Initialize(in saveDataRootPath);

        return Result.Success;
    }

    // ReSharper disable once UnusedParameter.Global
    public Result UpdateSaveDataMacForDebug(SaveDataSpaceId spaceId, ulong saveDataId)
    {
        if (saveDataId == SaveIndexerId)
            return ResultFs.InvalidArgument.Log();

        return ResultFs.NotImplemented.Log();
    }

    public Result OpenSaveDataFile(ref SharedRef<IFile> outFile, SaveDataSpaceId spaceId, ulong saveDataId,
        OpenMode openMode)
    {
        var storageFlag = StorageLayoutType.NonGameCard;
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        using var file = new SharedRef<IFile>();
        Result res = _serviceImpl.OpenSaveDataFile(ref file.Ref, spaceId, saveDataId, openMode);
        if (res.IsFailure()) return res.Miss();

        using var typeSetFile = new SharedRef<IFile>(new StorageLayoutTypeSetFile(ref file.Ref, storageFlag));

        outFile.SetByMove(ref typeSetFile.Ref);
        return Result.Success;
    }

    public Result CheckSaveDataFile(long saveDataId, SaveDataSpaceId spaceId)
    {
        throw new NotImplementedException();
    }

    private Result CreateSaveDataFileSystemCore(in SaveDataCreationInfo2 creationInfo, bool leaveUnfinalized)
    {
        ulong saveDataId = 0;
        bool creating = false;
        bool accessorInitialized = false;
        Result res;

        StorageLayoutType storageFlag = DecidePossibleStorageFlag(creationInfo.Attribute.Type, creationInfo.SpaceId);
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        using var accessor = new UniqueRef<SaveDataIndexerAccessor>();

        try
        {
            // Add the new save data to the save indexer
            if (creationInfo.Attribute.StaticSaveDataId == SaveIndexerId)
            {
                // The save indexer doesn't index itself
                saveDataId = SaveIndexerId;
                res = _serviceImpl.DoesSaveDataEntityExist(out bool saveExists, creationInfo.SpaceId, saveDataId);

                if (res.IsSuccess() && saveExists)
                {
                    return ResultFs.PathAlreadyExists.Log();
                }

                creating = true;
            }
            else
            {
                res = OpenSaveDataIndexerAccessor(ref accessor.Ref, creationInfo.SpaceId);
                if (res.IsFailure()) return res.Miss();

                accessorInitialized = true;

                SaveDataAttribute indexerKey = creationInfo.Attribute;

                // Add the new value to the indexer
                if (creationInfo.Attribute.StaticSaveDataId != 0 && creationInfo.Attribute.UserId == InvalidUserId)
                {
                    // If a static save data ID is specified that ID is always used
                    saveDataId = creationInfo.Attribute.StaticSaveDataId;

                    res = accessor.Get.GetInterface().PutStaticSaveDataIdIndex(in indexerKey);
                }
                else
                {
                    // The save indexer has an upper limit on the number of entries it can hold.
                    // A few of those entries are reserved for system saves so the system doesn't
                    // end up in a situation where it can't create a required system save.
                    if (!SaveDataProperties.CanUseIndexerReservedArea(creationInfo.Attribute.Type))
                    {
                        if (accessor.Get.GetInterface().IsRemainedReservedOnly())
                        {
                            return ResultKvdb.OutOfKeyResource.Log();
                        }
                    }

                    // If a static save data ID is no specified we're assigned a new save ID
                    res = accessor.Get.GetInterface().Publish(out saveDataId, in indexerKey);
                }

                if (res.IsSuccess())
                {
                    creating = true;

                    // Set the state, space ID and size on the new save indexer entry.
                    res = accessor.Get.GetInterface().SetState(saveDataId, SaveDataState.Processing);
                    if (res.IsFailure()) return res.Miss();

                    res = accessor.Get.GetInterface().SetSpaceId(saveDataId, ConvertToRealSpaceId(creationInfo.SpaceId));
                    if (res.IsFailure()) return res.Miss();

                    res = QuerySaveDataTotalSize(out long saveDataSize, creationInfo.Size, creationInfo.JournalSize);
                    if (res.IsFailure()) return res.Miss();

                    res = accessor.Get.GetInterface().SetSize(saveDataId, saveDataSize);
                    if (res.IsFailure()) return res.Miss();

                    res = accessor.Get.GetInterface().Commit();
                    if (res.IsFailure()) return res.Miss();
                }
                else
                {
                    if (ResultFs.AlreadyExists.Includes(res))
                    {
                        // The save already exists. Ensure the thumbnail meta file exists if needed.
                        if (creationInfo.MetaType == SaveDataMetaType.Thumbnail)
                        {
                            Result resultMeta = accessor.Get.GetInterface().Get(out SaveDataIndexerValue value, in indexerKey);
                            if (resultMeta.IsFailure()) return resultMeta.Miss();

                            resultMeta = CreateEmptyThumbnailFile(creationInfo.SpaceId, value.SaveDataId);

                            // Allow the thumbnail file to already exist
                            if (resultMeta.IsFailure() && !ResultFs.PathAlreadyExists.Includes(resultMeta))
                                return resultMeta.Miss();
                        }

                        return ResultFs.PathAlreadyExists.LogConverted(res);
                    }

                    return res.Miss();
                }

                if (res.IsFailure())
                {
                    if (ResultFs.AlreadyExists.Includes(res))
                    {
                        return ResultFs.PathAlreadyExists.LogConverted(res);
                    }

                    return res;
                }
            }

            // After the new save is added to the save indexer, create the save data file or directory.
            using Path saveDataRootPath = _saveDataRootPath.DangerousGetPath();
            res = _serviceImpl.CreateSaveDataFileSystem(saveDataId, in creationInfo, in saveDataRootPath, skipFormat: false);

            if (res.IsFailure())
            {
                if (!ResultFs.PathAlreadyExists.Includes(res))
                    return res.Miss();

                // The save exists on the file system but not in the save indexer.
                // Delete the save data and try creating it again.
                res = DeleteSaveDataFileSystemCore(creationInfo.SpaceId, saveDataId, false);
                if (res.IsFailure()) return res.Miss();

                res = _serviceImpl.CreateSaveDataFileSystem(saveDataId, in creationInfo, in saveDataRootPath,
                    skipFormat: false);
                if (res.IsFailure()) return res.Miss();
            }

            if (creationInfo.MetaType != SaveDataMetaType.None)
            {
                // Create the requested save data meta file.
                res = _serviceImpl.CreateSaveDataMeta(saveDataId, creationInfo.SpaceId, creationInfo.MetaType,
                    creationInfo.MetaSize);
                if (res.IsFailure()) return res.Miss();

                if (creationInfo.MetaType == SaveDataMetaType.Thumbnail)
                {
                    using var metaFile = new UniqueRef<IFile>();
                    res = _serviceImpl.OpenSaveDataMeta(ref metaFile.Ref, saveDataId, creationInfo.SpaceId,
                        creationInfo.MetaType);
                    if (res.IsFailure()) return res.Miss();

                    // The first 0x20 bytes of thumbnail meta files is an SHA-256 hash.
                    // Zero the hash to indicate that it's currently unused.
                    ReadOnlySpan<byte> metaFileHash = stackalloc byte[0x20];

                    res = metaFile.Get.Write(0, metaFileHash, WriteOption.Flush);
                    if (res.IsFailure()) return res.Miss();
                }
            }

            if (leaveUnfinalized)
            {
                creating = false;
                return Result.Success;
            }

            // The indexer's save data isn't tracked, so we don't need to update its state.
            if (creationInfo.Attribute.StaticSaveDataId != SaveIndexerId)
            {
                // Mark the save data as being successfully created
                res = accessor.Get.GetInterface().SetState(saveDataId, SaveDataState.Normal);
                if (res.IsFailure()) return res.Miss();

                res = accessor.Get.GetInterface().Commit();
                if (res.IsFailure()) return res.Miss();
            }

            creating = false;
            return Result.Success;
        }
        finally
        {
            // Revert changes if an error happened in the middle of creation
            if (creating)
            {
                res = DeleteSaveDataFileSystemCore(creationInfo.SpaceId, saveDataId, false);
                if (res.IsFailure())
                {
                    Hos.Diag.Impl.LogImpl(Log.EmptyModuleName, LogSeverity.Info, GetRollbackErrorMessage(res));
                }

                if (accessorInitialized && saveDataId != SaveIndexerId)
                {
                    res = accessor.Get.GetInterface().GetValue(out SaveDataIndexerValue value, saveDataId);

                    if (res.IsSuccess() && value.SpaceId == creationInfo.SpaceId)
                    {
                        accessor.Get.GetInterface().Delete(saveDataId);
                        if (res.IsFailure() && !ResultFs.TargetNotFound.Includes(res))
                        {
                            Hos.Diag.Impl.LogImpl(Log.EmptyModuleName, LogSeverity.Info, GetRollbackErrorMessage(res));
                        }

                        accessor.Get.GetInterface().Commit();
                        if (res.IsFailure())
                        {
                            Hos.Diag.Impl.LogImpl(Log.EmptyModuleName, LogSeverity.Info, GetRollbackErrorMessage(res));
                        }
                    }
                }
            }

            static byte[] GetRollbackErrorMessage(Result result)
            {
                string message = $"[fs] Error : Failed to rollback save data creation. ({result.Value:x})\n";
                return System.Text.Encoding.UTF8.GetBytes(message);
            }
        }
    }

    private Result CreateSaveDataFileSystemCore(in SaveDataAttribute attribute, in SaveDataCreationInfo creationInfo,
        in SaveDataMetaInfo metaInfo, in Optional<HashSalt> hashSalt, bool leaveUnfinalized)
    {
        // Changed: The original allocates a SaveDataCreationInfo2 on the heap for some reason
        Result res = SaveDataCreationInfo2.Make(out SaveDataCreationInfo2 newCreationInfo, in attribute,
            creationInfo.Size, creationInfo.JournalSize, creationInfo.BlockSize, creationInfo.OwnerId,
            creationInfo.Flags, creationInfo.SpaceId, GetSaveDataFormatType(in attribute));
        if (res.IsFailure()) return res.Miss();

        newCreationInfo.IsHashSaltEnabled = hashSalt.HasValue;
        if (hashSalt.HasValue)
        {
            newCreationInfo.HashSalt = hashSalt.ValueRo;
        }

        newCreationInfo.MetaType = metaInfo.Type;
        newCreationInfo.MetaSize = metaInfo.Size;

        res = CreateSaveDataFileSystemCore(in newCreationInfo, leaveUnfinalized);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result GetSaveDataInfo(out SaveDataInfo info, SaveDataSpaceId spaceId, in SaveDataAttribute attribute)
    {
        UnsafeHelpers.SkipParamInit(out info);

        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.NonGameCard);

        using var accessor = new UniqueRef<SaveDataIndexerAccessor>();
        Result res = OpenSaveDataIndexerAccessor(ref accessor.Ref, spaceId);
        if (res.IsFailure()) return res.Miss();

        res = accessor.Get.GetInterface().Get(out SaveDataIndexerValue value, in attribute);
        if (res.IsFailure()) return res.Miss();

        SaveDataIndexer.GenerateSaveDataInfo(out info, in attribute, in value);
        return Result.Success;
    }

    public Result QuerySaveDataTotalSize(out long outTotalSize, long dataSize, long journalSize)
    {
        UnsafeHelpers.SkipParamInit(out outTotalSize);

        if (dataSize < 0 || journalSize < 0)
            return ResultFs.InvalidSize.Log();

        Result res = _serviceImpl.QuerySaveDataTotalSize(out long totalSize, SaveDataBlockSize, dataSize, journalSize);
        if (res.IsFailure()) return res.Miss();

        outTotalSize = totalSize;
        return Result.Success;
    }

    public Result CreateSaveDataFileSystem(in SaveDataAttribute attribute, in SaveDataCreationInfo creationInfo,
        in SaveDataMetaInfo metaInfo)
    {
        // Changed: The original allocates a SaveDataCreationInfo2 on the heap for some reason
        Result res = SaveDataCreationInfo2.Make(out SaveDataCreationInfo2 newCreationInfo, in attribute,
            creationInfo.Size, creationInfo.JournalSize, creationInfo.BlockSize, creationInfo.OwnerId,
            creationInfo.Flags, creationInfo.SpaceId, GetSaveDataFormatType(in attribute));
        if (res.IsFailure()) return res.Miss();

        newCreationInfo.IsHashSaltEnabled = false;
        newCreationInfo.MetaType = metaInfo.Type;
        newCreationInfo.MetaSize = metaInfo.Size;

        res = CreateSaveDataFileSystemWithCreationInfo2(in newCreationInfo);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result CreateSaveDataFileSystemWithHashSalt(in SaveDataAttribute attribute,
        in SaveDataCreationInfo creationInfo, in SaveDataMetaInfo metaInfo, in HashSalt hashSalt)
    {
        // Changed: The original allocates a SaveDataCreationInfo2 on the heap for some reason
        Result res = SaveDataCreationInfo2.Make(out SaveDataCreationInfo2 newCreationInfo, in attribute,
            creationInfo.Size, creationInfo.JournalSize, creationInfo.BlockSize, creationInfo.OwnerId,
            creationInfo.Flags, creationInfo.SpaceId, GetSaveDataFormatType(in attribute));
        if (res.IsFailure()) return res.Miss();

        newCreationInfo.IsHashSaltEnabled = true;
        newCreationInfo.HashSalt = hashSalt;
        newCreationInfo.MetaType = metaInfo.Type;
        newCreationInfo.MetaSize = metaInfo.Size;

        res = CreateSaveDataFileSystemWithCreationInfo2(in newCreationInfo);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result CreateSaveDataFileSystemWithCreationInfo2(in SaveDataCreationInfo2 creationInfo)
    {
        StorageLayoutType storageFlag = DecidePossibleStorageFlag(creationInfo.Attribute.Type, creationInfo.SpaceId);
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        if (creationInfo.IsHashSaltEnabled &&
            !programInfo.AccessControl.CanCall(OperationType.CreateSaveDataWithHashSalt))
        {
            return ResultFs.PermissionDenied.Log();
        }

        ProgramId resolvedProgramId = ResolveDefaultSaveDataReferenceProgramId(programInfo.ProgramId);
        res = SaveDataAccessibilityChecker.CheckCreate(in creationInfo.Attribute, creationInfo.OwnerId, programInfo,
            resolvedProgramId);
        if (res.IsFailure()) return res.Miss();

        if (creationInfo.Attribute.Type == SaveDataType.Account && creationInfo.Attribute.UserId == InvalidUserId)
        {
            // Changed: The original allocates a SaveDataCreationInfo2 on the heap for some reason
            SaveDataCreationInfo2 newCreationInfo = creationInfo;

            if (newCreationInfo.Attribute.ProgramId == ProgramId.InvalidId)
            {
                newCreationInfo.Attribute.ProgramId = ResolveDefaultSaveDataReferenceProgramId(programInfo.ProgramId);
            }

            if (newCreationInfo.OwnerId == 0)
            {
                newCreationInfo.OwnerId = ResolveDefaultSaveDataReferenceProgramId(programInfo.ProgramId).Value;
            }

            res = CreateSaveDataFileSystemCore(in newCreationInfo, leaveUnfinalized: false);
            if (res.IsFailure()) return res.Miss();
        }
        else
        {
            res = CreateSaveDataFileSystemCore(in creationInfo, leaveUnfinalized: false);
            if (res.IsFailure()) return res.Miss();
        }

        return Result.Success;
    }

    public Result CreateSaveDataFileSystemBySystemSaveDataId(in SaveDataAttribute attribute,
        in SaveDataCreationInfo creationInfo)
    {
        StorageLayoutType storageFlag = DecidePossibleStorageFlag(attribute.Type, creationInfo.SpaceId);
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        if (!IsStaticSaveDataIdValueRange(attribute.StaticSaveDataId))
            return ResultFs.InvalidArgument.Log();

        ulong ownerId = creationInfo.OwnerId == 0 ? programInfo.ProgramIdValue : creationInfo.OwnerId;

        res = SaveDataAccessibilityChecker.CheckCreate(in attribute, ownerId, programInfo, programInfo.ProgramId);
        if (res.IsFailure()) return res.Miss();

        // Changed: The original allocates a SaveDataCreationInfo2 on the heap for some reason
        res = SaveDataCreationInfo2.Make(out SaveDataCreationInfo2 newCreationInfo, in attribute, creationInfo.Size,
            creationInfo.JournalSize, creationInfo.BlockSize, ownerId, creationInfo.Flags, creationInfo.SpaceId,
            GetSaveDataFormatType(in attribute));
        if (res.IsFailure()) return res.Miss();

        newCreationInfo.IsHashSaltEnabled = false;

        // Static system saves don't usually have meta files
        newCreationInfo.MetaType = SaveDataMetaType.None;
        newCreationInfo.MetaSize = 0;

        res = CreateSaveDataFileSystemCore(in newCreationInfo, leaveUnfinalized: false);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result ExtendSaveDataFileSystem(SaveDataSpaceId spaceId, ulong saveDataId, long dataSize, long journalSize)
    {
        SaveDataAttribute key;

        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        using (var accessor = new UniqueRef<SaveDataIndexerAccessor>())
        {
            res = OpenSaveDataIndexerAccessor(ref accessor.Ref, spaceId);
            if (res.IsFailure()) return res.Miss();

            res = accessor.Get.GetInterface().GetKey(out key, saveDataId);
            if (res.IsFailure()) return res.Miss();

            Result ReadExtraData(out SaveDataExtraData data)
            {
                using Path savePath = _saveDataRootPath.DangerousGetPath();
                return _serviceImpl.ReadSaveDataFileSystemExtraData(out data, spaceId, saveDataId, key.Type, in savePath);
            }

            // Check if we have permissions to extend this save data.
            res = SaveDataAccessibilityChecker.CheckExtend(spaceId, in key, programInfo, ReadExtraData);
            if (res.IsFailure()) return res.Miss();

            // Check that the save data is in a state that we can start extending it.
            res = accessor.Get.GetInterface().GetValue(out SaveDataIndexerValue value, saveDataId);
            if (res.IsFailure()) return res.Miss();

            switch (value.State)
            {
                case SaveDataState.Normal:
                    break;
                case SaveDataState.Processing:
                case SaveDataState.MarkedForDeletion:
                case SaveDataState.Extending:
                    return ResultFs.TargetLocked.Log();
                case SaveDataState.State2:
                    return ResultFs.SaveDataCorrupted.Log();
                default:
                    return ResultFs.InvalidSaveDataState.Log();
            }

            // Mark the save data as being extended.
            res = accessor.Get.GetInterface().SetState(saveDataId, SaveDataState.Extending);
            if (res.IsFailure()) return res.Miss();

            res = accessor.Get.GetInterface().Commit();
            if (res.IsFailure()) return res.Miss();
        }

        // Get the indexer key for the save data.
        using (var accessor = new UniqueRef<SaveDataIndexerAccessor>())
        {
            res = OpenSaveDataIndexerAccessor(ref accessor.Ref, spaceId);
            if (res.IsFailure()) return res.Miss();

            res = accessor.Get.GetInterface().GetKey(out key, saveDataId);
            if (res.IsFailure()) return res.Miss();
        }

        // Start the extension.
        using Path saveDataRootPath = _saveDataRootPath.DangerousGetPath();
        Result extendResult = _serviceImpl.StartExtendSaveDataFileSystem(out long extendedTotalSize, saveDataId,
            spaceId, key.Type, dataSize, journalSize, in saveDataRootPath);

        if (extendResult.IsFailure())
        {
            // Try to return the save data to its original state if something went wrong.
            using var accessor = new UniqueRef<SaveDataIndexerAccessor>();
            res = OpenSaveDataIndexerAccessor(ref accessor.Ref, spaceId);
            if (res.IsFailure()) return res.Miss();

            res = accessor.Get.GetInterface().GetValue(out SaveDataIndexerValue value, saveDataId);
            if (res.IsSuccess())
            {
                _serviceImpl.RevertExtendSaveDataFileSystem(saveDataId, spaceId, value.Size, in saveDataRootPath);
            }

            res = accessor.Get.GetInterface().SetState(saveDataId, SaveDataState.Normal);
            if (res.IsSuccess())
            {
                accessor.Get.GetInterface().Commit().IgnoreResult();
            }

            return extendResult.Log();
        }

        // Update the save data's size in the indexer.
        using (var accessor = new UniqueRef<SaveDataIndexerAccessor>())
        {
            res = OpenSaveDataIndexerAccessor(ref accessor.Ref, spaceId);
            if (res.IsFailure()) return res.Miss();

            accessor.Get.GetInterface().SetSize(saveDataId, extendedTotalSize).IgnoreResult();

            res = accessor.Get.GetInterface().Commit();
            if (res.IsFailure()) return res.Miss();
        }

        // Finish the extension.
        res = _serviceImpl.FinishExtendSaveDataFileSystem(saveDataId, spaceId);
        if (res.IsFailure()) return res.Miss();

        // Set the save data's state back to normal.
        using (var accessor = new UniqueRef<SaveDataIndexerAccessor>())
        {
            res = OpenSaveDataIndexerAccessor(ref accessor.Ref, spaceId);
            if (res.IsFailure()) return res.Miss();

            res = accessor.Get.GetInterface().SetState(saveDataId, SaveDataState.Normal);
            if (res.IsFailure()) return res.Miss();

            res = accessor.Get.GetInterface().Commit();
            if (res.IsFailure()) return res.Miss();
        }

        return Result.Success;
    }

    public Result OpenSaveDataFileSystem(ref SharedRef<IFileSystemSf> fileSystem, SaveDataSpaceId spaceId,
        in SaveDataAttribute attribute)
    {
        return OpenUserSaveDataFileSystem(ref fileSystem, spaceId, in attribute, openReadOnly: false).Ret();
    }

    public Result OpenReadOnlySaveDataFileSystem(ref SharedRef<IFileSystemSf> fileSystem, SaveDataSpaceId spaceId,
        in SaveDataAttribute attribute)
    {
        return OpenUserSaveDataFileSystem(ref fileSystem, spaceId, in attribute, openReadOnly: true).Ret();
    }

    public Result OpenSaveDataFileSystemCore(ref SharedRef<IFileSystem> outFileSystem, out ulong outSaveDataId,
        SaveDataSpaceId spaceId, in SaveDataAttribute attribute, bool openReadOnly, bool cacheExtraData)
    {
        UnsafeHelpers.SkipParamInit(out outSaveDataId);

        using var accessor = new UniqueRef<SaveDataIndexerAccessor>();

        ulong saveDataId;
        bool isStaticSaveDataId =
            attribute.StaticSaveDataId != InvalidSystemSaveDataId && attribute.UserId == InvalidUserId;

        // Get the ID of the save data
        if (isStaticSaveDataId)
        {
            saveDataId = attribute.StaticSaveDataId;
        }
        else
        {
            Result res = OpenSaveDataIndexerAccessor(ref accessor.Ref, spaceId);
            if (res.IsFailure()) return res.Miss();

            res = accessor.Get.GetInterface().Get(out SaveDataIndexerValue indexerValue, in attribute);
            if (res.IsFailure()) return res.Miss();

            if (indexerValue.SpaceId != ConvertToRealSpaceId(spaceId))
                return ResultFs.TargetNotFound.Log();

            if (indexerValue.State == SaveDataState.Extending)
                return ResultFs.SaveDataExtending.Log();

            saveDataId = indexerValue.SaveDataId;
        }

        // Open the save data using its ID
        using Path saveDataRootPath = _saveDataRootPath.DangerousGetPath();
        Result saveFsResult = _serviceImpl.OpenSaveDataFileSystem(ref outFileSystem, spaceId, saveDataId,
            in saveDataRootPath, openReadOnly, attribute.Type, cacheExtraData);

        if (saveFsResult.IsSuccess())
        {
            outSaveDataId = saveDataId;
            return Result.Success;
        }

        // Copy the key so we can use it in a local function
        SaveDataAttribute key = attribute;

        // Remove the save from the indexer if the save is missing from the disk.
        if (ResultFs.PathNotFound.Includes(saveFsResult))
        {
            Result res = RemoveSaveIndexerEntry();
            if (res.IsFailure()) return res.Miss();

            return ResultFs.TargetNotFound.LogConverted(saveFsResult);
        }

        if (ResultFs.TargetNotFound.Includes(saveFsResult))
        {
            Result res = RemoveSaveIndexerEntry();
            if (res.IsFailure()) return res.Miss();
        }

        return saveFsResult;

        Result RemoveSaveIndexerEntry()
        {
            if (saveDataId == SaveIndexerId)
                return Result.Success;

            if (isStaticSaveDataId)
            {
                // The accessor won't be open yet if the save has a static ID
                Result res = OpenSaveDataIndexerAccessor(ref accessor.Ref, spaceId);
                if (res.IsFailure()) return res.Miss();

                // Check the space ID of the save data
                res = accessor.Get.GetInterface().Get(out SaveDataIndexerValue value, in key);
                if (res.IsFailure()) return res.Miss();

                if (value.SpaceId != ConvertToRealSpaceId(spaceId))
                    return ResultFs.TargetNotFound.Log();
            }

            // Remove the indexer entry. Nintendo ignores these results
            accessor.Get.GetInterface().Delete(saveDataId).IgnoreResult();
            accessor.Get.GetInterface().Commit().IgnoreResult();

            return Result.Success;
        }
    }

    private Result OpenUserSaveDataFileSystemCore(ref SharedRef<IFileSystemSf> outFileSystem, SaveDataSpaceId spaceId,
        in SaveDataAttribute attribute, ProgramInfo programInfo, bool openReadOnly)
    {
        StorageLayoutType storageFlag = DecidePossibleStorageFlag(attribute.Type, spaceId);
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        // Try grabbing the mount count semaphore
        using var mountCountSemaphore = new UniqueRef<IUniqueLock>();
        Result res = TryAcquireSaveDataMountCountSemaphore(ref mountCountSemaphore.Ref);
        if (res.IsFailure()) return res.Miss();

        using Path saveDataRootPath = _saveDataRootPath.DangerousGetPath();
        bool useAsyncFileSystem = !_serviceImpl.IsAllowedDirectorySaveData(spaceId, in saveDataRootPath);

        using var fileSystem = new SharedRef<IFileSystem>();

        // Open the file system
        res = OpenSaveDataFileSystemCore(ref fileSystem.Ref, out ulong saveDataId, spaceId, in attribute,
            openReadOnly, cacheExtraData: true);
        if (res.IsFailure()) return res.Miss();

        // Can't use attribute in a closure, so copy the needed field
        SaveDataType type = attribute.Type;

        Result ReadExtraData(out SaveDataExtraData data)
        {
            using Path savePath = _saveDataRootPath.DangerousGetPath();
            return _serviceImpl.ReadSaveDataFileSystemExtraData(out data, spaceId, saveDataId, type, in savePath);
        }

        // Check if we have permissions to open this save data
        res = SaveDataAccessibilityChecker.CheckOpen(in attribute, programInfo, ReadExtraData);
        if (res.IsFailure()) return res.Miss();

        // Add all the wrappers for the file system
        using var typeSetFileSystem =
            new SharedRef<IFileSystem>(new StorageLayoutTypeSetFileSystem(ref fileSystem.Ref, storageFlag));

        using var asyncFileSystem = new SharedRef<IFileSystem>();

        if (useAsyncFileSystem)
        {
            asyncFileSystem.Reset(new AsynchronousAccessFileSystem(ref typeSetFileSystem.Ref));
        }
        else
        {
            asyncFileSystem.SetByMove(ref typeSetFileSystem.Ref);
        }

        using SharedRef<SaveDataFileSystemService> saveService = GetSharedFromThis();
        using var openEntryCountAdapter =
            new SharedRef<IEntryOpenCountSemaphoreManager>(new SaveDataOpenCountAdapter(ref saveService.Ref));

        using var openCountFileSystem = new SharedRef<IFileSystem>(new OpenCountFileSystem(ref asyncFileSystem.Ref,
            ref openEntryCountAdapter.Ref, ref mountCountSemaphore.Ref));

        var pathFlags = new PathFlags();
        pathFlags.AllowBackslash();
        pathFlags.AllowAllCharacters();

        using SharedRef<IFileSystemSf> fileSystemAdapter =
            FileSystemInterfaceAdapter.CreateShared(ref openCountFileSystem.Ref, pathFlags, false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref);

        return Result.Success;
    }

    private Result OpenUserSaveDataFileSystem(ref SharedRef<IFileSystemSf> outFileSystem, SaveDataSpaceId spaceId,
        in SaveDataAttribute attribute, bool openReadOnly)
    {
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        res = SaveDataAccessibilityChecker.CheckOpenPre(in attribute, programInfo);
        if (res.IsFailure()) return res.Miss();

        SaveDataAttribute tempAttribute;

        if (attribute.ProgramId.Value == 0)
        {
            ProgramId resolvedProgramId = ResolveDefaultSaveDataReferenceProgramId(programInfo.ProgramId);

            res = SaveDataAttribute.Make(out tempAttribute, resolvedProgramId, attribute.Type, attribute.UserId,
                attribute.StaticSaveDataId, attribute.Index);
            if (res.IsFailure()) return res.Miss();
        }
        else
        {
            tempAttribute = attribute;
        }

        SaveDataSpaceId targetSpaceId;

        if (tempAttribute.Type == SaveDataType.Cache)
        {
            // Check whether the save is on the SD card or the BIS
            res = GetCacheStorageSpaceId(out targetSpaceId, tempAttribute.ProgramId.Value);
            if (res.IsFailure()) return res.Miss();
        }
        else
        {
            targetSpaceId = spaceId;
        }

        return OpenUserSaveDataFileSystemCore(ref outFileSystem, targetSpaceId, in tempAttribute, programInfo,
            openReadOnly).Ret();
    }

    public Result OpenSaveDataFileSystemBySystemSaveDataId(ref SharedRef<IFileSystemSf> outFileSystem,
        SaveDataSpaceId spaceId, in SaveDataAttribute attribute)
    {
        if (!IsStaticSaveDataIdValueRange(attribute.StaticSaveDataId))
            return ResultFs.InvalidArgument.Log();

        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        StorageLayoutType storageFlag = DecidePossibleStorageFlag(attribute.Type, spaceId);
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        Accessibility accessibility =
            programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountSystemSaveData);

        if (!accessibility.CanRead || !accessibility.CanWrite)
            return ResultFs.PermissionDenied.Log();

        using Path saveDataRootPath = _saveDataRootPath.DangerousGetPath();
        bool useAsyncFileSystem = !_serviceImpl.IsAllowedDirectorySaveData(spaceId, in saveDataRootPath);

        using var fileSystem = new SharedRef<IFileSystem>();

        // Open the file system
        res = OpenSaveDataFileSystemCore(ref fileSystem.Ref, out ulong saveDataId, spaceId, in attribute,
            openReadOnly: false, cacheExtraData: true);
        if (res.IsFailure()) return res.Miss();

        // Can't use attribute in a closure, so copy the needed field
        SaveDataType type = attribute.Type;

        Result ReadExtraData(out SaveDataExtraData data)
        {
            using Path savePath = _saveDataRootPath.DangerousGetPath();
            return _serviceImpl.ReadSaveDataFileSystemExtraData(out data, spaceId, saveDataId, type, in savePath);
        }

        // Check if we have permissions to open this save data
        res = SaveDataAccessibilityChecker.CheckOpen(in attribute, programInfo, ReadExtraData);
        if (res.IsFailure()) return res.Miss();

        // Add all the wrappers for the file system
        using var typeSetFileSystem =
            new SharedRef<IFileSystem>(new StorageLayoutTypeSetFileSystem(ref fileSystem.Ref, storageFlag));

        using var asyncFileSystem = new SharedRef<IFileSystem>();

        if (useAsyncFileSystem)
        {
            asyncFileSystem.Reset(new AsynchronousAccessFileSystem(ref typeSetFileSystem.Ref));
        }
        else
        {
            asyncFileSystem.SetByMove(ref typeSetFileSystem.Ref);
        }

        using SharedRef<SaveDataFileSystemService> saveService = GetSharedFromThis();
        using var openEntryCountAdapter =
            new SharedRef<IEntryOpenCountSemaphoreManager>(new SaveDataOpenCountAdapter(ref saveService.Ref));

        using var openCountFileSystem = new SharedRef<IFileSystem>(
            new OpenCountFileSystem(ref asyncFileSystem.Ref, ref openEntryCountAdapter.Ref));

        var pathFlags = new PathFlags();
        pathFlags.AllowBackslash();
        pathFlags.AllowAllCharacters();

        using SharedRef<IFileSystemSf> fileSystemAdapter =
            FileSystemInterfaceAdapter.CreateShared(ref openCountFileSystem.Ref, pathFlags,
                allowAllOperations: false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref);

        return Result.Success;
    }

    // ReSharper disable once UnusedParameter.Local
    // Nintendo used isTemporarySaveData in older FS versions, but never removed the parameter.
    private Result ReadSaveDataFileSystemExtraDataCore(out SaveDataExtraData extraData, SaveDataSpaceId spaceId,
        ulong saveDataId, bool isTemporarySaveData)
    {
        UnsafeHelpers.SkipParamInit(out extraData);

        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.NonGameCard);
        using var accessor = new UniqueRef<SaveDataIndexerAccessor>();

        Result res = OpenSaveDataIndexerAccessor(ref accessor.Ref, spaceId);
        if (res.IsFailure()) return res.Miss();

        res = accessor.Get.GetInterface().GetKey(out SaveDataAttribute key, saveDataId);
        if (res.IsFailure()) return res.Miss();

        using Path saveDataRootPath = _saveDataRootPath.DangerousGetPath();
        return _serviceImpl.ReadSaveDataFileSystemExtraData(out extraData, spaceId, saveDataId, key.Type,
            in saveDataRootPath).Ret();
    }

    private Result ReadSaveDataFileSystemExtraDataCore(out SaveDataExtraData extraData,
        Optional<SaveDataSpaceId> spaceId, ulong saveDataId, in SaveDataExtraData extraDataMask)
    {
        UnsafeHelpers.SkipParamInit(out extraData);

        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.NonGameCard);

        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        SaveDataSpaceId resolvedSpaceId;
        SaveDataAttribute key;

        if (!spaceId.HasValue)
        {
            using var accessor = new UniqueRef<SaveDataIndexerAccessor>();

            if (IsStaticSaveDataIdValueRange(saveDataId))
            {
                res = OpenSaveDataIndexerAccessor(ref accessor.Ref, SaveDataSpaceId.System);
                if (res.IsFailure()) return res.Miss();
            }
            else
            {
                res = OpenSaveDataIndexerAccessor(ref accessor.Ref, SaveDataSpaceId.User);
                if (res.IsFailure()) return res.Miss();
            }

            res = accessor.Get.GetInterface().GetValue(out SaveDataIndexerValue value, saveDataId);
            if (res.IsFailure()) return res.Miss();

            resolvedSpaceId = value.SpaceId;

            res = accessor.Get.GetInterface().GetKey(out key, saveDataId);
            if (res.IsFailure()) return res.Miss();
        }
        else
        {
            using var accessor = new UniqueRef<SaveDataIndexerAccessor>();

            res = OpenSaveDataIndexerAccessor(ref accessor.Ref, spaceId.ValueRo);
            if (res.IsFailure()) return res.Miss();

            res = accessor.Get.GetInterface().GetValue(out SaveDataIndexerValue _, saveDataId);
            if (res.IsFailure()) return res.Miss();

            resolvedSpaceId = spaceId.ValueRo;

            res = accessor.Get.GetInterface().GetKey(out key, saveDataId);
            if (res.IsFailure()) return res.Miss();
        }

        Result ReadExtraData(out SaveDataExtraData data)
        {
            using Path savePath = _saveDataRootPath.DangerousGetPath();
            return _serviceImpl.ReadSaveDataFileSystemExtraData(out data, resolvedSpaceId, saveDataId, key.Type,
                in savePath);
        }

        res = SaveDataAccessibilityChecker.CheckReadExtraData(in key, in extraDataMask, programInfo, ReadExtraData);
        if (res.IsFailure()) return res.Miss();

        using Path saveDataRootPath = _saveDataRootPath.DangerousGetPath();
        res = _serviceImpl.ReadSaveDataFileSystemExtraData(out SaveDataExtraData tempExtraData, resolvedSpaceId,
            saveDataId, key.Type, in saveDataRootPath);
        if (res.IsFailure()) return res.Miss();

        MaskExtraData(ref tempExtraData, in extraDataMask);
        extraData = tempExtraData;

        return Result.Success;
    }

    public Result ReadSaveDataFileSystemExtraData(OutBuffer extraData, ulong saveDataId)
    {
        if (extraData.Size != Unsafe.SizeOf<SaveDataExtraData>())
            return ResultFs.InvalidArgument.Log();

        // Make a mask for reading the entire extra data
        Unsafe.SkipInit(out SaveDataExtraData extraDataMask);
        SpanHelpers.AsByteSpan(ref extraDataMask).Fill(0xFF);

        return ReadSaveDataFileSystemExtraDataCore(out SpanHelpers.AsStruct<SaveDataExtraData>(extraData.Buffer),
            spaceId: default, saveDataId, in extraDataMask).Ret();
    }

    public Result ReadSaveDataFileSystemExtraDataBySaveDataAttribute(OutBuffer extraData, SaveDataSpaceId spaceId,
        in SaveDataAttribute attribute)
    {
        if (extraData.Size != Unsafe.SizeOf<SaveDataExtraData>())
            return ResultFs.InvalidArgument.Log();

        ref SaveDataExtraData extraDataRef = ref SpanHelpers.AsStruct<SaveDataExtraData>(extraData.Buffer);

        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        SaveDataAttribute tempAttribute = attribute;

        if (tempAttribute.ProgramId == AutoResolveCallerProgramId)
        {
            tempAttribute.ProgramId = ResolveDefaultSaveDataReferenceProgramId(programInfo.ProgramId);
        }

        res = GetSaveDataInfo(out SaveDataInfo info, spaceId, in tempAttribute);
        if (res.IsFailure()) return res.Miss();

        // Make a mask for reading the entire extra data
        Unsafe.SkipInit(out SaveDataExtraData extraDataMask);
        SpanHelpers.AsByteSpan(ref extraDataMask).Fill(0xFF);

        return ReadSaveDataFileSystemExtraDataCore(out extraDataRef, spaceId, info.SaveDataId, in extraDataMask).Ret();
    }

    public Result ReadSaveDataFileSystemExtraDataBySaveDataSpaceId(OutBuffer extraData, SaveDataSpaceId spaceId,
        ulong saveDataId)
    {
        if (extraData.Size != Unsafe.SizeOf<SaveDataExtraData>())
            return ResultFs.InvalidArgument.Log();

        ref SaveDataExtraData extraDataRef = ref SpanHelpers.AsStruct<SaveDataExtraData>(extraData.Buffer);

        // Make a mask for reading the entire extra data
        Unsafe.SkipInit(out SaveDataExtraData extraDataMask);
        SpanHelpers.AsByteSpan(ref extraDataMask).Fill(0xFF);

        return ReadSaveDataFileSystemExtraDataCore(out extraDataRef, spaceId, saveDataId, in extraDataMask).Ret();
    }

    public Result ReadSaveDataFileSystemExtraDataWithMaskBySaveDataAttribute(OutBuffer extraData,
        SaveDataSpaceId spaceId, in SaveDataAttribute attribute, InBuffer extraDataMask)
    {
        if (extraDataMask.Size != Unsafe.SizeOf<SaveDataExtraData>())
            return ResultFs.InvalidArgument.Log();

        if (extraData.Size != Unsafe.SizeOf<SaveDataExtraData>())
            return ResultFs.InvalidArgument.Log();

        ref readonly SaveDataExtraData maskRef =
            ref SpanHelpers.AsReadOnlyStruct<SaveDataExtraData>(extraDataMask.Buffer);

        ref SaveDataExtraData extraDataRef = ref SpanHelpers.AsStruct<SaveDataExtraData>(extraData.Buffer);

        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        SaveDataAttribute tempAttribute = attribute;

        if (tempAttribute.ProgramId == AutoResolveCallerProgramId)
        {
            tempAttribute.ProgramId = ResolveDefaultSaveDataReferenceProgramId(programInfo.ProgramId);
        }

        res = GetSaveDataInfo(out SaveDataInfo info, spaceId, in tempAttribute);
        if (res.IsFailure()) return res.Miss();

        return ReadSaveDataFileSystemExtraDataCore(out extraDataRef, spaceId, info.SaveDataId, in maskRef).Ret();
    }

    private Result WriteSaveDataFileSystemExtraDataCore(SaveDataSpaceId spaceId, ulong saveDataId,
        in SaveDataExtraData extraData, SaveDataType saveType, bool updateTimeStamp)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.NonGameCard);

        using Path saveDataRootPath = _saveDataRootPath.DangerousGetPath();
        return _serviceImpl.WriteSaveDataFileSystemExtraData(spaceId, saveDataId, in extraData, in saveDataRootPath,
            saveType, updateTimeStamp).Ret();
    }

    private Result WriteSaveDataFileSystemExtraDataWithMaskCore(ulong saveDataId, SaveDataSpaceId spaceId,
        in SaveDataExtraData extraData, in SaveDataExtraData extraDataMask)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.NonGameCard);

        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        using var accessor = new UniqueRef<SaveDataIndexerAccessor>();

        res = OpenSaveDataIndexerAccessor(ref accessor.Ref, spaceId);
        if (res.IsFailure()) return res.Miss();

        res = accessor.Get.GetInterface().GetKey(out SaveDataAttribute key, saveDataId);
        if (res.IsFailure()) return res.Miss();

        Result ReadExtraData(out SaveDataExtraData data)
        {
            using Path saveDataRootPath = _saveDataRootPath.DangerousGetPath();
            return _serviceImpl.ReadSaveDataFileSystemExtraData(out data, spaceId, saveDataId, key.Type,
                in saveDataRootPath);
        }

        res = SaveDataAccessibilityChecker.CheckWriteExtraData(in key, in extraDataMask, programInfo, ReadExtraData);
        if (res.IsFailure()) return res.Miss();

        using Path saveDataRootPath = _saveDataRootPath.DangerousGetPath();
        res = _serviceImpl.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraDataModify, spaceId, saveDataId,
            key.Type, in saveDataRootPath);
        if (res.IsFailure()) return res.Miss();

        ModifySaveDataExtraData(ref extraDataModify, in extraData, in extraDataMask);

        return _serviceImpl.WriteSaveDataFileSystemExtraData(spaceId, saveDataId, in extraDataModify,
            in saveDataRootPath, key.Type, updateTimeStamp: false).Ret();
    }

    public Result WriteSaveDataFileSystemExtraData(ulong saveDataId, SaveDataSpaceId spaceId, InBuffer extraData)
    {
        if (extraData.Size != Unsafe.SizeOf<SaveDataExtraData>())
            return ResultFs.InvalidArgument.Log();

        ref readonly SaveDataExtraData extraDataRef =
            ref SpanHelpers.AsReadOnlyStruct<SaveDataExtraData>(extraData.Buffer);

        SaveDataExtraData extraDataMask = default;
        extraDataMask.Flags = unchecked((SaveDataFlags)0xFFFFFFFF);

        return WriteSaveDataFileSystemExtraDataWithMaskCore(saveDataId, spaceId, in extraDataRef, in extraDataMask).Ret();
    }

    public Result WriteSaveDataFileSystemExtraDataWithMaskBySaveDataAttribute(in SaveDataAttribute attribute,
        SaveDataSpaceId spaceId, InBuffer extraData, InBuffer extraDataMask)
    {
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        SaveDataAttribute tempAttribute = attribute;

        if (tempAttribute.ProgramId == AutoResolveCallerProgramId)
        {
            tempAttribute.ProgramId = ResolveDefaultSaveDataReferenceProgramId(programInfo.ProgramId);
        }

        res = GetSaveDataInfo(out SaveDataInfo info, spaceId, in tempAttribute);
        if (res.IsFailure()) return res.Miss();

        return WriteSaveDataFileSystemExtraDataWithMask(info.SaveDataId, spaceId, extraData, extraDataMask).Ret();
    }

    public Result WriteSaveDataFileSystemExtraDataWithMask(ulong saveDataId, SaveDataSpaceId spaceId,
        InBuffer extraData, InBuffer extraDataMask)
    {
        if (extraData.Size != Unsafe.SizeOf<SaveDataExtraData>())
            return ResultFs.InvalidArgument.Log();

        if (extraDataMask.Size != Unsafe.SizeOf<SaveDataExtraData>())
            return ResultFs.InvalidArgument.Log();

        ref readonly SaveDataExtraData extraDataRef =
            ref SpanHelpers.AsReadOnlyStruct<SaveDataExtraData>(extraData.Buffer);

        ref readonly SaveDataExtraData maskRef =
            ref SpanHelpers.AsReadOnlyStruct<SaveDataExtraData>(extraDataMask.Buffer);

        return WriteSaveDataFileSystemExtraDataWithMaskCore(saveDataId, spaceId, in extraDataRef, in maskRef).Ret();
    }

    public Result OpenSaveDataInfoReader(ref SharedRef<ISaveDataInfoReader> outInfoReader)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.Bis);

        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.OpenSaveDataInfoReader))
            return ResultFs.PermissionDenied.Log();

        if (!programInfo.AccessControl.CanCall(OperationType.OpenSaveDataInfoReaderForSystem))
            return ResultFs.PermissionDenied.Log();

        using var reader = new SharedRef<SaveDataInfoReaderImpl>();

        using (var accessor = new UniqueRef<SaveDataIndexerAccessor>())
        {
            res = OpenSaveDataIndexerAccessor(ref accessor.Ref, SaveDataSpaceId.System);
            if (res.IsFailure()) return res.Miss();

            res = accessor.Get.GetInterface().OpenSaveDataInfoReader(ref reader.Ref);
            if (res.IsFailure()) return res.Miss();
        }

        outInfoReader.SetByMove(ref reader.Ref);

        return Result.Success;
    }

    public Result OpenSaveDataInfoReaderBySaveDataSpaceId(ref SharedRef<ISaveDataInfoReader> outInfoReader,
        SaveDataSpaceId spaceId)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.NonGameCard);

        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        res = CheckOpenSaveDataInfoReaderAccessControl(programInfo, spaceId);
        if (res.IsFailure()) return res.Miss();

        using var filterReader = new UniqueRef<SaveDataInfoFilterReader>();

        using (var accessor = new UniqueRef<SaveDataIndexerAccessor>())
        {
            res = OpenSaveDataIndexerAccessor(ref accessor.Ref, spaceId);
            if (res.IsFailure()) return res.Miss();

            using var reader = new SharedRef<SaveDataInfoReaderImpl>();

            res = accessor.Get.GetInterface().OpenSaveDataInfoReader(ref reader.Ref);
            if (res.IsFailure()) return res.Miss();

            var filter = new SaveDataInfoFilter(ConvertToRealSpaceId(spaceId), programId: default,
                saveDataType: default, userId: default, saveDataId: default, index: default, (int)SaveDataRank.Primary);

            filterReader.Reset(new SaveDataInfoFilterReader(ref reader.Ref, in filter));
        }

        outInfoReader.Set(ref filterReader.Ref);

        return Result.Success;
    }

    public Result OpenSaveDataInfoReaderWithFilter(ref SharedRef<ISaveDataInfoReader> outInfoReader,
        SaveDataSpaceId spaceId, in SaveDataFilter filter)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.NonGameCard);

        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.OpenSaveDataInfoReaderForInternal))
            return ResultFs.PermissionDenied.Log();

        res = CheckOpenSaveDataInfoReaderAccessControl(programInfo, spaceId);
        if (res.IsFailure()) return res.Miss();

        using var filterReader = new UniqueRef<SaveDataInfoFilterReader>();

        using (var accessor = new UniqueRef<SaveDataIndexerAccessor>())
        {
            res = OpenSaveDataIndexerAccessor(ref accessor.Ref, spaceId);
            if (res.IsFailure()) return res.Miss();

            using var reader = new SharedRef<SaveDataInfoReaderImpl>();

            res = accessor.Get.GetInterface().OpenSaveDataInfoReader(ref reader.Ref);
            if (res.IsFailure()) return res.Miss();

            var infoFilter = new SaveDataInfoFilter(ConvertToRealSpaceId(spaceId), in filter);

            filterReader.Reset(new SaveDataInfoFilterReader(ref reader.Ref, in infoFilter));
        }

        outInfoReader.Set(ref filterReader.Ref);

        return Result.Success;
    }

    private Result FindSaveDataWithFilterImpl(out long count, out SaveDataInfo info, SaveDataSpaceId spaceId,
        in SaveDataInfoFilter infoFilter)
    {
        UnsafeHelpers.SkipParamInit(out count, out info);

        using var reader = new SharedRef<SaveDataInfoReaderImpl>();
        using var accessor = new UniqueRef<SaveDataIndexerAccessor>();

        Result res = OpenSaveDataIndexerAccessor(ref accessor.Ref, spaceId);
        if (res.IsFailure()) return res.Miss();

        res = accessor.Get.GetInterface().OpenSaveDataInfoReader(ref reader.Ref);
        if (res.IsFailure()) return res.Miss();

        using var filterReader =
            new UniqueRef<SaveDataInfoFilterReader>(new SaveDataInfoFilterReader(ref reader.Ref, in infoFilter));

        return filterReader.Get.Read(out count, new OutBuffer(SpanHelpers.AsByteSpan(ref info))).Ret();
    }

    public Result FindSaveDataWithFilter(out long count, OutBuffer saveDataInfoBuffer, SaveDataSpaceId spaceId,
        in SaveDataFilter filter)
    {
        UnsafeHelpers.SkipParamInit(out count);

        if (saveDataInfoBuffer.Size != Unsafe.SizeOf<SaveDataInfo>())
            return ResultFs.InvalidArgument.Log();

        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.NonGameCard);

        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        res = CheckOpenSaveDataInfoReaderAccessControl(programInfo, spaceId);

        if (res.IsFailure())
        {
            if (!ResultFs.PermissionDenied.Includes(res))
                return res;

            // Don't have full info reader permissions. Check if we have find permissions.
            res = SaveDataAccessibilityChecker.CheckFind(in filter, programInfo);
            if (res.IsFailure()) return res.Miss();
        }

        SaveDataFilter tempFilter = filter;
        if (filter.Attribute.ProgramId == ProgramId.InvalidId)
        {
            tempFilter.Attribute.ProgramId = ResolveDefaultSaveDataReferenceProgramId(programInfo.ProgramId);
        }

        var infoFilter = new SaveDataInfoFilter(ConvertToRealSpaceId(spaceId), in tempFilter);

        return FindSaveDataWithFilterImpl(out count, out SpanHelpers.AsStruct<SaveDataInfo>(saveDataInfoBuffer.Buffer),
            spaceId, in infoFilter).Ret();
    }

    private Result CreateEmptyThumbnailFile(SaveDataSpaceId spaceId, ulong saveDataId)
    {
        Result res = _serviceImpl.CreateSaveDataMeta(saveDataId, spaceId, SaveDataMetaType.Thumbnail,
            SaveDataMetaPolicy.ThumbnailFileSize);
        if (res.IsFailure()) return res.Miss();

        using var file = new UniqueRef<IFile>();
        res = _serviceImpl.OpenSaveDataMeta(ref file.Ref, saveDataId, spaceId, SaveDataMetaType.Thumbnail);
        if (res.IsFailure()) return res.Miss();

        Hash hash = default;
        res = file.Get.Write(0, hash.Value, WriteOption.Flush);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    private Result OpenSaveDataInternalStorageFileSystemCore(ref SharedRef<IFileSystem> fileSystem,
        SaveDataSpaceId spaceId, ulong saveDataId, bool useSecondMacKey)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataInternalStorageFileSystem(ref SharedRef<IFileSystemSf> fileSystem,
        SaveDataSpaceId spaceId, ulong saveDataId)
    {
        throw new NotImplementedException();
    }

    public Result QuerySaveDataInternalStorageTotalSize(out long size, SaveDataSpaceId spaceId, ulong saveDataId)
    {
        throw new NotImplementedException();
    }

    public Result GetSaveDataCommitId(out long commitId, SaveDataSpaceId spaceId, ulong saveDataId)
    {
        UnsafeHelpers.SkipParamInit(out commitId);

        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.GetSaveDataCommitId))
            return ResultFs.PermissionDenied.Log();

        Unsafe.SkipInit(out SaveDataExtraData extraData);
        res = ReadSaveDataFileSystemExtraDataBySaveDataSpaceId(OutBuffer.FromStruct(ref extraData), spaceId, saveDataId);
        if (res.IsFailure()) return res.Miss();

        commitId = Impl.Utility.ConvertZeroCommitId(in extraData);
        return Result.Success;
    }

    public Result OpenSaveDataInfoReaderOnlyCacheStorage(ref SharedRef<ISaveDataInfoReader> outInfoReader)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.NonGameCard);

        // Find where the current program's cache storage is located
        Result res = GetCacheStorageSpaceId(out SaveDataSpaceId spaceId);

        if (res.IsFailure())
        {
            spaceId = SaveDataSpaceId.User;

            if (!ResultFs.TargetNotFound.Includes(res))
                return res;
        }

        return OpenSaveDataInfoReaderOnlyCacheStorage(ref outInfoReader, spaceId).Ret();
    }

    private Result OpenSaveDataInfoReaderOnlyCacheStorage(ref SharedRef<ISaveDataInfoReader> outInfoReader,
        SaveDataSpaceId spaceId)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.NonGameCard);

        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        if (spaceId != SaveDataSpaceId.SdUser && spaceId != SaveDataSpaceId.User)
            return ResultFs.InvalidSaveDataSpaceId.Log();

        using var filterReader = new UniqueRef<SaveDataInfoFilterReader>();

        using (var reader = new SharedRef<SaveDataInfoReaderImpl>())
        using (var accessor = new UniqueRef<SaveDataIndexerAccessor>())
        {
            res = OpenSaveDataIndexerAccessor(ref accessor.Ref, spaceId);
            if (res.IsFailure()) return res.Miss();

            res = accessor.Get.GetInterface().OpenSaveDataInfoReader(ref reader.Ref);
            if (res.IsFailure()) return res.Miss();

            ProgramId resolvedProgramId = ResolveDefaultSaveDataReferenceProgramId(programInfo.ProgramId);

            var filter = new SaveDataInfoFilter(ConvertToRealSpaceId(spaceId), resolvedProgramId, SaveDataType.Cache,
                userId: default, saveDataId: default, index: default, (int)SaveDataRank.Primary);

            filterReader.Reset(new SaveDataInfoFilterReader(ref reader.Ref, in filter));
        }

        outInfoReader.Set(ref filterReader.Ref);

        return Result.Success;
    }

    private Result OpenSaveDataMetaFileRaw(ref SharedRef<IFile> outFile, SaveDataSpaceId spaceId, ulong saveDataId,
        SaveDataMetaType metaType, OpenMode mode)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataMetaFile(ref SharedRef<IFileSf> file, SaveDataSpaceId spaceId,
        in SaveDataAttribute attribute, SaveDataMetaType metaType)
    {
        throw new NotImplementedException();
    }

    private Result GetCacheStorageSpaceId(out SaveDataSpaceId spaceId)
    {
        UnsafeHelpers.SkipParamInit(out spaceId);

        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        ulong resolvedProgramId = ResolveDefaultSaveDataReferenceProgramId(programInfo.ProgramId).Value;
        return GetCacheStorageSpaceId(out spaceId, resolvedProgramId);
    }

    private Result GetCacheStorageSpaceId(out SaveDataSpaceId spaceId, ulong programId)
    {
        UnsafeHelpers.SkipParamInit(out spaceId);
        Result res;

        // Cache storage on the SD card will always take priority over case storage in NAND
        if (_serviceImpl.IsSdCardAccessible())
        {
            res = DoesCacheStorageExist(out bool existsOnSdCard, SaveDataSpaceId.SdUser);
            if (res.IsFailure()) return res.Miss();

            if (existsOnSdCard)
            {
                spaceId = SaveDataSpaceId.SdUser;
                return Result.Success;
            }
        }

        res = DoesCacheStorageExist(out bool existsOnNand, SaveDataSpaceId.User);
        if (res.IsFailure()) return res.Miss();

        if (existsOnNand)
        {
            spaceId = SaveDataSpaceId.User;
            return Result.Success;
        }

        return ResultFs.TargetNotFound.Log();

        Result DoesCacheStorageExist(out bool exists, SaveDataSpaceId saveSpaceId)
        {
            UnsafeHelpers.SkipParamInit(out exists);

            var filter = new SaveDataInfoFilter(saveSpaceId, new ProgramId(programId), SaveDataType.Cache,
                userId: default, saveDataId: default, index: default, (int)SaveDataRank.Primary);

            Result result = FindSaveDataWithFilterImpl(out long count, out _, saveSpaceId, in filter);
            if (result.IsFailure()) return result;

            exists = count != 0;
            return Result.Success;
        }
    }

    private Result FindCacheStorage(out SaveDataInfo saveInfo, out SaveDataSpaceId spaceId, ushort index)
    {
        UnsafeHelpers.SkipParamInit(out saveInfo);

        Result res = GetCacheStorageSpaceId(out spaceId);
        if (res.IsFailure()) return res.Miss();

        res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        ProgramId resolvedProgramId = ResolveDefaultSaveDataReferenceProgramId(programInfo.ProgramId);

        var filter = new SaveDataInfoFilter(ConvertToRealSpaceId(spaceId), resolvedProgramId, SaveDataType.Cache,
            userId: default, saveDataId: default, index, (int)SaveDataRank.Primary);

        res = FindSaveDataWithFilterImpl(out long count, out SaveDataInfo info, spaceId, in filter);
        if (res.IsFailure()) return res.Miss();

        if (count == 0)
            return ResultFs.TargetNotFound.Log();

        saveInfo = info;
        return Result.Success;
    }

    public Result DeleteCacheStorage(ushort index)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.NonGameCard);

        Result res = FindCacheStorage(out SaveDataInfo saveInfo, out SaveDataSpaceId spaceId, index);
        if (res.IsFailure()) return res.Miss();

        res = Hos.Fs.DeleteSaveData(spaceId, saveInfo.SaveDataId);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result GetCacheStorageSize(out long usableDataSize, out long journalSize, ushort index)
    {
        UnsafeHelpers.SkipParamInit(out usableDataSize, out journalSize);

        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.NonGameCard);

        Result res = FindCacheStorage(out SaveDataInfo saveInfo, out SaveDataSpaceId spaceId, index);
        if (res.IsFailure()) return res.Miss();

        using Path saveDataRootPath = _saveDataRootPath.DangerousGetPath();
        res = _serviceImpl.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, spaceId, saveInfo.SaveDataId,
            saveInfo.Type, in saveDataRootPath);
        if (res.IsFailure()) return res.Miss();

        usableDataSize = extraData.DataSize;
        journalSize = extraData.JournalSize;

        return Result.Success;
    }

    public Result OpenSaveDataTransferManager(ref SharedRef<ISaveDataTransferManager> manager)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataTransferManagerVersion2(ref SharedRef<ISaveDataTransferManagerWithDivision> manager)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataTransferManagerForSaveDataRepair(
        ref SharedRef<ISaveDataTransferManagerForSaveDataRepair> manager)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataTransferManagerForRepair(ref SharedRef<ISaveDataTransferManagerForRepair> manager)
    {
        throw new NotImplementedException();
    }

    private Result OpenSaveDataTransferProhibiterCore(ref SharedRef<ISaveDataTransferProhibiter> prohibiter,
        Ncm.ApplicationId applicationId)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataTransferProhibiter(ref SharedRef<ISaveDataTransferProhibiter> prohibiter,
        Ncm.ApplicationId applicationId)
    {
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        Assert.SdkNotNull(_serviceImpl.GetSaveDataTransferCryptoConfiguration());

        res = SaveDataAccessibilityChecker.CheckOpenProhibiter(applicationId, programInfo);
        if (res.IsFailure()) return res.Miss();

        return OpenSaveDataTransferProhibiterCore(ref prohibiter, applicationId).Ret();
    }

    public bool IsProhibited(ref UniqueLock<SdkMutex> outLock, ApplicationId applicationId)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataMover(ref SharedRef<ISaveDataMover> saveMover, SaveDataSpaceId sourceSpaceId,
        SaveDataSpaceId destinationSpaceId, NativeHandle workBufferHandle, ulong workBufferSize)
    {
        throw new NotImplementedException();
    }

    public Result SetSdCardEncryptionSeed(in EncryptionSeed seed)
    {
        return _serviceImpl.SetSdCardEncryptionSeed(in seed).Ret();
    }

    public Result ListAccessibleSaveDataOwnerId(out int readCount, OutBuffer idBuffer, ProgramId programId,
        int startIndex, int bufferIdCount)
    {
        UnsafeHelpers.SkipParamInit(out readCount);

        if (startIndex < 0)
            return ResultFs.InvalidOffset.Log();

        var ids = Span<Ncm.ApplicationId>.Empty;

        if (bufferIdCount > 0)
        {
            ids = MemoryMarshal.Cast<byte, Ncm.ApplicationId>(idBuffer.Buffer);

            if (ids.Length < bufferIdCount)
                return ResultFs.InvalidSize.Log();
        }

        Result res = GetProgramInfo(out ProgramInfo callerProgramInfo);
        if (res.IsFailure()) return res.Miss();

        if (!callerProgramInfo.AccessControl.CanCall(OperationType.ListAccessibleSaveDataOwnerId))
            return ResultFs.PermissionDenied.Log();

        res = GetProgramInfoByProgramId(out ProgramInfo targetProgramInfo, programId.Value);
        if (res.IsFailure()) return res.Miss();

        targetProgramInfo.AccessControl.ListSaveDataOwnedId(out readCount, ids.Slice(0, bufferIdCount), startIndex);
        return Result.Success;
    }

    private ProgramId ResolveDefaultSaveDataReferenceProgramId(ProgramId programId)
    {
        return _serviceImpl.ResolveDefaultSaveDataReferenceProgramId(programId);
    }

    public Result VerifySaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId,
        OutBuffer workBuffer)
    {
        // The caller needs the VerifySaveData permission.
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.VerifySaveData))
            return ResultFs.PermissionDenied.Log();

        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.NonGameCard);

        try
        {
            // Get the information needed to open the file system.
            SaveDataIndexerValue value;
            SaveDataType saveDataType;

            using (var accessor = new UniqueRef<SaveDataIndexerAccessor>())
            {
                res = OpenSaveDataIndexerAccessor(ref accessor.Ref, spaceId);
                if (res.IsFailure()) return res.Miss();

                res = accessor.Get.GetInterface().GetValue(out value, saveDataId);
                if (res.IsFailure()) return res.Miss();

                res = accessor.Get.GetInterface().GetKey(out SaveDataAttribute key, saveDataId);
                if (res.IsFailure()) return res.Miss();

                saveDataType = key.Type;
            }

            // Open the save data file system.
            using var fileSystem = new SharedRef<IFileSystem>();

            using Path saveDataRootPath = _saveDataRootPath.DangerousGetPath();
            res = _serviceImpl.OpenSaveDataFileSystem(ref fileSystem.Ref, value.SpaceId, saveDataId,
                in saveDataRootPath,
                openReadOnly: false, saveDataType, cacheExtraData: true);
            if (res.IsFailure()) return res.Miss();

            // Verify the file system.
            res = Utility.VerifyDirectoryRecursively(fileSystem.Get, workBuffer.Buffer);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }
        finally
        {
            // Make sure we don't leak any invalid data.
            workBuffer.Buffer.Clear();
        }
    }

    public Result CorruptSaveDataFileSystemByOffset(SaveDataSpaceId spaceId, ulong saveDataId, long offset)
    {
        throw new NotImplementedException();
    }

    public Result CleanUpSaveData()
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.Bis);
        using var accessor = new UniqueRef<SaveDataIndexerAccessor>();

        Result res = OpenSaveDataIndexerAccessor(ref accessor.Ref, SaveDataSpaceId.System);
        if (res.IsFailure()) return res.Miss();

        return CleanUpSaveData(accessor.Get).Ret();
    }

    private Result CleanUpSaveData(SaveDataIndexerAccessor accessor)
    {
        // Todo: Implement
        return Result.Success;
    }

    public Result CompleteSaveDataExtension()
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.Bis);
        using var accessor = new UniqueRef<SaveDataIndexerAccessor>();

        Result res = OpenSaveDataIndexerAccessor(ref accessor.Ref, SaveDataSpaceId.System);
        if (res.IsFailure()) return res.Miss();

        return CompleteSaveDataExtension(accessor.Get).Ret();
    }

    private Result CompleteSaveDataExtension(SaveDataIndexerAccessor accessor)
    {
        // Todo: Implement
        return Result.Success;
    }

    public Result CleanUpTemporaryStorage()
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.Bis);
        using var fileSystem = new SharedRef<IFileSystem>();

        Result res = _serviceImpl.OpenSaveDataDirectoryFileSystem(ref fileSystem.Ref, SaveDataSpaceId.Temporary);
        if (res.IsFailure()) return res.Miss();

        using var pathRoot = new Path();
        res = PathFunctions.SetUpFixedPath(ref pathRoot.Ref(), "/"u8);
        if (res.IsFailure()) return res.Miss();

        res = fileSystem.Get.CleanDirectoryRecursively(in pathRoot);
        if (res.IsFailure()) return res.Miss();

        _serviceImpl.ResetTemporaryStorageIndexer();
        return Result.Success;
    }

    public Result FixSaveData()
    {
        // Todo: Implement
        return Result.Success;
    }

    public Result OpenMultiCommitManager(ref SharedRef<IMultiCommitManager> outCommitManager)
    {
        using SharedRef<ISaveDataMultiCommitCoreInterface> commitInterface = GetSharedMultiCommitInterfaceFromThis();

        outCommitManager.Reset(new MultiCommitManager(_serviceImpl.FsServer, ref commitInterface.Ref));

        return Result.Success;
    }

    public Result OpenMultiCommitContext(ref SharedRef<IFileSystem> contextFileSystem)
    {
        Result res = SaveDataAttribute.Make(out SaveDataAttribute attribute, new ProgramId(MultiCommitManager.ProgramId),
            SaveDataType.System, InvalidUserId, MultiCommitManager.SaveDataId, index: 0);
        if (res.IsFailure()) return res.Miss();

        using var fileSystem = new SharedRef<IFileSystem>();

        res = OpenSaveDataFileSystemCore(ref fileSystem.Ref, out _, SaveDataSpaceId.System, in attribute,
            openReadOnly: false, cacheExtraData: true);
        if (res.IsFailure()) return res.Miss();

        contextFileSystem.SetByMove(ref fileSystem.Ref);
        return Result.Success;
    }

    public Result RecoverMultiCommit()
    {
        return MultiCommitManager.Recover(_serviceImpl.FsServer, this, _serviceImpl).Ret();
    }

    public Result IsProvisionallyCommittedSaveData(out bool isProvisionallyCommitted, in SaveDataInfo saveInfo)
    {
        return _serviceImpl.IsProvisionallyCommittedSaveData(out isProvisionallyCommitted, in saveInfo).Ret();
    }

    public Result RecoverProvisionallyCommittedSaveData(in SaveDataInfo saveInfo, bool doRollback)
    {
        Result res = SaveDataAttribute.Make(out SaveDataAttribute attribute, saveInfo.ProgramId, saveInfo.Type,
            saveInfo.UserId, saveInfo.SaveDataId, saveInfo.Index);
        if (res.IsFailure()) return res.Miss();

        using var fileSystem = new SharedRef<IFileSystem>();

        res = OpenSaveDataFileSystemCore(ref fileSystem.Ref, out _, saveInfo.SpaceId, in attribute,
            openReadOnly: false, cacheExtraData: false);
        if (res.IsFailure()) return res.Miss();

        if (!doRollback)
        {
            res = fileSystem.Get.Commit();
            if (res.IsFailure()) return res.Miss();
        }
        else
        {
            res = fileSystem.Get.Rollback();
            if (res.IsFailure()) return res.Miss();
        }

        return Result.Success;
    }

    private Result TryAcquireSaveDataEntryOpenCountSemaphore(ref UniqueRef<IUniqueLock> outSemaphoreLock)
    {
        using SharedRef<SaveDataFileSystemService> saveService = GetSharedFromThis();

        Result res = Utility.MakeUniqueLockWithPin(ref outSemaphoreLock, _openEntryCountSemaphore,
            ref saveService.Ref);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    private Result TryAcquireSaveDataMountCountSemaphore(ref UniqueRef<IUniqueLock> outSemaphoreLock)
    {
        using SharedRef<SaveDataFileSystemService> saveService = GetSharedFromThis();

        Result res = Utility.MakeUniqueLockWithPin(ref outSemaphoreLock, _saveDataMountCountSemaphore,
            ref saveService.Ref);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result OverrideSaveDataTransferTokenSignVerificationKey(InBuffer key)
    {
        throw new NotImplementedException();
    }

    public Result SetSdCardAccessibility(bool isAccessible)
    {
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.SetSdCardAccessibility))
            return ResultFs.PermissionDenied.Log();

        _serviceImpl.SetSdCardAccessibility(isAccessible);
        return Result.Success;
    }

    public Result IsSdCardAccessible(out bool isAccessible)
    {
        isAccessible = _serviceImpl.IsSdCardAccessible();
        return Result.Success;
    }

    private Result OpenSaveDataIndexerAccessor(ref UniqueRef<SaveDataIndexerAccessor> outAccessor,
        SaveDataSpaceId spaceId)
    {
        using var accessor = new UniqueRef<SaveDataIndexerAccessor>();
        Result res = _serviceImpl.OpenSaveDataIndexerAccessor(ref accessor.Ref, out bool isInitialOpen, spaceId);
        if (res.IsFailure()) return res.Miss();

        if (isInitialOpen)
        {
            CleanUpSaveData(accessor.Get).IgnoreResult();
            CompleteSaveDataExtension(accessor.Get).IgnoreResult();
        }

        outAccessor.Set(ref accessor.Ref);
        return Result.Success;
    }

    Result ISaveDataTransferCoreInterface.CreateSaveDataFileSystemCore(in SaveDataAttribute attribute,
        in SaveDataCreationInfo creationInfo, in SaveDataMetaInfo metaInfo, in Optional<HashSalt> hashSalt,
        bool leaveUnfinalized)
    {
        return CreateSaveDataFileSystemCore(in attribute, in creationInfo, in metaInfo, in hashSalt,
            leaveUnfinalized);
    }

    Result ISaveDataTransferCoreInterface.ReadSaveDataFileSystemExtraDataCore(out SaveDataExtraData extraData,
        SaveDataSpaceId spaceId, ulong saveDataId, bool isTemporarySaveData)
    {
        return ReadSaveDataFileSystemExtraDataCore(out extraData, spaceId, saveDataId, isTemporarySaveData);
    }

    Result ISaveDataTransferCoreInterface.WriteSaveDataFileSystemExtraDataCore(SaveDataSpaceId spaceId,
        ulong saveDataId, in SaveDataExtraData extraData, SaveDataType type, bool updateTimeStamp)
    {
        return WriteSaveDataFileSystemExtraDataCore(spaceId, saveDataId, in extraData, type, updateTimeStamp);
    }

    Result ISaveDataTransferCoreInterface.OpenSaveDataInternalStorageFileSystemCore(
        ref SharedRef<IFileSystem> fileSystem, SaveDataSpaceId spaceId, ulong saveDataId, bool useSecondMacKey)
    {
        return OpenSaveDataInternalStorageFileSystemCore(ref fileSystem, spaceId, saveDataId, useSecondMacKey);
    }

    Result ISaveDataTransferCoreInterface.OpenSaveDataMetaFileRaw(ref SharedRef<IFile> outFile, SaveDataSpaceId spaceId,
        ulong saveDataId, SaveDataMetaType metaType, OpenMode mode)
    {
        return OpenSaveDataMetaFileRaw(ref outFile, spaceId, saveDataId, metaType, mode);
    }

    Result ISaveDataTransferCoreInterface.OpenSaveDataIndexerAccessor(
        ref UniqueRef<SaveDataIndexerAccessor> outAccessor, SaveDataSpaceId spaceId)
    {
        return OpenSaveDataIndexerAccessor(ref outAccessor, spaceId);
    }
}