// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Fs;

namespace LibHac.FsSystem;

public class DuplexBitmap : IDisposable
{
    public struct Iterator
    {
        public uint Index;
        public uint IndexEnd;
    }

    public static long QuerySize(uint bitCount)
    {
        throw new NotImplementedException();
    }

    public static Result Format(uint size, in ValueSubStorage storage, in ValueSubStorage storageOriginal)
    {
        throw new NotImplementedException();
    }

    public static Result Expand(uint bitCountOld, uint bitCountNew, in ValueSubStorage storage, in ValueSubStorage storageOriginal)
    {
        throw new NotImplementedException();
    }

    public DuplexBitmap()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public void Initialize(uint bitCount, in ValueSubStorage storage, in ValueSubStorage storageOriginal)
    {
        throw new NotImplementedException();
    }

    public void FinalizeObject()
    {
        throw new NotImplementedException();
    }

    public void IterateBegin(out Iterator outIterator, uint index, ulong count)
    {
        throw new NotImplementedException();
    }

    public Result IterateNext(out ulong outCountZero, out ulong outCountOne, ref Iterator iter)
    {
        throw new NotImplementedException();
    }

    public Result IterateOriginalNext(out ulong outCountZero, out ulong outCountOne, ref Iterator iter)
    {
        throw new NotImplementedException();
    }

    public Result ReadBitmap32(out uint outBitmap, uint index)
    {
        throw new NotImplementedException();
    }

    public Result ReadOriginalBitmap32(out uint outBitmap, uint index)
    {
        throw new NotImplementedException();
    }

    public void IterateOriginalBegin(out Iterator outIterator, uint index, ulong count)
    {
        throw new NotImplementedException();
    }

    public Result MarkModified(uint index, ulong count)
    {
        throw new NotImplementedException();
    }

    public Result Flush()
    {
        throw new NotImplementedException();
    }

    public Result Invalidate()
    {
        throw new NotImplementedException();
    }

    private Result ReadBlock(Span<byte> outBuf, out uint outReadSize, ulong maxBytes, uint index, SubStorage storage)
    {
        throw new NotImplementedException();
    }

    private Result FindBitCount(out ulong outTotalCount, out Iterator outIterator, Span<byte> outBuf,
        out uint outReadIndex, out uint outRead, bool countZeros, SubStorage storage)
    {
        throw new NotImplementedException();
    }

    private Result IterateNextImpl(out ulong outCountZero, out ulong outCountOne, ref Iterator iter, SubStorage storage)
    {
        throw new NotImplementedException();
    }

    private Result ReadBitmap32Impl(out uint outBitmap, uint index, SubStorage storage)
    {
        throw new NotImplementedException();
    }
}