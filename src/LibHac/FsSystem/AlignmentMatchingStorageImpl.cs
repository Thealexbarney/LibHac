using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Util;

namespace LibHac.FsSystem;

/// <summary>
/// Contains the functions used by classes like <see cref="AlignmentMatchingStorage{TDataAlignment,TBufferAlignment}"/> for
/// accessing an aligned <see cref="IStorage"/>. 
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public static class AlignmentMatchingStorageImpl
{
    public static uint GetRoundDownDifference(int value, uint alignment)
    {
        return (uint)(value - Alignment.AlignDown(value, alignment));
    }

    public static uint GetRoundDownDifference(long value, uint alignment)
    {
        return (uint)(value - Alignment.AlignDown(value, alignment));
    }

    public static uint GetRoundUpDifference(int value, uint alignment)
    {
        return (uint)(Alignment.AlignUp(value, alignment) - value);
    }

    private static uint GetRoundUpDifference(long value, uint alignment)
    {
        return (uint)(Alignment.AlignUp(value, alignment) - value);
    }

    public static Result Read(ref readonly SharedRef<IStorage> storage, Span<byte> workBuffer, uint dataAlignment,
        uint bufferAlignment, long offset, Span<byte> destination)
    {
        return Read(storage.Get, workBuffer, dataAlignment, bufferAlignment, offset, destination);
    }

    public static Result Write(ref readonly SharedRef<IStorage> storage, Span<byte> subBuffer, uint dataAlignment,
        uint bufferAlignment, long offset, ReadOnlySpan<byte> source)
    {
        return Write(storage.Get, subBuffer, dataAlignment, bufferAlignment, offset, source);
    }

    public static Result Read(IStorage storage, Span<byte> workBuffer, uint dataAlignment, uint bufferAlignment,
        long offset, Span<byte> destination)
    {
        // We don't support buffer alignment because Nintendo never uses any alignment other than 1, and because
        // we'd have to mess with pinning the buffer.
        Abort.DoAbortUnless(bufferAlignment == 1);

        Assert.SdkRequiresGreaterEqual((uint)workBuffer.Length, dataAlignment);

        if (destination.Length == 0)
            return Result.Success;

        // Calculate the range that contains only full data blocks.
        uint offsetRoundUpDifference = GetRoundUpDifference(offset, dataAlignment);

        long coreOffset = Alignment.AlignUp(offset, dataAlignment);
        long coreSize = destination.Length < offsetRoundUpDifference
            ? 0
            : Alignment.AlignDown(destination.Length - offsetRoundUpDifference, dataAlignment);

        long coveredOffset = coreSize > 0 ? coreOffset : offset;

        // Read the core portion that doesn't contain any partial blocks.
        if (coreSize > 0)
        {
            Result res = storage.Read(coreOffset, destination.Slice((int)offsetRoundUpDifference, (int)coreSize));
            if (res.IsFailure()) return res.Miss();
        }

        // Read any partial block at the head of the requested range
        if (offset < coveredOffset)
        {
            long headOffset = Alignment.AlignDown(offset, dataAlignment);
            int headSize = (int)(coveredOffset - offset);

            Assert.SdkAssert(GetRoundDownDifference(offset, dataAlignment) + headSize <= workBuffer.Length);

            Result res = storage.Read(headOffset, workBuffer.Slice(0, (int)dataAlignment));
            if (res.IsFailure()) return res.Miss();

            workBuffer.Slice((int)GetRoundDownDifference(offset, dataAlignment), headSize).CopyTo(destination);
        }

        long tailOffset = coveredOffset + coreSize;
        long remainingTailSize = offset + destination.Length - tailOffset;

        // Read any partial block at the tail of the requested range
        while (remainingTailSize > 0)
        {
            long alignedTailOffset = Alignment.AlignDown(tailOffset, dataAlignment);
            long copySize = Math.Min(alignedTailOffset + dataAlignment - tailOffset, remainingTailSize);

            Result res = storage.Read(alignedTailOffset, workBuffer.Slice(0, (int)dataAlignment));
            if (res.IsFailure()) return res.Miss();

            Assert.SdkAssert(tailOffset - offset + copySize <= destination.Length);
            Assert.SdkAssert(tailOffset - alignedTailOffset + copySize <= dataAlignment);
            workBuffer.Slice((int)(tailOffset - alignedTailOffset), (int)copySize)
                .CopyTo(destination.Slice((int)(tailOffset - offset)));

            remainingTailSize -= copySize;
            tailOffset += copySize;
        }

        return Result.Success;
    }

    public static Result Write(IStorage storage, Span<byte> workBuffer, uint dataAlignment, uint bufferAlignment,
        long offset, ReadOnlySpan<byte> source)
    {
        // We don't support buffer alignment because Nintendo never uses any alignment other than 1, and because
        // we'd have to mess with pinning the buffer.
        Abort.DoAbortUnless(bufferAlignment == 1);

        Assert.SdkRequiresGreaterEqual((uint)workBuffer.Length, dataAlignment);

        if (source.Length == 0)
            return Result.Success;

        // Calculate the range that contains only full data blocks.
        uint offsetRoundUpDifference = GetRoundUpDifference(offset, dataAlignment);

        long coreOffset = Alignment.AlignUp(offset, dataAlignment);
        long coreSize = source.Length < offsetRoundUpDifference
            ? 0
            : Alignment.AlignDown(source.Length - offsetRoundUpDifference, dataAlignment);

        long coveredOffset = coreSize > 0 ? coreOffset : offset;

        // Write the core portion that doesn't contain any partial blocks.
        if (coreSize > 0)
        {
            Result res = storage.Write(coreOffset, source.Slice((int)offsetRoundUpDifference, (int)coreSize));
            if (res.IsFailure()) return res.Miss();
        }

        // Write any partial block at the head of the specified range
        if (offset < coveredOffset)
        {
            long headOffset = Alignment.AlignDown(offset, dataAlignment);
            int headSize = (int)(coveredOffset - offset);

            Assert.SdkAssert((offset - headOffset) + headSize <= workBuffer.Length);

            // Read the existing block, copy the partial block to the appropriate portion,
            // and write the modified block back to the base storage.
            Result res = storage.Read(headOffset, workBuffer.Slice(0, (int)dataAlignment));
            if (res.IsFailure()) return res.Miss();

            source.Slice(0, headSize).CopyTo(workBuffer.Slice((int)(offset - headOffset)));

            res = storage.Write(headOffset, workBuffer.Slice(0, (int)dataAlignment));
            if (res.IsFailure()) return res.Miss();
        }

        long tailOffset = coveredOffset + coreSize;
        long remainingTailSize = offset + source.Length - tailOffset;

        // Write any partial block at the tail of the specified range
        while (remainingTailSize > 0)
        {
            Assert.SdkAssert(tailOffset - offset < source.Length);

            long alignedTailOffset = Alignment.AlignDown(tailOffset, dataAlignment);
            long copySize = Math.Min(alignedTailOffset + dataAlignment - tailOffset, remainingTailSize);

            // Read the existing block, copy the partial block to the appropriate portion,
            // and write the modified block back to the base storage.
            Result res = storage.Read(alignedTailOffset, workBuffer.Slice(0, (int)dataAlignment));
            if (res.IsFailure()) return res.Miss();

            source.Slice((int)(tailOffset - offset), (int)copySize)
                .CopyTo(workBuffer.Slice((int)GetRoundDownDifference(tailOffset, dataAlignment)));

            res = storage.Write(alignedTailOffset, workBuffer.Slice(0, (int)dataAlignment));
            if (res.IsFailure()) return res.Miss();

            remainingTailSize -= copySize;
            tailOffset += copySize;
        }

        return Result.Success;
    }
}