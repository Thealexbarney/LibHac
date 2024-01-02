// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
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
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public uint GetBlockCount() => throw new NotImplementedException();
    public ref readonly ValueSubStorage GetOriginalStorage() => throw new NotImplementedException();
    public ref readonly ValueSubStorage GetUpdateStorage() => throw new NotImplementedException();

    public static Result Format(uint size, SubStorage storage, SubStorage storageOriginal)
    {
        throw new NotImplementedException();
    }

    public static Result Expand(uint bitCountOld, uint bitCountNew, in ValueSubStorage storage, in ValueSubStorage storageOriginal)
    {
        throw new NotImplementedException();
    }

    public void Initialize(uint blockCount, in ValueSubStorage storage1, in ValueSubStorage storage2)
    {
        throw new NotImplementedException();
    }

    public void InitializeForRead(uint blockCount, in ValueSubStorage storage1, in ValueSubStorage storage2)
    {
        throw new NotImplementedException();
    }

    public void RemountForWrite()
    {
        throw new NotImplementedException();
    }

    private void SwapDuplexBitmapForHierarchicalDuplexStorage(ref DuplexBitmapHolder outBitmap)
    {
        throw new NotImplementedException();
    }
}