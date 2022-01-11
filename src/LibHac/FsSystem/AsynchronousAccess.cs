using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Util;

namespace LibHac.FsSystem;

public interface IAsynchronousAccessSplitter : IDisposable
{
    private static readonly DefaultAsynchronousAccessSplitter DefaultAccessSplitter = new();

    public static IAsynchronousAccessSplitter GetDefaultAsynchronousAccessSplitter()
    {
        return DefaultAccessSplitter;
    }

    Result QueryNextOffset(out long nextOffset, long startOffset, long endOffset, long accessSize, long alignmentSize)
    {
        UnsafeHelpers.SkipParamInit(out nextOffset);

        Assert.SdkRequiresLess(0, accessSize);
        Assert.SdkRequiresLess(0, alignmentSize);

        if (endOffset - startOffset <= accessSize)
        {
            nextOffset = endOffset;
            return Result.Success;
        }

        Result rc = QueryAppropriateOffset(out long offsetAppropriate, startOffset, accessSize, alignmentSize);
        if (rc.IsFailure()) return rc.Miss();
        Assert.SdkNotEqual(startOffset, offsetAppropriate);

        nextOffset = Math.Min(startOffset, offsetAppropriate);
        return Result.Success;
    }

    Result QueryInvocationCount(out long count, long startOffset, long endOffset, long accessSize, long alignmentSize)
    {
        UnsafeHelpers.SkipParamInit(out count);

        long invocationCount = 0;
        long currentOffset = startOffset;

        while (currentOffset < endOffset)
        {
            Result rc = QueryNextOffset(out currentOffset, currentOffset, endOffset, accessSize, alignmentSize);
            if (rc.IsFailure()) return rc.Miss();

            invocationCount++;
        }

        count = invocationCount;
        return Result.Success;
    }

    Result QueryAppropriateOffset(out long offsetAppropriate, long startOffset, long accessSize, long alignmentSize);
}

public class DefaultAsynchronousAccessSplitter : IAsynchronousAccessSplitter
{
    public void Dispose() { }

    public Result QueryAppropriateOffset(out long offsetAppropriate, long startOffset, long accessSize, long alignmentSize)
    {
        offsetAppropriate = Alignment.AlignDownPow2(startOffset + accessSize, alignmentSize);
        return Result.Success;
    }

    public Result QueryInvocationCount(out long count, long startOffset, long endOffset, long accessSize, long alignmentSize)
    {
        long alignedStartOffset = Alignment.AlignDownPow2(startOffset, alignmentSize);
        count = BitUtil.DivideUp(endOffset - alignedStartOffset, accessSize);
        return Result.Success;
    }
}