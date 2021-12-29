using System;

namespace LibHac.Fs;

public class MemoryStorage : IStorage
{
    private byte[] StorageBuffer { get; }

    public MemoryStorage(byte[] buffer)
    {
        StorageBuffer = buffer;
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        if (destination.Length == 0)
            return Result.Success;

        if (!CheckAccessRange(offset, destination.Length, StorageBuffer.Length))
            return ResultFs.OutOfRange.Log();

        StorageBuffer.AsSpan((int)offset, destination.Length).CopyTo(destination);

        return Result.Success;
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        if (source.Length == 0)
            return Result.Success;

        if (!CheckAccessRange(offset, source.Length, StorageBuffer.Length))
            return ResultFs.OutOfRange.Log();

        source.CopyTo(StorageBuffer.AsSpan((int)offset));

        return Result.Success;
    }

    public override Result Flush()
    {
        return Result.Success;
    }

    public override Result SetSize(long size)
    {
        return ResultFs.UnsupportedSetSizeForMemoryStorage.Log();
    }

    public override Result GetSize(out long size)
    {
        size = StorageBuffer.Length;

        return Result.Success;
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }
}