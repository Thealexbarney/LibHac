using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSrv;
using LibHac.Os;
using static LibHac.FsSystem.HierarchicalIntegrityVerificationStorage;

namespace LibHac.FsSystem;

/// <summary>
/// An <see cref="IStorage"/> that handles initializing a <see cref="HierarchicalIntegrityVerificationStorage"/>
/// from a RomFs.
/// </summary>
/// <remarks>Based on FS 14.1.0 (nnSdk 14.3.0)</remarks>
public class IntegrityRomFsStorage : IStorage
{
    private HierarchicalIntegrityVerificationStorage _integrityStorage;
    private FileSystemBufferManagerSet _bufferManagerSet;
    private SdkRecursiveMutex _mutex;
    private byte[] _masterHash;
    private UniqueRef<MemoryStorage> _masterHashStorage;

    public IntegrityRomFsStorage(FileSystemServer fsServer)
    {
        _integrityStorage = new HierarchicalIntegrityVerificationStorage(fsServer);
        _mutex = new SdkRecursiveMutex();

        _bufferManagerSet = new FileSystemBufferManagerSet();
    }

    public override void Dispose()
    {
        FinalizeObject();

        _masterHashStorage.Destroy();
        _integrityStorage.Dispose();

        base.Dispose();
    }

    public Result Initialize(ref HierarchicalIntegrityVerificationInformation info, Hash masterHash,
        ref HierarchicalStorageInformation storageInfo, IBufferManager bufferManager, int maxDataCacheEntries,
        int maxHashCacheEntries, sbyte bufferLevel, IHash256GeneratorFactory hashGeneratorFactory)
    {
        // Validate preconditions.
        Assert.SdkRequiresNotNull(bufferManager);

        // Set master hash.
        _masterHash = SpanHelpers.AsReadOnlyByteSpan(in masterHash).ToArray();
        _masterHashStorage.Reset(new MemoryStorage(_masterHash));
        if (!_masterHashStorage.HasValue)
            return ResultFs.AllocationMemoryFailedInIntegrityRomFsStorageA.Log();

        // Set the master hash storage.
        using var masterHashStorage = new ValueSubStorage(_masterHashStorage.Get, 0, Unsafe.SizeOf<Hash>());
        storageInfo[(int)HierarchicalStorageInformation.Storage.MasterStorage].Set(in masterHashStorage);

        // Set buffers.
        for (int i = 0; i < _bufferManagerSet.Buffers.Length; i++)
        {
            _bufferManagerSet.Buffers[i] = bufferManager;
        }

        // Initialize our integrity storage.
        Result rc = _integrityStorage.Initialize(in info, ref storageInfo, _bufferManagerSet, hashGeneratorFactory,
            false, _mutex, maxDataCacheEntries, maxHashCacheEntries, bufferLevel, false, false);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public void FinalizeObject()
    {
        _integrityStorage.FinalizeObject();
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        return _integrityStorage.Read(offset, destination);
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        return _integrityStorage.Write(offset, source);
    }

    public override Result Flush()
    {
        return _integrityStorage.Flush();
    }

    public override Result SetSize(long size)
    {
        return ResultFs.UnsupportedSetSizeForIntegrityRomFsStorage.Log();
    }

    public override Result GetSize(out long size)
    {
        return _integrityStorage.GetSize(out size);
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        return _integrityStorage.OperateRange(outBuffer, operationId, offset, size, inBuffer);
    }
}