using System;
using LibHac.Common;
using LibHac.Crypto;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Os;

namespace LibHac.FsSrv.Impl;

public class DeepRetryStorage : IStorage
{
    private AsynchronousAccessStorage _asyncStorage;
    private SharedRef<IRomFileSystemAccessFailureManager> _parent;
    private UniqueRef<IUniqueLock> _mountCountLock;
    private DataStorageContext _dataStorageContext;
    private bool _deepRetryEnabled;
    private ReaderWriterLock _readWriteLock;
    
    // LibHac addition
    private FileSystemServer _fsServer;

    private struct DataStorageContext
    {
        private Hash _digest;
        private ulong _programId;
        private StorageId _storageId;
        private bool _isValid;

        public DataStorageContext()
        {
            _digest = default;
            _programId = 0;
            _storageId = StorageId.None;
            _isValid = false;
        }

        public DataStorageContext(in Hash digest, ulong programId, StorageId storageId)
        {
            _digest = digest;
            _programId = programId;
            _storageId = storageId;
            _isValid = true;
        }

        public readonly bool IsValid() => _isValid;
        public readonly Hash GetDigest() => _digest;
        public readonly ulong GetProgramIdValue() => _programId;
        public readonly StorageId GetStorageId() => _storageId;
    }

    public DeepRetryStorage(ref readonly SharedRef<IStorage> baseStorage,
        ref readonly SharedRef<IRomFileSystemAccessFailureManager> parent,
        ref UniqueRef<IUniqueLock> mountCountSemaphore,
        bool deepRetryEnabled, FileSystemServer fsServer)
    {
        // Missing: Getting the thread pool via GetRegisteredThreadPool()
        _asyncStorage = new AsynchronousAccessStorage(in baseStorage);
        _parent = SharedRef<IRomFileSystemAccessFailureManager>.CreateCopy(in parent);
        _mountCountLock = UniqueRef<IUniqueLock>.Create(ref mountCountSemaphore);
        _dataStorageContext = new DataStorageContext();
        _deepRetryEnabled = deepRetryEnabled;
        _readWriteLock = new ReaderWriterLock(fsServer.Hos.Os);

        _fsServer = fsServer;
    }

    public DeepRetryStorage(ref readonly SharedRef<IStorage> baseStorage,
        ref readonly SharedRef<IRomFileSystemAccessFailureManager> parent,
        ref UniqueRef<IUniqueLock> mountCountSemaphore,
        in Hash hash, ulong programId, StorageId storageId, FileSystemServer fsServer)
    {
        // Missing: Getting the thread pool via GetRegisteredThreadPool()
        _asyncStorage = new AsynchronousAccessStorage(in baseStorage);
        _parent = SharedRef<IRomFileSystemAccessFailureManager>.CreateCopy(in parent);
        _mountCountLock = UniqueRef<IUniqueLock>.Create(ref mountCountSemaphore);
        _dataStorageContext = new DataStorageContext(in hash, programId, storageId);
        _deepRetryEnabled = true;
        _readWriteLock = new ReaderWriterLock(fsServer.Hos.Os);

        _fsServer = fsServer;
    }

    public override void Dispose()
    {
        _readWriteLock.Dispose();
        _mountCountLock.Destroy();
        _parent.Destroy();
        _asyncStorage.Dispose();

        base.Dispose();
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        throw new NotImplementedException();
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        throw new NotImplementedException();
    }

    public override Result Flush()
    {
        throw new NotImplementedException();
    }

    public override Result SetSize(long size)
    {
        throw new NotImplementedException();
    }

    public override Result GetSize(out long size)
    {
        throw new NotImplementedException();
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }

    private Result InvalidateCacheOnStorage(bool remount)
    {
        Abort.DoAbortUnless(_deepRetryEnabled);

        using var scopedWriterLock = new UniqueLock<ReaderWriterLock>(_readWriteLock);

        if (!remount || !_dataStorageContext.IsValid())
        {
            _asyncStorage.OperateRange(default, OperationId.InvalidateCache, 0, long.MaxValue, default).IgnoreResult();
            return Result.Success;
        }

        Assert.SdkNotNull(_parent);

        using var remountStorage = new SharedRef<IStorage>();

        const int maxRetryCount = 2;

        Result retryResult = Result.Success;
        Hash digest = default;
        for (int i = 0; i < maxRetryCount; i++)
        {
            retryResult = _parent.Get.OpenDataStorageCore(ref remountStorage.Ref, ref digest,
                _dataStorageContext.GetProgramIdValue(), _dataStorageContext.GetStorageId());

            if (!ResultFs.DataCorrupted.Includes(retryResult))
                break;
            
            // Todo: Log
            // _fsServer.Hos.Diag.Impl.LogImpl()
        }

        if (retryResult.IsFailure()) return retryResult.Miss();
        
        // Compare the digest of the remounted NCA header to the one we originally mounted
        Hash originalDigest = _dataStorageContext.GetDigest();
        if (!CryptoUtil.IsSameBytes(originalDigest.Value, digest.Value, digest.Value.Length))
        {
            return ResultFs.NcaDigestInconsistent.Log();
        }
        
        _asyncStorage.SetBaseStorage(in remountStorage);

        return Result.Success;
    }

    private void AcquireReaderLockForCacheInvalidation(ref SharedLock<ReaderWriterLock> outReaderLock)
    {
        outReaderLock.Reset(_readWriteLock);
    }
}