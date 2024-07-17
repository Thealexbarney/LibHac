using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Diag;

namespace LibHac.Fs;

/// <summary>
/// Allows interacting with a <see cref="byte"/> array via the <see cref="IStorage"/> interface.
/// </summary>
/// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
public class MemoryStorage : IStorage
{
    private byte[] _buffer;
    private int _size;

    public MemoryStorage(byte[] buffer)
    {
        _buffer = buffer;
        _size = buffer.Length;
    }

    public MemoryStorage(byte[] buffer, int size)
    {
        Assert.SdkRequiresNotNull(buffer);
        Assert.SdkRequiresInRange(size, 0, buffer.Length);

        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        Abort.DoAbortUnless(buffer is null || 0 <= size && size < buffer.Length);

        _buffer = buffer;
        _size = size;
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        if (destination.Length == 0)
            return Result.Success;

        Result res = CheckAccessRange(offset, destination.Length, _size);
        if (res.IsFailure()) return res.Miss();

        _buffer.AsSpan((int)offset, destination.Length).CopyTo(destination);

        return Result.Success;
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        if (source.Length == 0)
            return Result.Success;

        Result res = CheckAccessRange(offset, source.Length, _size);
        if (res.IsFailure()) return res.Miss();

        source.CopyTo(_buffer.AsSpan((int)offset));

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
        size = _size;

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

/// <summary>
/// Allows interacting with a <see cref="Memory{T}"/> via the <see cref="IStorage"/> interface.
/// </summary>
/// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
internal class MemoryStorageFromMemory : IStorage
{
    private Memory<byte> _buffer;
    private int _size;

    public MemoryStorageFromMemory(Memory<byte> buffer)
    {
        _buffer = buffer;
        _size = buffer.Length;
    }

    public MemoryStorageFromMemory(Memory<byte> buffer, int size)
    {
        Assert.SdkRequiresInRange(size, 0, buffer.Length);

        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        Abort.DoAbortUnless(0 <= size && size < buffer.Length);

        _buffer = buffer;
        _size = size;
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        if (destination.Length == 0)
            return Result.Success;

        Result res = CheckAccessRange(offset, destination.Length, _size);
        if (res.IsFailure()) return res.Miss();

        _buffer.Span.Slice((int)offset, destination.Length).CopyTo(destination);

        return Result.Success;
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        if (source.Length == 0)
            return Result.Success;

        Result res = CheckAccessRange(offset, source.Length, _size);
        if (res.IsFailure()) return res.Miss();

        source.CopyTo(_buffer.Span.Slice((int)offset));

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
        size = _size;

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