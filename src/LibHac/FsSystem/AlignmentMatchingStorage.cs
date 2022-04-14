using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Util;

namespace LibHac.FsSystem;

public interface IAlignmentMatchingStorageSize { }

[StructLayout(LayoutKind.Sequential, Size = 1)] public struct AlignmentMatchingStorageSize1 : IAlignmentMatchingStorageSize { }
[StructLayout(LayoutKind.Sequential, Size = 16)] public struct AlignmentMatchingStorageSize16 : IAlignmentMatchingStorageSize { }
[StructLayout(LayoutKind.Sequential, Size = 512)] public struct AlignmentMatchingStorageSize512 : IAlignmentMatchingStorageSize { }

/// <summary>
/// Handles accessing a base <see cref="IStorage"/> that must always be accessed via an aligned offset and size. 
/// </summary>
/// <typeparam name="TDataAlignment">The alignment of all accesses made to the base storage.
/// Must be a power of 2 that is less than or equal to 0x200.</typeparam>
/// <typeparam name="TBufferAlignment">The alignment of the destination buffer for the core read. Must be a power of 2.</typeparam>
/// <remarks><para>This class uses a work buffer on the stack to avoid allocations. Because of this the data alignment
/// must be kept small; no larger than 0x200. The <see cref="AlignmentMatchingStoragePooledBuffer{TBufferAlignment}"/> class
/// should be used for data alignment sizes larger than this.</para>
/// <para>Based on FS 14.1.0 (nnSdk 14.3.0)</para></remarks>
[SkipLocalsInit]
public class AlignmentMatchingStorage<TDataAlignment, TBufferAlignment> : IStorage
    where TDataAlignment : struct, IAlignmentMatchingStorageSize
    where TBufferAlignment : struct, IAlignmentMatchingStorageSize
{
    public static uint DataAlign => (uint)Unsafe.SizeOf<TDataAlignment>();
    public static uint BufferAlign => (uint)Unsafe.SizeOf<TBufferAlignment>();

    public static uint DataAlignMax => 0x200;

    private static void VerifyTypeParameters()
    {
        Abort.DoAbortUnless(DataAlign <= DataAlignMax);
        Abort.DoAbortUnless(BitUtil.IsPowerOfTwo(DataAlign));
        Abort.DoAbortUnless(BitUtil.IsPowerOfTwo(BufferAlign));
    }

    private IStorage _baseStorage;
    private long _baseStorageSize;
    private bool _isBaseStorageSizeDirty;
    private SharedRef<IStorage> _sharedBaseStorage;

    public AlignmentMatchingStorage(ref SharedRef<IStorage> baseStorage)
    {
        VerifyTypeParameters();

        _baseStorage = baseStorage.Get;
        _isBaseStorageSizeDirty = true;
        _sharedBaseStorage = SharedRef<IStorage>.CreateMove(ref baseStorage);
    }

    public AlignmentMatchingStorage(IStorage baseStorage)
    {
        VerifyTypeParameters();

        _baseStorage = baseStorage;
        _isBaseStorageSizeDirty = true;
    }

    public override void Dispose()
    {
        _sharedBaseStorage.Destroy();

        base.Dispose();
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        Span<byte> workBuffer = stackalloc byte[(int)DataAlign];

        if (destination.Length == 0)
            return Result.Success;

        Result rc = GetSize(out long totalSize);
        if (rc.IsFailure()) return rc.Miss();

        rc = CheckAccessRange(offset, destination.Length, totalSize);
        if (rc.IsFailure()) return rc.Miss();

        return AlignmentMatchingStorageImpl.Read(_baseStorage, workBuffer, DataAlign, BufferAlign, offset, destination);
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        Span<byte> workBuffer = stackalloc byte[(int)DataAlign];

        if (source.Length == 0)
            return Result.Success;

        Result rc = GetSize(out long totalSize);
        if (rc.IsFailure()) return rc.Miss();

        rc = CheckAccessRange(offset, source.Length, totalSize);
        if (rc.IsFailure()) return rc.Miss();

        return AlignmentMatchingStorageImpl.Write(_baseStorage, workBuffer, DataAlign, BufferAlign, offset, source);
    }

    public override Result Flush()
    {
        return _baseStorage.Flush();
    }

    public override Result SetSize(long size)
    {
        Result rc = _baseStorage.SetSize(Alignment.AlignUpPow2(size, DataAlign));
        _isBaseStorageSizeDirty = true;

        return rc;
    }

    public override Result GetSize(out long size)
    {
        UnsafeHelpers.SkipParamInit(out size);

        if (_isBaseStorageSizeDirty)
        {
            Result rc = _baseStorage.GetSize(out long baseStorageSize);
            if (rc.IsFailure()) return rc.Miss();

            _baseStorageSize = baseStorageSize;
            _isBaseStorageSizeDirty = false;
        }

        size = _baseStorageSize;
        return Result.Success;
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        if (operationId == OperationId.InvalidateCache)
        {
            return _baseStorage.OperateRange(OperationId.InvalidateCache, offset, size);
        }

        if (size == 0)
            return Result.Success;

        Result rc = GetSize(out long baseStorageSize);
        if (rc.IsFailure()) return rc.Miss();

        rc = CheckOffsetAndSize(offset, size);
        if (rc.IsFailure()) return rc.Miss();

        long validSize = Math.Min(size, baseStorageSize - offset);
        long alignedOffset = Alignment.AlignDownPow2(offset, DataAlign);
        long alignedOffsetEnd = Alignment.AlignUpPow2(offset + validSize, DataAlign);
        long alignedSize = alignedOffsetEnd - alignedOffset;

        return _baseStorage.OperateRange(outBuffer, operationId, alignedOffset, alignedSize, inBuffer);
    }
}

/// <summary>
/// Handles accessing a base <see cref="IStorage"/> that must always be accessed via an aligned offset and size. 
/// </summary>
/// <typeparam name="TBufferAlignment">The alignment of the destination buffer for the core read. Must be a power of 2.</typeparam>
/// <remarks><para>On every access this class allocates a work buffer that is used for handling any partial blocks at
/// the beginning or end of the requested range. For data alignment sizes of 0x200 or smaller
/// <see cref="AlignmentMatchingStorage{TDataAlignment,TBufferAlignment}"/> should be used instead
/// to avoid these allocations.</para>
/// <para>Based on FS 14.1.0 (nnSdk 14.3.0)</para></remarks>
public class AlignmentMatchingStoragePooledBuffer<TBufferAlignment> : IStorage
    where TBufferAlignment : struct, IAlignmentMatchingStorageSize
{
    public static uint BufferAlign => (uint)Unsafe.SizeOf<TBufferAlignment>();

    private IStorage _baseStorage;
    private long _baseStorageSize;
    private uint _dataAlignment;
    private bool _isBaseStorageSizeDirty;

    // LibHac addition: This field goes unused if initialized with a plain IStorage.
    // The original class uses a template for both the shared and non-shared IStorage which avoids needing this field.
    private SharedRef<IStorage> _sharedBaseStorage;

    public AlignmentMatchingStoragePooledBuffer(IStorage baseStorage, int dataAlign)
    {
        Abort.DoAbortUnless(BitUtil.IsPowerOfTwo(BufferAlign));

        _baseStorage = baseStorage;
        _dataAlignment = (uint)dataAlign;
        _isBaseStorageSizeDirty = true;

        Assert.SdkRequires(BitUtil.IsPowerOfTwo(dataAlign), "DataAlign must be a power of 2.");
    }

    public AlignmentMatchingStoragePooledBuffer(in SharedRef<IStorage> baseStorage, int dataAlign)
    {
        Abort.DoAbortUnless(BitUtil.IsPowerOfTwo(BufferAlign));

        _baseStorage = baseStorage.Get;
        _dataAlignment = (uint)dataAlign;
        _isBaseStorageSizeDirty = true;

        Assert.SdkRequires(BitUtil.IsPowerOfTwo(dataAlign), "DataAlign must be a power of 2.");

        _sharedBaseStorage = SharedRef<IStorage>.CreateCopy(in baseStorage);
    }

    public override void Dispose()
    {
        _sharedBaseStorage.Destroy();

        base.Dispose();
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        if (destination.Length == 0)
            return Result.Success;

        Result rc = GetSize(out long baseStorageSize);
        if (rc.IsFailure()) return rc.Miss();

        rc = CheckAccessRange(offset, destination.Length, baseStorageSize);
        if (rc.IsFailure()) return rc.Miss();

        using var pooledBuffer = new PooledBuffer();
        pooledBuffer.AllocateParticularlyLarge((int)_dataAlignment, (int)_dataAlignment);

        return AlignmentMatchingStorageImpl.Read(_baseStorage, pooledBuffer.GetBuffer(), _dataAlignment, BufferAlign,
            offset, destination);
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        if (source.Length == 0)
            return Result.Success;

        Result rc = GetSize(out long baseStorageSize);
        if (rc.IsFailure()) return rc.Miss();

        rc = CheckAccessRange(offset, source.Length, baseStorageSize);
        if (rc.IsFailure()) return rc.Miss();

        using var pooledBuffer = new PooledBuffer();
        pooledBuffer.AllocateParticularlyLarge((int)_dataAlignment, (int)_dataAlignment);

        return AlignmentMatchingStorageImpl.Write(_baseStorage, pooledBuffer.GetBuffer(), _dataAlignment, BufferAlign,
            offset, source);
    }

    public override Result Flush()
    {
        return _baseStorage.Flush();
    }

    public override Result SetSize(long size)
    {
        Result rc = _baseStorage.SetSize(Alignment.AlignUpPow2(size, _dataAlignment));
        _isBaseStorageSizeDirty = true;

        return rc;
    }

    public override Result GetSize(out long size)
    {
        UnsafeHelpers.SkipParamInit(out size);

        if (_isBaseStorageSizeDirty)
        {
            Result rc = _baseStorage.GetSize(out long baseStorageSize);
            if (rc.IsFailure()) return rc.Miss();

            _isBaseStorageSizeDirty = false;
            _baseStorageSize = baseStorageSize;
        }

        size = _baseStorageSize;
        return Result.Success;
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        if (operationId == OperationId.InvalidateCache)
        {
            return _baseStorage.OperateRange(OperationId.InvalidateCache, offset, size);
        }

        if (size == 0)
            return Result.Success;

        Result rc = GetSize(out long baseStorageSize);
        if (rc.IsFailure()) return rc.Miss();

        rc = CheckOffsetAndSize(offset, size);
        if (rc.IsFailure()) return rc.Miss();

        long validSize = Math.Min(size, baseStorageSize - offset);
        long alignedOffset = Alignment.AlignDownPow2(offset, _dataAlignment);
        long alignedOffsetEnd = Alignment.AlignUpPow2(offset + validSize, _dataAlignment);
        long alignedSize = alignedOffsetEnd - alignedOffset;

        return _baseStorage.OperateRange(outBuffer, operationId, alignedOffset, alignedSize, inBuffer);
    }
}

/// <summary>
/// Handles accessing a base <see cref="IStorage"/> that must always be accessed via an aligned offset and size. 
/// </summary>
/// <typeparam name="TBufferAlignment">The alignment of the destination buffer for the core read. Must be a power of 2.</typeparam>
/// <remarks><para>This class is basically the same as <see cref="AlignmentMatchingStoragePooledBuffer{TBufferAlignment}"/> except
/// it doesn't allocate a work buffer for reads that are already aligned, and it ignores the buffer alignment for reads.</para>
/// <para>Based on FS 13.1.0 (nnSdk 13.4.0)</para></remarks>
public class AlignmentMatchingStorageInBulkRead<TBufferAlignment> : IStorage
    where TBufferAlignment : struct, IAlignmentMatchingStorageSize
{
    public static uint BufferAlign => (uint)Unsafe.SizeOf<TBufferAlignment>();

    private IStorage _baseStorage;
    private SharedRef<IStorage> _sharedBaseStorage;
    private long _baseStorageSize;
    private uint _dataAlignment;

    public AlignmentMatchingStorageInBulkRead(IStorage baseStorage, int dataAlignment)
    {
        Abort.DoAbortUnless(BitUtil.IsPowerOfTwo(BufferAlign));

        _baseStorage = baseStorage;
        _baseStorageSize = -1;
        _dataAlignment = (uint)dataAlignment;

        Assert.SdkRequires(BitUtil.IsPowerOfTwo(dataAlignment));
    }

    public AlignmentMatchingStorageInBulkRead(in SharedRef<IStorage> baseStorage, int dataAlignment)
    {
        Abort.DoAbortUnless(BitUtil.IsPowerOfTwo(BufferAlign));

        _baseStorage = baseStorage.Get;
        _baseStorageSize = -1;
        _dataAlignment = (uint)dataAlignment;

        Assert.SdkRequires(BitUtil.IsPowerOfTwo(dataAlignment));

        _sharedBaseStorage = SharedRef<IStorage>.CreateCopy(in baseStorage);
    }

    public override void Dispose()
    {
        _sharedBaseStorage.Destroy();

        base.Dispose();
    }

    // The original template doesn't define this function, requiring a specialized function for each TBufferAlignment used.
    // The only buffer alignment used by that template is 1, so we use that specialization for our Read method.
    public override Result Read(long offset, Span<byte> destination)
    {
        if (destination.Length == 0)
            return Result.Success;

        Result rc = GetSize(out long baseStorageSize);
        if (rc.IsFailure()) return rc.Miss();

        rc = CheckAccessRange(offset, destination.Length, baseStorageSize);
        if (rc.IsFailure()) return rc.Miss();

        // Calculate the aligned offsets of the requested region.
        long offsetEnd = offset + destination.Length;
        long alignedOffset = Alignment.AlignDownPow2(offset, _dataAlignment);
        long alignedOffsetEnd = Alignment.AlignUpPow2(offsetEnd, _dataAlignment);
        long alignedSize = alignedOffsetEnd - alignedOffset;

        using var pooledBuffer = new PooledBuffer();

        // If we aren't aligned we need to allocate a buffer.
        if (alignedOffset != offset || alignedSize != destination.Length)
        {
            if (alignedSize <= PooledBuffer.GetAllocatableSizeMax())
            {
                // Try to allocate a buffer that will fit the entire aligned read.
                pooledBuffer.Allocate((int)alignedSize, (int)_dataAlignment);

                // If we were able to get a buffer that fits the entire aligned read then read it
                // into the buffer and copy the unaligned portion to the destination buffer.
                if (alignedSize <= pooledBuffer.GetSize())
                {
                    rc = _baseStorage.Read(alignedOffset, pooledBuffer.GetBuffer().Slice(0, (int)alignedSize));
                    if (rc.IsFailure()) return rc.Miss();

                    pooledBuffer.GetBuffer().Slice((int)(offset - alignedOffset), destination.Length)
                        .CopyTo(destination);

                    return Result.Success;
                }

                // We couldn't get as large a buffer as we wanted.
                // Shrink the buffer since we only need a single block.
                pooledBuffer.Shrink((int)_dataAlignment);
            }
            else
            {
                // The requested read is larger than we can allocate, so only allocate a single block.
                pooledBuffer.Allocate((int)_dataAlignment, (int)_dataAlignment);
            }
        }

        // Determine read extents for the aligned portion.
        long coreOffset = Alignment.AlignUpPow2(offset, _dataAlignment);
        long coreOffsetEnd = Alignment.AlignDownPow2(offsetEnd, _dataAlignment);

        // Handle any data before the aligned portion.
        if (offset < coreOffset)
        {
            int headSize = (int)(coreOffset - offset);
            Assert.SdkLess(headSize, destination.Length);

            rc = _baseStorage.Read(alignedOffset, pooledBuffer.GetBuffer().Slice(0, (int)_dataAlignment));
            if (rc.IsFailure()) return rc.Miss();

            pooledBuffer.GetBuffer().Slice((int)(offset - alignedOffset), headSize).CopyTo(destination);
        }

        // Handle the aligned portion.
        if (coreOffset < coreOffsetEnd)
        {
            int coreSize = (int)(coreOffsetEnd - coreOffset);
            Span<byte> coreBuffer = destination.Slice((int)(coreOffset - offset), coreSize);

            rc = _baseStorage.Read(coreOffset, coreBuffer);
            if (rc.IsFailure()) return rc.Miss();
        }

        // Handle any data after the aligned portion.
        if (coreOffsetEnd < offsetEnd)
        {
            int tailSize = (int)(offsetEnd - coreOffsetEnd);

            rc = _baseStorage.Read(coreOffsetEnd, pooledBuffer.GetBuffer().Slice(0, (int)_dataAlignment));
            if (rc.IsFailure()) return rc.Miss();

            pooledBuffer.GetBuffer().Slice(0, tailSize).CopyTo(destination.Slice((int)(coreOffsetEnd - offset)));
        }

        return Result.Success;
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        if (source.Length == 0)
            return Result.Success;

        Result rc = GetSize(out long baseStorageSize);
        if (rc.IsFailure()) return rc.Miss();

        rc = CheckAccessRange(offset, source.Length, baseStorageSize);
        if (rc.IsFailure()) return rc.Miss();

        using var pooledBuffer = new PooledBuffer((int)_dataAlignment, (int)_dataAlignment);

        return AlignmentMatchingStorageImpl.Write(_baseStorage, pooledBuffer.GetBuffer(), _dataAlignment, BufferAlign,
            offset, source);
    }

    public override Result Flush()
    {
        return _baseStorage.Flush();
    }

    public override Result SetSize(long size)
    {
        Result rc = _baseStorage.SetSize(Alignment.AlignUpPow2(size, _dataAlignment));
        _baseStorageSize = -1;

        return rc;
    }

    public override Result GetSize(out long size)
    {
        UnsafeHelpers.SkipParamInit(out size);

        if (_baseStorageSize < 0)
        {
            Result rc = _baseStorage.GetSize(out long baseStorageSize);
            if (rc.IsFailure()) return rc.Miss();

            _baseStorageSize = baseStorageSize;
        }

        size = _baseStorageSize;
        return Result.Success;
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        if (operationId == OperationId.InvalidateCache)
        {
            return _baseStorage.OperateRange(OperationId.InvalidateCache, offset, size);
        }

        if (size == 0)
            return Result.Success;

        Result rc = GetSize(out long baseStorageSize);
        if (rc.IsFailure()) return rc.Miss();

        rc = CheckOffsetAndSize(offset, size);
        if (rc.IsFailure()) return rc.Miss();

        long validSize = Math.Min(size, baseStorageSize - offset);
        long alignedOffset = Alignment.AlignDownPow2(offset, _dataAlignment);
        long alignedOffsetEnd = Alignment.AlignUpPow2(offset + validSize, _dataAlignment);
        long alignedSize = alignedOffsetEnd - alignedOffset;

        return _baseStorage.OperateRange(outBuffer, operationId, alignedOffset, alignedSize, inBuffer);
    }
}