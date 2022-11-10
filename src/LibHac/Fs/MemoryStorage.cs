using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.Fs;

/// <summary>
/// Allows interacting with a <see cref="byte"/> array via the <see cref="IStorage"/> interface.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
public class MemoryStorage : IStorage
{
    private byte[] _storageBuffer;

    public MemoryStorage(byte[] buffer)
    {
        _storageBuffer = buffer;
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        if (destination.Length == 0)
            return Result.Success;

        Result rc = CheckAccessRange(offset, destination.Length, _storageBuffer.Length);
        if (rc.IsFailure()) return rc.Miss();

        _storageBuffer.AsSpan((int)offset, destination.Length).CopyTo(destination);

        return Result.Success;
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        if (source.Length == 0)
            return Result.Success;

        Result rc = CheckAccessRange(offset, source.Length, _storageBuffer.Length);
        if (rc.IsFailure()) return rc.Miss();

        source.CopyTo(_storageBuffer.AsSpan((int)offset));

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
        size = _storageBuffer.Length;

        return Result.Success;
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        switch (operationId)
        {
            case OperationId.InvalidateCache:
                return Result.Success;
            case OperationId.QueryRange:
                if (outBuffer.Length != Unsafe.SizeOf<QueryRangeInfo>())
                    return ResultFs.InvalidSize.Log();

                Unsafe.As<byte, QueryRangeInfo>(ref MemoryMarshal.GetReference(outBuffer)).Clear();
                return Result.Success;
            default:
                return ResultFs.UnsupportedOperateRangeForMemoryStorage.Log();
        }
    }
}