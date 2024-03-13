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
/// Reads from an <see cref="IStorage"/> encrypted with AES-CTR-128 using a table of counters.
/// </summary>
/// <remarks><para>The base data used for this storage comes with a table of ranges and counter values that are used
/// to decrypt each range. This encryption scheme is used for encrypting content updates so that no counter values
/// are ever reused.</para>
/// <para>Based on nnSdk 14.3.0 (FS 14.1.0)</para></remarks>
public class AesCtrCounterExtendedStorage : IStorage
{
    public delegate Result DecryptFunction(Span<byte> destination, int index, int generation,
        ReadOnlySpan<byte> encryptedKey, ReadOnlySpan<byte> iv, ReadOnlySpan<byte> source);

    public interface IDecryptor : IDisposable
    {
        Result Decrypt(Span<byte> destination, ReadOnlySpan<byte> encryptedKey, ReadOnlySpan<byte> iv);
        bool HasExternalDecryptionKey();
    }

    public struct Entry
    {
        public Array8<byte> Offset;
        public Encryption EncryptionValue;
        public Array3<byte> Reserved;
        public int Generation;

        public enum Encryption : byte
        {
            Encrypted = 0,
            NotEncrypted = 1
        }

        public void SetOffset(long value)
        {
            BinaryPrimitives.WriteInt64LittleEndian(Offset, value);
        }

        public readonly long GetOffset()
        {
            return BinaryPrimitives.ReadInt64LittleEndian(Offset);
        }
    }

    public static readonly int BlockSize = Aes.BlockSize;
    public static readonly int KeySize = Aes.KeySize128;
    public static readonly int IvSize = Aes.BlockSize;
    public static readonly int NodeSize = 1024 * 16;

    private BucketTree _table;
    private ValueSubStorage _dataStorage;
    private Array16<byte> _key;
    private uint _secureValue;
    private long _counterOffset;
    private UniqueRef<IDecryptor> _decryptor;

    public static long QueryHeaderStorageSize()
    {
        return BucketTree.QueryHeaderStorageSize();
    }

    public static long QueryNodeStorageSize(int entryCount)
    {
        return BucketTree.QueryNodeStorageSize(NodeSize, Unsafe.SizeOf<Entry>(), entryCount);
    }

    public static long QueryEntryStorageSize(int entryCount)
    {
        return BucketTree.QueryEntryStorageSize(NodeSize, Unsafe.SizeOf<Entry>(), entryCount);
    }

    public static Result CreateExternalDecryptor(ref UniqueRef<IDecryptor> outDecryptor,
        DecryptFunction decryptFunction, int keyIndex, int keyGeneration)
    {
        using var decryptor = new UniqueRef<IDecryptor>(new ExternalDecryptor(decryptFunction, keyIndex, keyGeneration));

        if (!decryptor.HasValue)
            return ResultFs.AllocationMemoryFailedInAesCtrCounterExtendedStorageA.Log();

        outDecryptor.Set(ref decryptor.Ref);
        return Result.Success;
    }

    public static Result CreateSoftwareDecryptor(ref UniqueRef<IDecryptor> outDecryptor)
    {
        using var decryptor = new UniqueRef<IDecryptor>(new SoftwareDecryptor());

        if (!decryptor.HasValue)
            return ResultFs.AllocationMemoryFailedInAesCtrCounterExtendedStorageA.Log();

        outDecryptor.Set(ref decryptor.Ref);
        return Result.Success;
    }

    public AesCtrCounterExtendedStorage()
    {
        _table = new BucketTree();
    }

    public override void Dispose()
    {
        FinalizeObject();

        _decryptor.Destroy();
        _dataStorage.Dispose();
        _table.Dispose();

        base.Dispose();
    }

    public bool IsInitialized()
    {
        return _table.IsInitialized();
    }

    // ReSharper disable once UnusedMember.Local
    private Result Initialize(MemoryResource allocator, ReadOnlySpan<byte> key, uint secureValue,
        ref readonly ValueSubStorage dataStorage, ref readonly ValueSubStorage tableStorage)
    {
        Unsafe.SkipInit(out BucketTree.Header header);

        Result res = tableStorage.Read(0, SpanHelpers.AsByteSpan(ref header));
        if (res.IsFailure()) return res.Miss();

        res = header.Verify();
        if (res.IsFailure()) return res.Miss();

        long nodeStorageSize = QueryNodeStorageSize(header.EntryCount);
        long entryStorageSize = QueryEntryStorageSize(header.EntryCount);
        long nodeStorageOffset = QueryHeaderStorageSize();
        long entryStorageOffset = nodeStorageOffset + nodeStorageSize;

        using var decryptor = new UniqueRef<IDecryptor>();
        res = CreateSoftwareDecryptor(ref decryptor.Ref);
        if (res.IsFailure()) return res.Miss();

        res = tableStorage.GetSize(out long storageSize);
        if (res.IsFailure()) return res.Miss();

        if (nodeStorageOffset + nodeStorageSize + entryStorageSize > storageSize)
            return ResultFs.InvalidAesCtrCounterExtendedMetaStorageSize.Log();

        using var entryStorage = new ValueSubStorage(in tableStorage, entryStorageOffset, entryStorageSize);
        using var nodeStorage = new ValueSubStorage(in tableStorage, nodeStorageOffset, nodeStorageSize);

        return Initialize(allocator, key, secureValue, counterOffset: 0, in dataStorage, in nodeStorage,
            in entryStorage, header.EntryCount, ref decryptor.Ref);
    }

    public Result Initialize(MemoryResource allocator, ReadOnlySpan<byte> key, uint secureValue, long counterOffset,
        ref readonly ValueSubStorage dataStorage, ref readonly ValueSubStorage nodeStorage,
        ref readonly ValueSubStorage entryStorage, int entryCount, ref UniqueRef<IDecryptor> decryptor)
    {
        Assert.SdkRequiresEqual(key.Length, KeySize);
        Assert.SdkRequiresGreaterEqual(counterOffset, 0);
        Assert.SdkRequiresNotNull(in decryptor);

        Result res;

        if (entryCount > 0)
        {
            res = _table.Initialize(allocator, in nodeStorage, in entryStorage, NodeSize, Unsafe.SizeOf<Entry>(),
                entryCount);
            if (res.IsFailure()) return res.Miss();
        }
        else
        {
            _table.Initialize(NodeSize, 0);
        }

        res = dataStorage.GetSize(out long dataStorageSize);
        if (res.IsFailure()) return res.Miss();

        res = _table.GetOffsets(out BucketTree.Offsets offsets);
        if (res.IsFailure()) return res.Miss();

        if (offsets.EndOffset > dataStorageSize)
            return ResultFs.InvalidAesCtrCounterExtendedDataStorageSize.Log();

        _dataStorage.Set(in dataStorage);
        key.CopyTo(_key);
        _secureValue = secureValue;
        _counterOffset = counterOffset;
        _decryptor.Set(ref decryptor);

        return Result.Success;
    }

    public void FinalizeObject()
    {
        if (IsInitialized())
        {
            _table.FinalizeObject();

            using var emptyStorage = new ValueSubStorage();
            _dataStorage.Set(in emptyStorage);
        }
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        Assert.SdkRequiresLessEqual(0, offset);
        Assert.SdkRequires(IsInitialized());

        if (destination.Length == 0)
            return Result.Success;

        // Reads cannot contain any partial blocks.
        if (!Alignment.IsAligned(offset, (uint)BlockSize))
            return ResultFs.InvalidOffset.Log();

        if (!Alignment.IsAligned(destination.Length, (uint)BlockSize))
            return ResultFs.InvalidSize.Log();

        // Ensure the the requested range is within the bounds of the table.
        Result res = _table.GetOffsets(out BucketTree.Offsets offsets);
        if (res.IsFailure()) return res.Miss();

        if (!offsets.IsInclude(offset, destination.Length))
            return ResultFs.OutOfRange.Log();

        // Fill the destination buffer with the encrypted data.
        res = _dataStorage.Read(offset, destination);
        if (res.IsFailure()) return res.Miss();

        // Temporarily increase our thread priority.
        using var changePriority = new ScopedThreadPriorityChanger(1, ScopedThreadPriorityChanger.Mode.Relative);

        // Find the entry in the table that contains our current offset.
        using var visitor = new BucketTree.Visitor();
        res = _table.Find(ref visitor.Ref, offset);
        if (res.IsFailure()) return res.Miss();

        // Verify that the entry's offset is aligned to an AES block and within the bounds of the table.
        long entryOffset = visitor.Get<Entry>().GetOffset();
        if (!Alignment.IsAligned(entryOffset, (uint)BlockSize) || entryOffset < 0 ||
            !offsets.IsInclude(entryOffset))
        {
            return ResultFs.InvalidAesCtrCounterExtendedEntryOffset.Log();
        }

        Span<byte> currentData = destination;
        long currentOffset = offset;
        long endOffset = offset + destination.Length;

        while (currentOffset < endOffset)
        {
            // Get the current entry and validate its offset.
            // No need to check its alignment since it was already checked elsewhere.
            var entry = visitor.Get<Entry>();

            long entryStartOffset = entry.GetOffset();
            if (entryStartOffset > currentOffset)
                return ResultFs.InvalidAesCtrCounterExtendedEntryOffset.Log();

            // Get current entry's end offset.
            long entryEndOffset;
            if (visitor.CanMoveNext())
            {
                // Advance to the next entry so we know where our current entry ends.
                // The current entry's end offset is the next entry's start offset.
                res = visitor.MoveNext();
                if (res.IsFailure()) return res.Miss();

                entryEndOffset = visitor.Get<Entry>().GetOffset();
                if (!offsets.IsInclude(entryEndOffset))
                    return ResultFs.InvalidAesCtrCounterExtendedEntryOffset.Log();
            }
            else
            {
                // If this is the last entry its end offset is the table's end offset.
                entryEndOffset = offsets.EndOffset;
            }

            if (!Alignment.IsAligned((ulong)entryEndOffset, (uint)BlockSize) || currentOffset >= entryEndOffset)
                return ResultFs.InvalidAesCtrCounterExtendedEntryOffset.Log();

            // Get the part of the entry that contains the data we read.
            long dataOffset = currentOffset - entryStartOffset;
            long dataSize = entryEndOffset - currentOffset;
            Assert.SdkLess(0, dataSize);

            long remainingSize = endOffset - currentOffset;
            long readSize = Math.Min(remainingSize, dataSize);
            Assert.SdkLessEqual(readSize, destination.Length);

            if (entry.EncryptionValue == Entry.Encryption.Encrypted)
            {
                // Create the counter for the first data block we're decrypting.
                long counterOffset = _counterOffset + entryStartOffset + dataOffset;
                var upperIv = new NcaAesCtrUpperIv
                {
                    Generation = (uint)entry.Generation,
                    SecureValue = _secureValue
                };

                Unsafe.SkipInit(out Array16<byte> counter);
                AesCtrStorage.MakeIv(counter, upperIv.Value, counterOffset);

                // Decrypt the data from the current entry.
                res = _decryptor.Get.Decrypt(currentData.Slice(0, (int)dataSize), _key, counter);
                if (res.IsFailure()) return res.Miss();
            }

            // Advance the current offsets.
            currentData = currentData.Slice((int)dataSize);
            currentOffset -= dataSize;
        }

        return Result.Success;
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        return ResultFs.UnsupportedWriteForAesCtrCounterExtendedStorage.Log();
    }

    public override Result Flush()
    {
        return Result.Success;
    }

    public override Result GetSize(out long size)
    {
        UnsafeHelpers.SkipParamInit(out size);

        Result res = _table.GetOffsets(out BucketTree.Offsets offsets);
        if (res.IsFailure()) return res.Miss();

        size = offsets.EndOffset;
        return Result.Success;
    }

    public override Result SetSize(long size)
    {
        return ResultFs.UnsupportedSetSizeForAesCtrCounterExtendedStorage.Log();
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        switch (operationId)
        {
            case OperationId.InvalidateCache:
            {
                Assert.SdkRequires(IsInitialized());

                // Invalidate the table's cache.
                Result res = _table.InvalidateCache();
                if (res.IsFailure()) return res.Miss();

                // Invalidate the data storage's cache.
                res = _dataStorage.OperateRange(OperationId.InvalidateCache, offset: 0, size: long.MaxValue);
                if (res.IsFailure()) return res.Miss();

                return Result.Success;
            }
            case OperationId.QueryRange:
            {
                Assert.SdkRequiresLessEqual(0, offset);
                Assert.SdkRequires(IsInitialized());

                if (outBuffer.Length != Unsafe.SizeOf<QueryRangeInfo>())
                    return ResultFs.InvalidSize.Log();

                ref QueryRangeInfo outInfo =
                    ref Unsafe.As<byte, QueryRangeInfo>(ref MemoryMarshal.GetReference(outBuffer));

                if (size == 0)
                {
                    outInfo.Clear();
                    return Result.Success;
                }

                if (!Alignment.IsAligned(offset, (uint)BlockSize))
                    return ResultFs.InvalidOffset.Log();

                if (!Alignment.IsAligned(size, (uint)BlockSize))
                    return ResultFs.InvalidSize.Log();

                // Ensure the storage contains the provided offset and size.
                Result res = _table.GetOffsets(out BucketTree.Offsets offsets);
                if (res.IsFailure()) return res.Miss();

                if (!offsets.IsInclude(offset, size))
                    return ResultFs.OutOfRange.Log();

                // Get the QueryRangeInfo of the underlying data storage.
                res = _dataStorage.OperateRange(outBuffer, operationId, offset, size, inBuffer);
                if (res.IsFailure()) return res.Miss();

                // Set the key type in the info and merge it with the output info.
                Unsafe.SkipInit(out QueryRangeInfo info);
                info.Clear();
                info.AesCtrKeyType = (int)(_decryptor.Get.HasExternalDecryptionKey()
                    ? AesCtrKeyTypeFlag.ExternalKeyForHardwareAes
                    : AesCtrKeyTypeFlag.InternalKeyForHardwareAes);

                outInfo.Merge(in info);

                return Result.Success;
            }

            default:
                return ResultFs.UnsupportedOperateRangeForAesCtrCounterExtendedStorage.Log();
        }
    }

    private class ExternalDecryptor : IDecryptor
    {
        private DecryptFunction _decryptFunction;
        private int _keyIndex;
        private int _keyGeneration;

        public ExternalDecryptor(DecryptFunction decryptFunction, int keyIndex, int keyGeneration)
        {
            Assert.SdkRequiresNotNull(decryptFunction);

            _decryptFunction = decryptFunction;
            _keyIndex = keyIndex;
            _keyGeneration = keyGeneration;
        }

        public void Dispose() { }

        public Result Decrypt(Span<byte> destination, ReadOnlySpan<byte> encryptedKey, ReadOnlySpan<byte> iv)
        {
            Assert.SdkRequiresEqual(encryptedKey.Length, KeySize);
            Assert.SdkRequiresEqual(iv.Length, IvSize);

            Unsafe.SkipInit(out Array16<byte> counter);
            iv.CopyTo(counter);

            int remainingSize = destination.Length;
            int currentOffset = 0;

            // Todo: Align the buffer to the block size
            using var pooledBuffer = new PooledBuffer();
            pooledBuffer.AllocateParticularlyLarge(destination.Length, BlockSize);
            Assert.SdkAssert(pooledBuffer.GetSize() > 0);

            while (remainingSize > 0)
            {
                int currentSize = Math.Min(pooledBuffer.GetSize(), remainingSize);
                Span<byte> dstBuffer = destination.Slice(currentOffset, currentSize);
                Span<byte> workBuffer = pooledBuffer.GetBuffer().Slice(0, currentSize);

                Result res = _decryptFunction(workBuffer, _keyIndex, _keyGeneration, encryptedKey, counter, dstBuffer);
                if (res.IsFailure()) return res.Miss();

                workBuffer.CopyTo(dstBuffer);

                currentOffset += currentSize;
                remainingSize -= currentSize;

                if (remainingSize > 0)
                {
                    Utility.AddCounter(counter, (uint)currentSize / (uint)BlockSize);
                }
            }

            return Result.Success;
        }

        public bool HasExternalDecryptionKey()
        {
            return _keyIndex < 0;
        }
    }

    private class SoftwareDecryptor : IDecryptor
    {
        public void Dispose() { }

        public Result Decrypt(Span<byte> destination, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
        {
            Aes.DecryptCtr128(destination, destination, key, iv);
            return Result.Success;
        }

        public bool HasExternalDecryptionKey()
        {
            return false;
        }
    }
}