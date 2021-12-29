using System;
using LibHac.Fs;

namespace LibHac.Tools.FsSystem;

/// <summary>
/// An <see cref="IStorage"/> that returns all zeros when read, and does nothing on write.
/// </summary>
public class NullStorage : IStorage
{
    private long Length { get; }

    public NullStorage() { }
    public NullStorage(long length) => Length = length;


    public override Result Read(long offset, Span<byte> destination)
    {
        destination.Clear();
        return Result.Success;
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        return Result.Success;
    }

    public override Result Flush()
    {
        return Result.Success;
    }

    public override Result SetSize(long size)
    {
        return ResultFs.NotImplemented.Log();
    }

    public override Result GetSize(out long size)
    {
        size = Length;
        return Result.Success;
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        return Result.Success;
    }
}