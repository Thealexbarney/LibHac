// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common.FixedArrays;
using LibHac.Fs;
using LibHac.Os;

namespace LibHac.FsSystem;

file static class Anonymous
{
    public static bool IsFutureVersion(uint version)
    {
        throw new NotImplementedException();
    }
}

public struct HierarchicalDuplexInformation
{
    public struct Entry
    {
        public Fs.Int64 Offset;
        public Fs.Int64 Size;
        public int OrderBlock;
    }

    public Array3<Entry> Info;
}

public struct HierarchicalDuplexMetaInformation
{
    public uint Magic;
    public uint Version;
    public HierarchicalDuplexInformation InfoDuplex;
}

public struct HierarchicalDuplexSizeSet
{
    public Array3<long> Sizes;
}

public class HierarchicalDuplexStorageControlArea : IDisposable
{
    private ValueSubStorage _storage;
    private HierarchicalDuplexMetaInformation _meta;

    public HierarchicalDuplexStorageControlArea()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    private static Result CreateMetaInformation(ref HierarchicalDuplexMetaInformation meta, long blockSize, long sizeData)
    {
        throw new NotImplementedException();
    }

    public static Result QuerySize(ref HierarchicalDuplexSizeSet outSizeSet, long blockSize, long sizeData)
    {
        throw new NotImplementedException();
    }

    public static Result Format(ref ValueSubStorage storageMeta, long blockSize, long sizeData)
    {
        throw new NotImplementedException();
    }

    public static Result Expand(ref ValueSubStorage storageMeta, long blockSize, long sizeData)
    {
        throw new NotImplementedException();
    }

    public ref readonly HierarchicalDuplexMetaInformation GetMeta() => throw new NotImplementedException();

    public Result Initialize(in ValueSubStorage storage)
    {
        throw new NotImplementedException();
    }

    public Result FinalizeObject()
    {
        throw new NotImplementedException();
    }
}

public class HierarchicalDuplexStorage : IStorage
{
    private DuplexStorage _duplexStorageMaster;
    private DuplexStorage _duplexStorageL1;
    private DuplexStorage _duplexStorageData;

    private DuplexBitmapHolder _masterBitmapHolderWrite;
    private DuplexBitmapHolder _masterBitmapHolderRead;
    private DuplexBitmapHolder _bitmapHolder3;
    private DuplexBitmapHolder _bitmapHolderL1;

    private ValueSubStorage _trimmedStorageL1A;
    private ValueSubStorage _trimmedStorageL1B;
    private ValueSubStorage _trimmedStorageDataA;
    private ValueSubStorage _trimmedStorageDataB;

    private TruncatedSubStorage[] _storages;
    private BlockCacheBufferedStorage[] _bufferedStorages;

    private long _sizeData;
    private IBufferManager _bufferManager;
    private SdkRecursiveMutex _mutex;
    private bool _isWritten;

    public HierarchicalDuplexStorage()
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    public static Result Format(
        in HierarchicalDuplexInformation infoDuplex,
        in ValueSubStorage storageMasterA,
        in ValueSubStorage storageMasterB,
        in ValueSubStorage storageL1A,
        in ValueSubStorage storageL1B,
        IBufferManager bufferManager)
    {
        throw new NotImplementedException();
    }

    public static Result Expand(
        in HierarchicalDuplexInformation infoDuplexOld,
        in HierarchicalDuplexInformation infoDuplexNew,
        in ValueSubStorage storageMasterA,
        in ValueSubStorage storageMasterB,
        in ValueSubStorage storageL1A,
        in ValueSubStorage storageL1B,
        IBufferManager bufferManager)
    {
        throw new NotImplementedException();
    }

    public Result Initialize(in HierarchicalDuplexInformation infoDuplex,
        in ValueSubStorage storageMasterA,
        in ValueSubStorage storageMasterB,
        in ValueSubStorage storageL1A,
        in ValueSubStorage storageL1B,
        in ValueSubStorage storageDataA,
        in ValueSubStorage storageDataB,
        bool isStorageAOriginal,
        IBufferManager bufferManager,
        SdkRecursiveMutex mutex)
    {
        throw new NotImplementedException();
    }

    public void FinalizeObject()
    {
        throw new NotImplementedException();
    }

    public void ClearCommitFlag()
    {
        throw new NotImplementedException();
    }

    public Result SwapDuplexBitmap()
    {
        throw new NotImplementedException();
    }

    public bool NeedCommit()
    {
        throw new NotImplementedException();
    }

    public override Result GetSize(out long size)
    {
        throw new NotImplementedException();
    }

    public override Result SetSize(long size)
    {
        throw new NotImplementedException();
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

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }

    public void Remount()
    {
        throw new NotImplementedException();
    }

    private static Result CopyStorage(in ValueSubStorage destStorage, in ValueSubStorage sourceStorage, long size)
    {
        throw new NotImplementedException();
    }
}