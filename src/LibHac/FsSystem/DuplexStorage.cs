// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common.FixedArrays;
using LibHac.Fs;

namespace LibHac.FsSystem;

public class DuplexStorage : IStorage
{
    private DuplexBitmap _duplexBitmap;
    private Array2<ValueSubStorage> _storages;
    private long _countShiftBlock;
    private long _sizeBlock;
    private IBufferManager _bufferManager;
    private bool _isReadOnly;

    public DuplexStorage()
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    public long GetBlockSize()
    {
        throw new NotImplementedException();
    }

    public void SetReadOnly(bool isReadOnly)
    {
        throw new NotImplementedException();
    }

    public void Initialize(DuplexBitmap bitmap, in ValueSubStorage storageData1, in ValueSubStorage storageData2,
        long sizeBlock, IBufferManager buffer)
    {
        throw new NotImplementedException();
    }

    public void FinalizeObject()
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

    public override Result SetSize(long size)
    {
        throw new NotImplementedException();
    }

    public override Result GetSize(out long size)
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

    private Result ReadDuplexBits(out uint outOriginalBits, out uint outModifiedBits, long offset, ulong bitCount)
    {
        throw new NotImplementedException();
    }

    private Result UpdateModifiedBits(long offset, long offsetEnd)
    {
        throw new NotImplementedException();
    }
}