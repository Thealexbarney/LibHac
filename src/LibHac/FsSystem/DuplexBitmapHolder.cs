using System;
using LibHac.Diag;
using LibHac.Fs;

namespace LibHac.FsSystem;

public class DuplexBitmapHolder : IDisposable
{
    private DuplexBitmap _bitmap;
    private ValueSubStorage _updateStorage;
    private ValueSubStorage _originalStorage;
    private uint _blockCount;

    public DuplexBitmapHolder()
    {
        _bitmap = new DuplexBitmap();
        _updateStorage = new ValueSubStorage();
        _originalStorage = new ValueSubStorage();
    }

    public void Dispose()
    {
        _originalStorage.Dispose();
        _updateStorage.Dispose();
        _bitmap.Dispose();
    }

    public uint GetBlockCount() => _blockCount;
    public ref readonly ValueSubStorage GetOriginalStorage() => ref _originalStorage;
    public ref readonly ValueSubStorage GetUpdateStorage() => ref _updateStorage;

    public static Result Format(uint size, SubStorage storage, SubStorage storageOriginal)
    {
        long bitmapSize = DuplexBitmap.QuerySize(size);

        using var subStorage = new ValueSubStorage(storage, 0, bitmapSize);
        using var subStorageOriginal = new ValueSubStorage(storageOriginal, 0, bitmapSize);

        Result res = DuplexBitmap.Format(size, in subStorage, in subStorageOriginal);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result Expand(uint bitCountOld, uint bitCountNew, in ValueSubStorage storage, in ValueSubStorage storageOriginal)
    {
        long bitmapSize = DuplexBitmap.QuerySize(bitCountNew);

        using var subStorage = new ValueSubStorage(in storage, 0, bitmapSize);
        using var subStorageOriginal = new ValueSubStorage(in storageOriginal, 0, bitmapSize);

        Result res = DuplexBitmap.Expand(bitCountOld, bitCountNew, in subStorage, in subStorageOriginal);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public void Initialize(uint blockCount, in ValueSubStorage storage1, in ValueSubStorage storage2)
    {
        long size = DuplexBitmap.QuerySize(blockCount);
        _blockCount = blockCount;

        _updateStorage.Set(in storage1, offset: 0, size);
        _originalStorage.Set(in storage2, offset: 0, size);

        _bitmap.Initialize(blockCount, in _updateStorage, in _originalStorage);
    }

    public void InitializeForRead(uint blockCount, in ValueSubStorage storage1, in ValueSubStorage storage2)
    {
        long size = DuplexBitmap.QuerySize(blockCount);
        _blockCount = blockCount;

        _updateStorage.Set(in storage1, offset: 0, size);
        _originalStorage.Set(in storage2, offset: 0, size);

        _bitmap.Initialize(blockCount, in _originalStorage, in _originalStorage);
    }

    public void RemountForWrite()
    {
        _bitmap.FinalizeObject();
        _bitmap.Initialize(_blockCount, in _updateStorage, in _originalStorage);
    }

    private void SwapDuplexBitmapForHierarchicalDuplexStorage(ref DuplexBitmapHolder outBitmap)
    {
        uint blockCount = GetBlockCount();

        Assert.SdkAssert(GetBlockCount() == outBitmap.GetBlockCount());

        using var storageUpdate = new ValueSubStorage(in outBitmap.GetUpdateStorage());
        using var storageOriginal = new ValueSubStorage(in outBitmap.GetOriginalStorage());

        outBitmap._bitmap.FinalizeObject();
        outBitmap.InitializeForRead(blockCount, in storageOriginal, in storageUpdate);

        _bitmap.FinalizeObject();
        Initialize(blockCount, in storageUpdate, in storageUpdate);
    }
}