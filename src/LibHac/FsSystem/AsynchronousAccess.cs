using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;

namespace LibHac.FsSystem;

/// <summary>
/// <para>Splits read and write requests on an <see cref="IFile"/> or <see cref="IStorage"/> into smaller chunks
/// so the request can be processed by multiple threads simultaneously.</para>
/// <para>This interface exists because of <see cref="CompressedStorage"/> where it will split requests into
/// chunks that start and end on the boundaries of the compressed blocks.</para> 
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
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

        Result res = QueryAppropriateOffset(out long offsetAppropriate, startOffset, accessSize, alignmentSize);
        if (res.IsFailure()) return res.Miss();
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
            Result res = QueryNextOffset(out currentOffset, currentOffset, endOffset, accessSize, alignmentSize);
            if (res.IsFailure()) return res.Miss();

            invocationCount++;
        }

        count = invocationCount;
        return Result.Success;
    }

    Result QueryAppropriateOffset(out long offsetAppropriate, long startOffset, long accessSize, long alignmentSize);
}

/// <summary>
/// The default <see cref="IAsynchronousAccessSplitter"/> that is used when an <see cref="IStorage"/>
/// or <see cref="IFile"/> doesn't need any special logic to split a request into multiple chunks.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
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