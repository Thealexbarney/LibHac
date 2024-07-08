// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Util;

namespace LibHac.FsSrv.Impl;

public struct IntegrityParam
{
    public HashAlgorithmType HashAlgorithmType;
    public bool IsIntegritySeedEnabled;
    public HashSalt IntegritySeed;
}

public static partial class SaveDataTransferUtilityGlobalMethods
{
    internal static int GetIndexById(ushort id)
    {
        throw new NotImplementedException();
    }

    internal static SaveDataCreationInfo2 InheritSaveDataCreationInfo2(in InitialDataVersion2Detail.Content initialData)
    {
        throw new NotImplementedException();
    }
}

public class StorageDuplicator : IDisposable
{
    private SharedRef<IStorage> _sourceFileStorage;
    private SharedRef<IStorage> _destinationFileStorage;
    private SharedRef<IFileSystem> _destinationFileSystem;
    private long _offset;
    private long _remainingSize;

    public StorageDuplicator(ref readonly SharedRef<IStorage> sourceFileStorage,
        ref readonly SharedRef<IStorage> destinationFileStorage,
        ref readonly SharedRef<IFileSystem> destinationFileSystem)
    {
        _sourceFileStorage = SharedRef<IStorage>.CreateCopy(in sourceFileStorage);
        _destinationFileStorage = SharedRef<IStorage>.CreateCopy(in destinationFileStorage);
        _destinationFileSystem = SharedRef<IFileSystem>.CreateCopy(in destinationFileSystem);

        _offset = 0;
        _remainingSize = -1;
    }

    public void Dispose()
    {
        _sourceFileStorage.Destroy();
        _destinationFileStorage.Destroy();
        _destinationFileSystem.Destroy();
    }

    public Result Initialize()
    {
        Result res = _sourceFileStorage.Get.GetSize(out long sourceSize);
        if (res.IsFailure()) return res.Miss();

        res = _destinationFileStorage.Get.GetSize(out long destinationSize);
        if (res.IsFailure()) return res.Miss();

        if (sourceSize != destinationSize)
            return ResultFs.SaveDataPorterSaveDataModified.Log();

        _remainingSize = destinationSize;
        return Result.Success;
    }

    public Result FinalizeObject()
    {
        Result res = _destinationFileStorage.Get.Flush();
        if (res.IsFailure()) return res.Miss();

        res = _destinationFileSystem.Get.Commit();
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result ProcessDuplication(out long outRemainingSize, Span<byte> workBuffer, long sizeToProcess)
    {
        UnsafeHelpers.SkipParamInit(out outRemainingSize);
        Assert.SdkGreaterEqual(_remainingSize, 0);

        long remainingSizeToProcess = Math.Min(sizeToProcess, _remainingSize);

        while (remainingSizeToProcess > 0)
        {
            int processSize = (int)Math.Min(workBuffer.Length, Math.Min(sizeToProcess, remainingSizeToProcess));
            Span<byte> buffer = workBuffer.Slice(0, processSize);

            Result res = _sourceFileStorage.Get.Read(_offset, buffer);
            if (res.IsFailure()) return res.Miss();

            res = _destinationFileStorage.Get.Write(_offset, buffer);
            if (res.IsFailure()) return res.Miss();

            _offset += processSize;
            remainingSizeToProcess -= processSize;
            _remainingSize -= processSize;
        }

        outRemainingSize = _remainingSize;
        return Result.Success;
    }

    public Result ProcessDuplication(out long outRemainingSize, long sizeToProcess)
    {
        const int bufferSize = 0x8000;

        UnsafeHelpers.SkipParamInit(out outRemainingSize);
        Assert.SdkGreaterEqual(_remainingSize, 0);

        using var buffer = new PooledBuffer();
        Result res = buffer.Allocate(bufferSize, bufferSize);
        if (res.IsFailure()) return res.Miss();

        return ProcessDuplication(out outRemainingSize, buffer.GetBuffer(), sizeToProcess).Ret();
    }
}

public class SaveDataInternalStorageAccessor : IDisposable
{
    public class InternalFile : InternalFileWithDigest
    {
        public InternalFile(ref readonly SharedRef<IFile> file) : base(in file, in file, blockSize: 1, digestSize: 1)
        {
            throw new NotImplementedException();
        }
    }

    public class InternalFileWithDigest : IDisposable
    {
        private SharedRef<IFile> _file;
        private SharedRef<IFile> _digestFile;
        private long _blockSize;
        private long _digestSize;

        public InternalFileWithDigest(ref readonly SharedRef<IFile> file, ref readonly SharedRef<IFile> digestFile,
            long blockSize, long digestSize)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public long GetBlockSize()
        {
            throw new NotImplementedException();
        }

        public SharedRef<IFile> GetFile()
        {
            throw new NotImplementedException();
        }

        public SharedRef<IFile> GetDigestFile()
        {
            throw new NotImplementedException();
        }

        public long GetFileSize()
        {
            throw new NotImplementedException();
        }

        public long GetDigestFileSize()
        {
            throw new NotImplementedException();
        }

        public long GetDigestSize()
        {
            throw new NotImplementedException();
        }
    }

    public class PaddingFile : IFile
    {
        private long _paddingSize;

        public PaddingFile(long size)
        {
            throw new NotImplementedException();
        }

        protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination, in ReadOption option)
        {
            throw new NotImplementedException();
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
        {
            throw new NotImplementedException();
        }

        protected override Result DoFlush()
        {
            throw new NotImplementedException();
        }

        protected override Result DoSetSize(long size)
        {
            throw new NotImplementedException();
        }

        protected override Result DoGetSize(out long size)
        {
            throw new NotImplementedException();
        }

        protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
        {
            throw new NotImplementedException();
        }
    }

    private SaveDataSpaceId _spaceId;
    private ulong _saveDataId;
    private Array5<SharedRef<InternalFileWithDigest>> _internalFileWithDigestArray;
    private SharedRef<IFileSystem> _internalStorageFs;
    private SharedRef<ProxyStorage> _concatenationStorage;
    private SharedRef<ProxyStorage> _concatenationDigestStorage;
    private bool _isValid;

    public SaveDataInternalStorageAccessor(SaveDataSpaceId spaceId, ulong saveDataId)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result InitializeInternalFs(ISaveDataTransferCoreInterface coreInterface, bool isTemporaryTransferSave)
    {
        throw new NotImplementedException();
    }

    public Result Initialize(ISaveDataTransferCoreInterface coreInterface, bool isTemporaryTransferSave, in Optional<HashSalt> hashSalt)
    {
        throw new NotImplementedException();
    }

    public Result InitializeForTest(in Array5<SharedRef<InternalFileWithDigest>> files)
    {
        throw new NotImplementedException();
    }

    public bool IsValid()
    {
        throw new NotImplementedException();
    }

    public Result Invalidate()
    {
        throw new NotImplementedException();
    }

    public SharedRef<IFileSystem> GetSaveDataInternalFileSystem()
    {
        throw new NotImplementedException();
    }

    public Result CommitSaveDataInternalFileSystem(bool isTemporaryTransferSave)
    {
        throw new NotImplementedException();
    }

    public Result OpenConcatenationStorage(ref SharedRef<IStorage> outStorage)
    {
        throw new NotImplementedException();
    }

    public Result CalculateThumbnailHash(ref InitialDataVersion2Detail.Hash outHash)
    {
        throw new NotImplementedException();
    }

    public Result OpenConcatenationSubStorage(ref SharedRef<IStorage> outStorage, long offset, long size)
    {
        throw new NotImplementedException();
    }

    public Result OpenConcatenationDigestStorage(ref SharedRef<IStorage> outStorage)
    {
        throw new NotImplementedException();
    }

    public Result OpenConcatenationDigestSubStorage(ref SharedRef<IStorage> outStorage, long offset, long size)
    {
        throw new NotImplementedException();
    }

    public Result GetIntegrityParam(ref IntegrityParam outParam)
    {
        throw new NotImplementedException();
    }

    public Result ReadSaveDataExtraData(ref SaveDataExtraData outExtraData)
    {
        throw new NotImplementedException();
    }

    public Result WriteSaveDataExtraData(in SaveDataExtraData extraData)
    {
        throw new NotImplementedException();
    }

    public Result ConvertToDigestOffsetAndSize(out long outDigestOffset, out long outDigestSize, long offset, long size)
    {
        throw new NotImplementedException();
    }
}

public static partial class SaveDataTransferUtilityGlobalMethods
{
    public static Result OpenSaveDataInternalStorageAccessor(this FileSystemServer fsSrv,
        ref SharedRef<SaveDataInternalStorageAccessor> outAccessor, SaveDataSpaceId spaceId, ulong saveDataId)
    {
        throw new NotImplementedException();
    }

    public static Result CommitConcatenatedSaveDataStorage(this FileSystemServer fsSrv, IFileSystem fileSystem,
        bool isTemporaryTransferSave)
    {
        throw new NotImplementedException();
    }

    public static Result CalculateFileHash(this FileSystemServer fsSrv, ref InitialDataVersion2Detail.Hash outHash,
        ref readonly SharedRef<IFile> file)
    {
        throw new NotImplementedException();
    }

    public static Result CheckThumbnailUpdate(this FileSystemServer fsSrv,
        in InitialDataVersion2Detail.Hash expectedHash, in InitialDataVersion2Detail.Hash hash)
    {
        throw new NotImplementedException();
    }
}