using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Crypto;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Util;

namespace LibHac.FsSystem;

/// <summary>
/// Reads and writes to an <see cref="IStorage"/> that's encrypted with AES-CTR-128.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public class AesCtrStorage : IStorage
{
    public static readonly int BlockSize = Aes.BlockSize;
    public static readonly int KeySize = Aes.KeySize128;
    public static readonly int IvSize = Aes.KeySize128;

    private IStorage _baseStorage;
    private Array16<byte> _key;
    private Array16<byte> _iv;

    // LibHac addition: This field goes unused if initialized with a plain IStorage.
    // The original class uses a template for both the shared and non-shared IStorage which avoids needing this field.
    private SharedRef<IStorage> _baseStorageShared;

    public static void MakeIv(Span<byte> outIv, ulong upperIv, long offset)
    {
        Assert.SdkRequiresEqual(outIv.Length, IvSize);
        Assert.SdkRequiresGreaterEqual(offset, 0);

        BinaryPrimitives.WriteUInt64BigEndian(outIv, upperIv);
        BinaryPrimitives.WriteInt64BigEndian(outIv.Slice(sizeof(long)), offset / BlockSize);
    }

    public AesCtrStorage(IStorage baseStorage, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
    {
        Assert.SdkRequiresNotNull(baseStorage);
        Assert.SdkRequiresEqual(key.Length, KeySize);
        Assert.SdkRequiresEqual(iv.Length, IvSize);

        _baseStorage = baseStorage;

        key.CopyTo(_key);
        iv.CopyTo(_iv);
    }

    public AesCtrStorage(in SharedRef<IStorage> baseStorage, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
    {
        Assert.SdkRequiresNotNull(in baseStorage);
        Assert.SdkRequiresEqual(key.Length, KeySize);
        Assert.SdkRequiresEqual(iv.Length, IvSize);

        _baseStorage = baseStorage.Get;
        _baseStorageShared = SharedRef<IStorage>.CreateCopy(in baseStorage);

        key.CopyTo(_key);
        iv.CopyTo(_iv);
    }

    public override void Dispose()
    {
        _baseStorageShared.Destroy();

        base.Dispose();
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        if (destination.Length == 0)
            return Result.Success;

        // Reads cannot contain any partial blocks.
        if (!Alignment.IsAligned(offset, (uint)BlockSize))
            return ResultFs.InvalidArgument.Log();

        if (!Alignment.IsAligned(destination.Length, (uint)BlockSize))
            return ResultFs.InvalidArgument.Log();

        Result res = _baseStorage.Read(offset, destination);
        if (res.IsFailure()) return res.Miss();

        using var changePriority = new ScopedThreadPriorityChanger(1, ScopedThreadPriorityChanger.Mode.Relative);

        Array16<byte> counter = _iv;
        Utility.AddCounter(counter, (ulong)offset / (uint)BlockSize);

        int decSize = Aes.DecryptCtr128(destination, destination, _key, counter);
        if (decSize != destination.Length)
            return ResultFs.UnexpectedInAesCtrStorageA.Log();

        return Result.Success;
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        if (source.Length == 0)
            return Result.Success;

        // We can only write full, aligned blocks.
        if (!Alignment.IsAligned(offset, (uint)BlockSize))
            return ResultFs.InvalidArgument.Log();

        if (!Alignment.IsAligned(source.Length, (uint)BlockSize))
            return ResultFs.InvalidArgument.Log();

        // Get a pooled buffer.
        // Note: The original code will const_cast the input buffer and encrypt the data in-place if the provided
        // buffer is from the pooled buffer heap. This seems very error-prone since the data in buffers you pass
        // as const might unexpectedly be modified. We make IsDeviceAddress() always return false
        // so this won't happen, but the code that does the encryption in-place will be left in as a reference.
        using var pooledBuffer = new PooledBuffer();
        bool useWorkBuffer = PooledBufferGlobalMethods.IsDeviceAddress(source);
        if (useWorkBuffer)
            pooledBuffer.Allocate(source.Length, BlockSize);

        // Setup the counter.
        var counter = new Array16<byte>();
        Utility.AddCounter(counter, (ulong)offset / (uint)BlockSize);

        // Loop until all data is written.
        int remaining = source.Length;
        int currentOffset = 0;

        while (remaining > 0)
        {
            // Determine data we're writing and where.
            int writeSize = useWorkBuffer ? Math.Max(pooledBuffer.GetSize(), remaining) : remaining;
            Span<byte> writeBuffer = useWorkBuffer
                ? pooledBuffer.GetBuffer().Slice(0, writeSize)
                : SpanHelpers.CreateSpan(ref MemoryMarshal.GetReference(source), source.Length).Slice(0, writeSize);

            // Encrypt the data, with temporarily increased priority.
            using (new ScopedThreadPriorityChanger(1, ScopedThreadPriorityChanger.Mode.Relative))
            {
                int encSize = Aes.EncryptCtr128(source.Slice(currentOffset, writeSize), writeBuffer, _key, _iv);
                if (encSize != writeSize)
                    return ResultFs.UnexpectedInAesCtrStorageA.Log();
            }

            // Write the encrypted data.
            Result res = _baseStorage.Write(offset + currentOffset, writeBuffer);
            if (res.IsFailure()) return res.Miss();

            // Advance.
            currentOffset += writeSize;
            remaining -= writeSize;
            if (remaining > 0)
            {
                Utility.AddCounter(counter, (uint)writeSize / (uint)BlockSize);
            }
        }

        return Result.Success;
    }

    public override Result Flush()
    {
        return _baseStorage.Flush();
    }

    public override Result SetSize(long size)
    {
        return ResultFs.UnsupportedSetSizeForAesCtrStorage.Log();
    }

    public override Result GetSize(out long size)
    {
        return _baseStorage.GetSize(out size);
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        if (operationId != OperationId.InvalidateCache)
        {
            if (size == 0)
            {
                if (operationId == OperationId.QueryRange)
                {
                    if (outBuffer.Length != Unsafe.SizeOf<QueryRangeInfo>())
                        return ResultFs.InvalidSize.Log();

                    Unsafe.As<byte, QueryRangeInfo>(ref MemoryMarshal.GetReference(outBuffer)).Clear();
                }

                return Result.Success;
            }

            if (!Alignment.IsAligned(offset, (uint)BlockSize))
                return ResultFs.InvalidArgument.Log();

            if (!Alignment.IsAligned(size, (uint)BlockSize))
                return ResultFs.InvalidArgument.Log();
        }

        switch (operationId)
        {
            case OperationId.QueryRange:
            {
                if (outBuffer.Length != Unsafe.SizeOf<QueryRangeInfo>())
                    return ResultFs.InvalidSize.Log();

                ref QueryRangeInfo outInfo =
                    ref Unsafe.As<byte, QueryRangeInfo>(ref MemoryMarshal.GetReference(outBuffer));

                // Get the QueryRangeInfo of the underlying base storage.
                Result res = _baseStorage.OperateRange(outBuffer, operationId, offset, size, inBuffer);
                if (res.IsFailure()) return res.Miss();

                Unsafe.SkipInit(out QueryRangeInfo info);
                info.Clear();
                info.AesCtrKeyType = (int)QueryRangeInfo.AesCtrKeyTypeFlag.InternalKeyForSoftwareAes;

                outInfo.Merge(in info);

                break;
            }
            default:
            {
                Result res = _baseStorage.OperateRange(outBuffer, operationId, offset, size, inBuffer);
                if (res.IsFailure()) return res.Miss();

                break;
            }
        }

        return Result.Success;
    }
}