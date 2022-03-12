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
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public static class AlignmentMatchingStorageImpl
{
    public static uint GetRoundDownDifference(int value, uint alignment)
    {
        return (uint)(value - Alignment.AlignDownPow2(value, alignment));
    }

    public static uint GetRoundDownDifference(long value, uint alignment)
    {
        return (uint)(value - Alignment.AlignDownPow2(value, alignment));
    }

    public static uint GetRoundUpDifference(int value, uint alignment)
    {
        return (uint)(Alignment.AlignUpPow2(value, alignment) - value);
    }

    private static uint GetRoundUpDifference(long value, uint alignment)
    {
        return (uint)(Alignment.AlignUpPow2(value, alignment) - value);
    }

    public static Result Read(in SharedRef<IStorage> storage, Span<byte> workBuffer, uint dataAlignment,
        uint bufferAlignment, long offset, Span<byte> destination)
    {
        return Read(storage.Get, workBuffer, dataAlignment, bufferAlignment, offset, destination);
    }

    public static Result Write(in SharedRef<IStorage> storage, Span<byte> subBuffer, uint dataAlignment,
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

        long coreOffset = Alignment.AlignUpPow2(offset, dataAlignment);
        long coreSize = destination.Length < offsetRoundUpDifference
            ? 0
            : Alignment.AlignDownPow2(destination.Length - offsetRoundUpDifference, dataAlignment);

        long coveredOffset = coreSize > 0 ? coreOffset : offset;

        // Read the core portion that doesn't contain any partial blocks.
        if (coreSize > 0)
        {
            Result rc = storage.Read(coreOffset, destination.Slice((int)offsetRoundUpDifference, (int)coreSize));
            if (rc.IsFailure()) return rc.Miss();
        }

        // Read any partial block at the head of the requested range
        if (offset < coveredOffset)
        {
            long headOffset = Alignment.AlignDownPow2(offset, dataAlignment);
            int headSize = (int)(coveredOffset - offset);

            Assert.SdkAssert(GetRoundDownDifference(offset, dataAlignment) + headSize <= workBuffer.Length);

            Result rc = storage.Read(headOffset, workBuffer.Slice(0, (int)dataAlignment));
            if (rc.IsFailure()) return rc.Miss();

            workBuffer.Slice((int)GetRoundDownDifference(offset, dataAlignment), headSize).CopyTo(destination);
        }

        long tailOffset = coveredOffset + coreSize;
        long remainingTailSize = offset + destination.Length - tailOffset;

        // Read any partial block at the tail of the requested range
        while (remainingTailSize > 0)
        {
            long alignedTailOffset = Alignment.AlignDownPow2(tailOffset, dataAlignment);
            long copySize = Math.Min(alignedTailOffset + dataAlignment - tailOffset, remainingTailSize);

            Result rc = storage.Read(alignedTailOffset, workBuffer.Slice(0, (int)dataAlignment));
            if (rc.IsFailure()) return rc.Miss();

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

        long coreOffset = Alignment.AlignUpPow2(offset, dataAlignment);
        long coreSize = source.Length < offsetRoundUpDifference
            ? 0
            : Alignment.AlignDownPow2(source.Length - offsetRoundUpDifference, dataAlignment);

        long coveredOffset = coreSize > 0 ? coreOffset : offset;

        // Write the core portion that doesn't contain any partial blocks.
        if (coreSize > 0)
        {
            Result rc = storage.Write(coreOffset, source.Slice((int)offsetRoundUpDifference, (int)coreSize));
            if (rc.IsFailure()) return rc.Miss();
        }

        // Write any partial block at the head of the specified range
        if (offset < coveredOffset)
        {
            long headOffset = Alignment.AlignDownPow2(offset, dataAlignment);
            int headSize = (int)(coveredOffset - offset);

            Assert.SdkAssert((offset - headOffset) + headSize <= workBuffer.Length);

            // Read the existing block, copy the partial block to the appropriate portion,
            // and write the modified block back to the base storage.
            Result rc = storage.Read(headOffset, workBuffer.Slice(0, (int)dataAlignment));
            if (rc.IsFailure()) return rc.Miss();

            source.Slice(0, headSize).CopyTo(workBuffer.Slice((int)(offset - headOffset)));

            rc = storage.Write(headOffset, workBuffer.Slice(0, (int)dataAlignment));
            if (rc.IsFailure()) return rc.Miss();
        }

        long tailOffset = coveredOffset + coreSize;
        long remainingTailSize = offset + source.Length - tailOffset;

        // Write any partial block at the tail of the specified range
        while (remainingTailSize > 0)
        {
            Assert.SdkAssert(tailOffset - offset < source.Length);

            long alignedTailOffset = Alignment.AlignDownPow2(tailOffset, dataAlignment);
            long copySize = Math.Min(alignedTailOffset + dataAlignment - tailOffset, remainingTailSize);

            // Read the existing block, copy the partial block to the appropriate portion,
            // and write the modified block back to the base storage.
            Result rc = storage.Read(alignedTailOffset, workBuffer.Slice(0, (int)dataAlignment));
            if (rc.IsFailure()) return rc.Miss();

            source.Slice((int)(tailOffset - offset), (int)copySize)
                .CopyTo(workBuffer.Slice((int)GetRoundDownDifference(tailOffset, dataAlignment)));

            rc = storage.Write(alignedTailOffset, workBuffer.Slice(0, (int)dataAlignment));
            if (rc.IsFailure()) return rc.Miss();

            remainingTailSize -= copySize;
            tailOffset += copySize;
        }

        return Result.Success;
    }
}