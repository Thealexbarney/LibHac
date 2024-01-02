using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Util;

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
        _bitCount = 0;
    }

    public void Dispose()
    {
        _storage?.Dispose();
        _storage = null;
    }

    public static long QuerySize(uint bitCount)
    {
        return Alignment.AlignUp(bitCount, BitmapSizeAlignment);
    }

    public void Initialize(uint bitCount, SubStorage bitmapStorage)
    {
        _bitCount = bitCount;
        _storage = bitmapStorage;
    }

    public static Result Format(uint bitCount, SubStorage storage)
    {
        Span<byte> bits = stackalloc byte[IterateCacheSize];
        bits.Clear();

        long size = QuerySize(bitCount);
        long offset = 0;

        while (size != 0)
        {
            int iterationSize = (int)Math.Min(bits.Length, size);

            Result res = storage.Write(offset, bits.Slice(0, iterationSize));
            if (res.IsFailure()) return res.Miss();

            size -= iterationSize;
            offset += iterationSize;
        }

        return Result.Success;
    }

    public static Result Expand(uint bitCountOld, uint bitCountNew, SubStorage storage)
    {
        Assert.SdkRequiresGreater(bitCountNew, bitCountOld);

        Span<byte> bits = stackalloc byte[IterateCacheSize];

        long sizeOld = QuerySize(bitCountOld);
        long sizeNew = QuerySize(bitCountNew);

        if (sizeNew > sizeOld)
        {
            bits.Clear();
            long expandSize = sizeNew - sizeOld;
            long offset = sizeOld;

            while (expandSize != 0)
            {
                int iterationSize = (int)Math.Min(bits.Length, expandSize);

                Result res = storage.Write(offset, bits.Slice(0, iterationSize));
                if (res.IsFailure()) return res.Miss();

                expandSize -= iterationSize;
                offset += iterationSize;
            }
        }

        return Result.Success;
    }

    public Result IterateBegin(ref Iterator it, uint index)
    {
        Assert.SdkRequiresNotNull(ref it);
        Assert.SdkRequiresLessEqual(index, _bitCount);

        it.Index = index;
        return Result.Success;
    }

    private Result ReadBlock(Span<byte> buffer, uint index)
    {
        Assert.SdkRequiresNotNull(buffer);
        Assert.SdkAssert((buffer.Length & 3) == 0);

        uint blockIndex = Alignment.AlignDown(index, BitmapSizeAlignment);

        Result res = _storage.Read(blockIndex / BlockSize, buffer);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result IterateNext(out uint outZeroCount, out uint outOneCount, ref Iterator it)
    {
        return LimitedIterateNext(out outZeroCount, out outOneCount, ref it, 0, 0).Ret();
    }

    public Result LimitedIterateNext(out uint outZeroCount, out uint outOneCount, ref Iterator it, uint maxZeroCount, uint maxOneCount)
    {
        Assert.SdkNotNullOut(out outZeroCount);
        Assert.SdkNotNullOut(out outOneCount);
        Assert.SdkNotNull(ref it);
        Assert.SdkAssert(it.Index <= _bitCount);

        outZeroCount = 0;
        outOneCount = 0;

        if (it.Index >= _bitCount)
            return Result.Success;

        Span<byte> buffer = stackalloc byte[IterateCacheSize];

        uint totalCount = 0;
        bool countingZeros = true;
        bool isLastIteration = false;

        do
        {
            if (it.Index >= _bitCount)
                break;

            int readRequestBytes =
                (int)Math.Min(
                    (Alignment.AlignUp(_bitCount, BitmapSizeAlignment) -
                     Alignment.AlignDown(it.Index, BitmapSizeAlignment)) / 8, IterateCacheSize);

            Result res = ReadBlock(buffer.Slice(0, readRequestBytes), it.Index);
            if (res.IsFailure()) return res.Miss();

            for (int i = 0; i < readRequestBytes; i += 4)
            {
                uint value = BitmapUtils.ReadU32(buffer, i);
                uint positionInValue = it.Index % BitmapSizeAlignment;
                value <<= (int)positionInValue;

                int currentIterationCount = 0;

                if (totalCount == 0)
                {
                    currentIterationCount = BitmapUtils.CountLeadingZeros(value);
                    if (currentIterationCount != 0)
                    {
                        countingZeros = true;
                    }
                    else
                    {
                        currentIterationCount = BitmapUtils.CountLeadingOnes(value);
                        if (currentIterationCount != 0)
                        {
                            countingZeros = false;
                        }
                        else
                        {
                            Assert.SdkAssert(false);
                        }
                    }
                }
                else if (countingZeros)
                {
                    currentIterationCount = BitmapUtils.CountLeadingZeros(value);
                    if (currentIterationCount == 0)
                    {
                        outZeroCount = totalCount;
                        isLastIteration = true;
                        break;
                    }
                }
                else
                {
                    currentIterationCount = BitmapUtils.CountLeadingOnes(value);
                    if (currentIterationCount == 0)
                    {
                        outOneCount = totalCount;
                        isLastIteration = true;
                        break;
                    }
                }

                isLastIteration = false;

                if (currentIterationCount >= 32 - positionInValue)
                {
                    currentIterationCount = (int)(32 - positionInValue);
                }
                else
                {
                    isLastIteration = true;
                }

                int bitsRemainingInBitmap = (int)(_bitCount - it.Index);
                if (currentIterationCount >= bitsRemainingInBitmap)
                {
                    currentIterationCount = bitsRemainingInBitmap;
                    isLastIteration = true;
                }

                totalCount += (uint)currentIterationCount;
                it.Index += (uint)currentIterationCount;

                if (countingZeros)
                {
                    if (maxZeroCount != 0 && totalCount >= maxZeroCount)
                    {
                        it.Index -= totalCount - maxZeroCount;
                        totalCount = maxZeroCount;
                        isLastIteration = true;
                    }
                }
                else if (maxOneCount != 0 && totalCount >= maxOneCount)
                {
                    it.Index -= totalCount - maxOneCount;
                    totalCount = maxOneCount;
                    isLastIteration = true;
                }

                if (isLastIteration)
                {
                    if (countingZeros)
                    {
                        outZeroCount = totalCount;
                    }
                    else
                    {
                        outOneCount = totalCount;
                    }

                    break;
                }
            }
        } while (!isLastIteration);

        return Result.Success;
    }

    public Result Reverse(uint index, uint count)
    {
        if (index + count > _bitCount)
            return ResultFs.InvalidBitmapIndex.Log();

        uint remaining = count;
        uint currentIndex = count;
        uint startBit = index % 0x20;
        Span<byte> bits = stackalloc byte[4];

        while (remaining != 0)
        {
            Result res = _storage.Read(4 * (currentIndex / 0x20), bits);
            if (res.IsFailure()) return res.Miss();

            uint value = BitmapUtils.ReadU32(bits, 0);

            uint mask = remaining < 0x20 ? ~((1u << (0x20 - (int)remaining)) - 1) : uint.MaxValue;

            if (startBit != 0)
            {
                mask >>= (int)startBit;
            }

            BitmapUtils.WriteU32(bits, 0, value ^ mask);

            res = _storage.Write(4 * (currentIndex / 0x20), bits);
            if (res.IsFailure()) return res.Miss();

            currentIndex += 0x20 - startBit;

            if (remaining + startBit > 0x20)
            {
                remaining -= 0x20 - startBit;
            }
            else
            {
                remaining = 0;
            }

            startBit = 0;
        }

        return Result.Success;
    }

    public Result Clear()
    {
        Span<byte> bits = stackalloc byte[128];
        Span<byte> bitsOriginal = stackalloc byte[128];
        bits.Clear();

        long size = QuerySize(_bitCount);
        long offset = 0;

        while (size != 0)
        {
            int iterationSize = (int)Math.Min(bits.Length, size);

            Result res = _storage.Read(offset, bitsOriginal.Slice(0, iterationSize));
            if (res.IsFailure()) return res.Miss();

            if (!bitsOriginal.Slice(0, iterationSize).IsZeros())
            {
                res = _storage.Write(offset, bits.Slice(0, iterationSize));
                if (res.IsFailure()) return res.Miss();
            }

            size -= iterationSize;
            offset += iterationSize;
        }

        return Result.Success;
    }

    private Result GetBitmap32(out uint outValue, uint index)
    {
        Assert.SdkRequiresNotNullOut(out outValue);
        Assert.SdkRequires((index & 0x1F) == 0);

        Span<byte> bits = stackalloc byte[4];

        Result res = ReadBlock(bits, index);
        if (res.IsFailure()) return res.Miss();

        outValue = Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(bits));
        return Result.Success;
    }

    private Result GetBit(out bool outValue, uint index)
    {
        Assert.SdkRequiresNotNullOut(out outValue);

        Span<byte> bits = stackalloc byte[4];

        if (index >= _bitCount)
            return ResultFs.InvalidBitmapIndex.Log();

        Result res = ReadBlock(bits, index);
        if (res.IsFailure()) return res.Miss();

        uint value = Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(bits)) << (int)(index & 0x1F);
        outValue = (value & 0x80000000) != 0;

        return Result.Success;
    }
}