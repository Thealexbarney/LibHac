// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Fs;

namespace LibHac.FsSystem;

public class Bitmap : IDisposable
{
    private const int IterateCacheSize = 0x80;
    private const uint BlockSize = 8;
    private const uint BitmapSizeAlignment = 0x20;

    public struct Iterator
    {
        public uint Index;
    }

    private uint _bitCount;
    private SubStorage _storage;

    public Bitmap()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public static long QuerySize(uint bitCount)
    {
        throw new NotImplementedException();
    }

    public void Initialize(uint bitCount, SubStorage bitmapStorage)
    {
        throw new NotImplementedException();
    }

    public static Result Format(uint bitCount, SubStorage storage)
    {
        throw new NotImplementedException();
    }

    public static Result Expand(uint bitCountOld, uint bitCountNew, SubStorage storage)
    {
        throw new NotImplementedException();
    }

    public Result IterateBegin(ref Iterator it, uint index)
    {
        throw new NotImplementedException();
    }

    private Result ReadBlock(Span<byte> buffer, uint index)
    {
        throw new NotImplementedException();
    }

    public Result IterateNext(out uint outZeroCount, out uint outOneCount, ref Iterator it)
    {
        throw new NotImplementedException();
    }

    public Result LimitedIterateNext(out uint outZeroCount, out uint outOneCount, ref Iterator it, uint maxZeroCount, uint maxOneCount)
    {
        throw new NotImplementedException();
    }

    public Result Reverse(uint index, uint count)
    {
        throw new NotImplementedException();
    }

    public Result Clear()
    {
        throw new NotImplementedException();
    }

    private Result GetBitmap32(out uint outValue, uint index)
    {
        throw new NotImplementedException();
    }

    private Result GetBit(out bool outValue, uint index)
    {
        throw new NotImplementedException();
    }
}