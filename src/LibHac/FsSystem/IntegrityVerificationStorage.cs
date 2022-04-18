using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Crypto;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Util;

namespace LibHac.FsSystem;

/// <summary>
/// An <see cref="IStorage"/> that can verify the integrity of its data by using a second <see cref="IStorage"/>
/// that contains hash digests of the main data.
/// </summary>
/// <remarks><para>An <see cref="IntegrityVerificationStorage"/> consists of a data <see cref="IStorage"/>
/// and a hash <see cref="IStorage"/>. The main storage is split into blocks of a provided size and the hashes
/// of all these blocks are stored sequentially in the hash storage. Each time a data block is read its hash is
/// also read and used to verify the integrity of the main data.</para>
/// <para>An <see cref="IntegrityVerificationStorage"/> may be writable, updating the hash storage as required
/// when written to. Writable storages have some additional features compared to read-only storages:<br/>
/// If the hash for a data block is all zeros then that block is treated as if the actual data is all zeros.<br/>
/// To avoid collisions in the case where a block's actual hash is all zeros, a certain bit in all writable storage
/// hashes is always set to 1.<br/>
/// An optional <see cref="HashSalt"/> may be provided. This salt will be added to the beginning of each block of
/// data before it is hashed.</para>
/// <para>Based on FS 14.1.0 (nnSdk 14.3.0)</para></remarks>
public class IntegrityVerificationStorage : IStorage
{
    public struct BlockHash
    {
        public Array32<byte> Hash;
    }

    public const int HashSize = Sha256.DigestSize;

    private ValueSubStorage _hashStorage;
    private ValueSubStorage _dataStorage;
    private int _verificationBlockSize;
    private int _verificationBlockOrder;
    private int _upperLayerVerificationBlockSize;
    private int _upperLayerVerificationBlockOrder;
    private IBufferManager _bufferManager;
    private Optional<HashSalt> _hashSalt;
    private bool _isRealData;
    private IHash256GeneratorFactory _hashGeneratorFactory;
    private bool _isWritable;
    private bool _allowClearedBlocks;

    public IntegrityVerificationStorage()
    {
        _hashStorage = new ValueSubStorage();
        _dataStorage = new ValueSubStorage();
    }

    public override void Dispose()
    {
        FinalizeObject();
        _dataStorage.Dispose();
        _hashStorage.Dispose();

        base.Dispose();
    }

    /// <summary>
    /// Sets the validation bit on the provided <see cref="BlockHash"/>. The hashes of all writable blocks
    /// have a certain bit set so we can tell the difference between a cleared block and a hash of all zeros.
    /// </summary>
    /// <param name="hash">The <see cref="BlockHash"/> to have its validation bit set.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetValidationBit(ref BlockHash hash)
    {
        hash.Hash.Items[HashSize - 1] |= 0x80;
    }

    /// <summary>
    /// Checks if the provided <see cref="BlockHash"/> has its validation bit set. The hashes of all writable blocks
    /// have a certain bit set so we can tell the difference between a cleared block and a hash of all zeros.
    /// </summary>
    /// <param name="hash">The <see cref="BlockHash"/> to check.</param>
    /// <returns><see langword="true"/> if the validation bit is set; otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidationBit(in BlockHash hash)
    {
        return (hash.Hash.ItemsRo[HashSize - 1] & 0x80) != 0;
    }

    public int GetBlockSize()
    {
        return _verificationBlockSize;
    }

    public void Initialize(in ValueSubStorage hashStorage, in ValueSubStorage dataStorage, int sizeVerificationBlock,
        int sizeUpperLayerVerificationBlock, IBufferManager bufferManager,
        IHash256GeneratorFactory hashGeneratorFactory, in Optional<HashSalt> hashSalt, bool isRealData, bool isWritable,
        bool allowClearedBlocks)
    {
        Assert.SdkRequiresGreaterEqual(sizeVerificationBlock, HashSize);
        Assert.SdkRequiresNotNull(bufferManager);
        Assert.SdkRequiresNotNull(hashGeneratorFactory);

        _hashStorage.Set(in hashStorage);
        _dataStorage.Set(in dataStorage);

        _hashGeneratorFactory = hashGeneratorFactory;

        _verificationBlockSize = sizeVerificationBlock;
        _verificationBlockOrder = BitmapUtils.ILog2((uint)sizeVerificationBlock);
        Assert.SdkRequiresEqual(1 << _verificationBlockOrder, _verificationBlockSize);

        _bufferManager = bufferManager;

        sizeUpperLayerVerificationBlock = Math.Max(sizeUpperLayerVerificationBlock, HashSize);
        _upperLayerVerificationBlockSize = sizeUpperLayerVerificationBlock;
        _upperLayerVerificationBlockOrder = BitmapUtils.ILog2((uint)sizeUpperLayerVerificationBlock);
        Assert.SdkRequiresEqual(1 << _upperLayerVerificationBlockOrder, _upperLayerVerificationBlockSize);

        Assert.SdkAssert(_dataStorage.GetSize(out long dataSize).IsSuccess());
        Assert.SdkAssert(_hashStorage.GetSize(out long hashSize).IsSuccess());
        Assert.SdkAssert(hashSize / HashSize * _verificationBlockSize >= dataSize);

        _hashSalt = hashSalt;

        _isRealData = isRealData;
        _isWritable = isWritable;
        _allowClearedBlocks = allowClearedBlocks;
    }

    public void FinalizeObject()
    {
        if (_bufferManager is not null)
        {
            using (var emptySubStorage = new ValueSubStorage())
            {
                _hashStorage.Set(in emptySubStorage);
            }

            using (var emptySubStorage = new ValueSubStorage())
            {
                _dataStorage.Set(in emptySubStorage);
            }

            _bufferManager = null;
        }
    }

    public override Result GetSize(out long size)
    {
        return _dataStorage.GetSize(out size);
    }

    public override Result SetSize(long size)
    {
        return ResultFs.UnsupportedSetSizeForIntegrityVerificationStorage.Log();
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        Assert.SdkRequiresNotEqual(0, destination.Length);

        Assert.SdkRequiresAligned(offset, _verificationBlockSize);
        Assert.SdkRequiresAligned(destination.Length, _verificationBlockSize);

        if (destination.Length == 0)
            return Result.Success;

        Result rc = _dataStorage.GetSize(out long dataSize);
        if (rc.IsFailure()) return rc.Miss();

        if (dataSize < offset)
            return ResultFs.InvalidOffset.Log();

        long alignedDataSize = Alignment.AlignUpPow2(dataSize, (uint)_verificationBlockSize);
        rc = CheckAccessRange(offset, destination.Length, alignedDataSize);
        if (rc.IsFailure()) return rc.Miss();

        int readSize = destination.Length;
        if (offset + readSize > dataSize)
        {
            // All reads to this storage must be aligned to the block size, but if the last data block is a partial block
            // it will not be written to the base data storage. If that's the case, fill the unused portion of the block
            // with zeros. The hash for the partial block is calculated using the padded, complete block.
            int paddingOffset = (int)(dataSize - offset);
            int paddingSize = _verificationBlockSize - (paddingOffset & (_verificationBlockSize - 1));
            Assert.SdkLess(paddingSize, _verificationBlockSize);

            // Clear the padding.
            destination.Slice(paddingOffset, paddingSize).Clear();

            // Set the new in-bounds size.
            readSize = (int)(dataSize - offset);
        }

        // Read all of the data to be validated.
        rc = _dataStorage.Read(offset, destination.Slice(0, readSize));
        if (rc.IsFailure())
        {
            destination.Clear();
            return rc.Log();
        }

        // Validate the hashes of the read data blocks.
        Result verifyHashResult = Result.Success;

        using var hashGenerator = new UniqueRef<IHash256Generator>();
        rc = _hashGeneratorFactory.Create(ref hashGenerator.Ref());
        if (rc.IsFailure()) return rc.Miss();

        int signatureCount = destination.Length >> _verificationBlockOrder;
        using var signatureBuffer =
            new PooledBuffer(signatureCount * Unsafe.SizeOf<BlockHash>(), Unsafe.SizeOf<BlockHash>());
        int bufferCount = (int)Math.Min(signatureCount, signatureBuffer.GetSize() / (uint)Unsafe.SizeOf<BlockHash>());

        // Loop over each block while validating their signatures
        int verifiedCount = 0;
        while (verifiedCount < signatureCount)
        {
            int currentCount = Math.Min(bufferCount, signatureCount - verifiedCount);

            Result currentResult = ReadBlockSignature(signatureBuffer.GetBuffer(),
                offset + (verifiedCount << _verificationBlockOrder), currentCount << _verificationBlockOrder);

            using var changePriority = new ScopedThreadPriorityChanger(1, ScopedThreadPriorityChanger.Mode.Relative);

            for (int i = 1; i < currentCount && currentResult.IsSuccess(); i++)
            {
                int verifiedSize = (verifiedCount + i) << _verificationBlockOrder;
                ref BlockHash blockHash = ref signatureBuffer.GetBuffer<BlockHash>()[i];
                currentResult = VerifyHash(destination.Slice(verifiedCount), ref blockHash, in hashGenerator);

                if (ResultFs.IntegrityVerificationStorageCorrupted.Includes(currentResult))
                {
                    // Don't output the corrupted block to the destination buffer
                    destination.Slice(verifiedSize, _verificationBlockSize).Clear();

                    if (!ResultFs.ClearedRealDataVerificationFailed.Includes(currentResult) && !_allowClearedBlocks)
                    {
                        verifyHashResult = currentResult;
                    }

                    currentResult = Result.Success;
                }
            }

            if (currentResult.IsFailure())
            {
                destination.Clear();
                return currentResult;
            }

            verifiedCount += currentCount;
        }

        return verifyHashResult;
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        if (source.Length == 0)
            return Result.Success;

        Result rc = CheckOffsetAndSize(offset, source.Length);
        if (rc.IsFailure()) return rc.Miss();

        rc = _dataStorage.GetSize(out long dataSize);
        if (rc.IsFailure()) return rc.Miss();

        if (offset >= dataSize)
            return ResultFs.InvalidOffset.Log();

        rc = CheckAccessRange(offset, source.Length, Alignment.AlignUpPow2(dataSize, (uint)_verificationBlockSize));
        if (rc.IsFailure()) return rc.Miss();

        Assert.SdkRequiresAligned(offset, _verificationBlockSize);
        Assert.SdkRequiresAligned(source.Length, _verificationBlockSize);
        Assert.SdkLessEqual(offset, dataSize);
        Assert.SdkLess(offset + source.Length, dataSize + _verificationBlockSize);

        // When writing to a partial final block, the data past the end of the partial block should be all zeros.
        if (offset + source.Length > dataSize)
        {
            Assert.SdkAssert(source.Slice((int)(dataSize - offset)).IsZeros());
        }

        // Determine the size of the unpadded data we're writing to the base data storage
        int writeSize = source.Length;
        if (offset + writeSize > dataSize)
        {
            writeSize = (int)(dataSize - offset);

            if (writeSize == 0)
                return Result.Success;
        }

        int alignedWriteSize = Alignment.AlignUpPow2(writeSize, (uint)_verificationBlockSize);

        Result updateResult = Result.Success;
        int updatedSignatureCount = 0;
        {
            int signatureCount = alignedWriteSize >> _verificationBlockOrder;

            using var signatureBuffer =
                new PooledBuffer(signatureCount * Unsafe.SizeOf<BlockHash>(), Unsafe.SizeOf<BlockHash>());
            int bufferCount = Math.Min(signatureCount, signatureBuffer.GetSize() / Unsafe.SizeOf<BlockHash>());

            using var hashGenerator = new UniqueRef<IHash256Generator>();
            rc = _hashGeneratorFactory.Create(ref hashGenerator.Ref());
            if (rc.IsFailure()) return rc.Miss();

            while (updatedSignatureCount < signatureCount)
            {
                int remainingCount = signatureCount - updatedSignatureCount;
                int currentCount = Math.Min(bufferCount, remainingCount);

                using (new ScopedThreadPriorityChanger(1, ScopedThreadPriorityChanger.Mode.Relative))
                {
                    // Calculate the new hashes for the current set of blocks
                    for (int i = 0; i < currentCount; i++)
                    {
                        int updatedSize = (updatedSignatureCount + i) << _verificationBlockOrder;
                        CalcBlockHash(out signatureBuffer.GetBuffer<BlockHash>()[i], source.Slice(updatedSize),
                            in hashGenerator);
                    }
                }

                // Write the new block signatures.
                updateResult = WriteBlockSignature(signatureBuffer.GetBuffer(),
                    offset: offset + (updatedSignatureCount << _verificationBlockOrder),
                    size: currentCount << _verificationBlockOrder);

                if (updateResult.IsFailure())
                    break;

                updatedSignatureCount += currentCount;
            }
        }

        // The updated hash values have all been written. Now write the actual data.
        // If there was an error writing the updated hashes, only the data for the blocks that were
        // successfully updated will be written.
        int dataWriteSize = Math.Min(writeSize, updatedSignatureCount << _verificationBlockOrder);
        rc = _dataStorage.Write(offset, source.Slice(0, dataWriteSize));
        if (rc.IsFailure()) return rc.Miss();

        return updateResult;
    }

    public override Result Flush()
    {
        Result rc = _hashStorage.Flush();
        if (rc.IsFailure()) return rc.Miss();

        rc = _dataStorage.Flush();
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        if (operationId != OperationId.InvalidateCache)
        {
            Assert.SdkRequiresAligned(offset, _verificationBlockSize);
            Assert.SdkRequiresAligned(size, _verificationBlockSize);
        }

        switch (operationId)
        {
            case OperationId.FillZero:
            {
                Assert.SdkRequires(_isWritable);

                Result rc = _dataStorage.GetSize(out long dataStorageSize);
                if (rc.IsFailure()) return rc.Miss();

                if (offset < 0 || dataStorageSize < offset)
                    return ResultFs.InvalidOffset.Log();

                // Get the range of the signatures for the blocks that will be cleared
                long signOffset = (offset >> _verificationBlockOrder) * Unsafe.SizeOf<BlockHash>();
                long signSize = Math.Min(size, dataStorageSize - offset) * Unsafe.SizeOf<BlockHash>();

                // Allocate a work buffer up to 4 times the size of the hash storage's verification block size.
                int bufferSize = (int)Math.Min(signSize, 1 << (_upperLayerVerificationBlockOrder + 2));
                using var workBuffer = new RentedArray<byte>(bufferSize);
                if (workBuffer.Array is null)
                    return ResultFs.AllocationMemoryFailedInIntegrityVerificationStorageA.Log();

                workBuffer.Span.Clear();

                long remainingSize = signSize;

                // Clear the hash storage in chunks.
                while (remainingSize > 0)
                {
                    int currentSize = (int)Math.Min(remainingSize, bufferSize);

                    rc = _hashStorage.Write(signOffset + signSize - remainingSize,
                        workBuffer.Span.Slice(0, currentSize));
                    if (rc.IsFailure()) return rc.Miss();

                    remainingSize -= currentSize;
                }

                return Result.Success;
            }
            case OperationId.DestroySignature:
            {
                Assert.SdkRequires(_isWritable);

                Result rc = _dataStorage.GetSize(out long dataStorageSize);
                if (rc.IsFailure()) return rc.Miss();

                if (offset < 0 || dataStorageSize < offset)
                    return ResultFs.InvalidOffset.Log();

                // Get the range of the signatures for the blocks that will be cleared
                long signOffset = (offset >> _verificationBlockOrder) * Unsafe.SizeOf<BlockHash>();
                long signSize = Math.Min(size, dataStorageSize - offset) * Unsafe.SizeOf<BlockHash>();

                using var workBuffer = new RentedArray<byte>((int)signSize);
                if (workBuffer.Array is null)
                    return ResultFs.AllocationMemoryFailedInIntegrityVerificationStorageB.Log();

                // Read the existing signature.
                rc = _hashStorage.Read(signOffset, workBuffer.Span);
                if (rc.IsFailure()) return rc.Miss();

                // Clear the signature.
                // This flips all bits, leaving the verification bit cleared.
                for (int i = 0; i < workBuffer.Span.Length; i++)
                {
                    workBuffer.Span[i] ^= (byte)((i + 1) % (uint)HashSize == 0 ? 0x7F : 0xFF);
                }

                // Write the cleared signature.
                return _hashStorage.Write(signOffset, workBuffer.Span);
            }
            case OperationId.InvalidateCache:
            {
                // Only allow cache invalidation for read-only storages.
                if (_isWritable)
                    return ResultFs.UnsupportedOperateRangeForWritableIntegrityVerificationStorage.Log();

                Result rc = _hashStorage.OperateRange(operationId, 0, long.MaxValue);
                if (rc.IsFailure()) return rc.Miss();

                rc = _dataStorage.OperateRange(operationId, offset, size);
                if (rc.IsFailure()) return rc.Miss();

                return Result.Success;
            }
            case OperationId.QueryRange:
            {
                Result rc = _dataStorage.GetSize(out long dataStorageSize);
                if (rc.IsFailure()) return rc.Miss();

                if (offset < 0 || dataStorageSize < offset)
                    return ResultFs.InvalidOffset.Log();

                long actualSize = Math.Min(size, dataStorageSize - offset);
                rc = _dataStorage.OperateRange(outBuffer, operationId, offset, actualSize, inBuffer);
                if (rc.IsFailure()) return rc.Miss();

                return Result.Success;
            }
            default:
                return ResultFs.UnsupportedOperateRangeForIntegrityVerificationStorage.Log();
        }
    }

    private Result ReadBlockSignature(Span<byte> destination, long offset, int size)
    {
        Assert.SdkRequiresAligned(offset, _verificationBlockSize);
        Assert.SdkRequiresAligned(size, _verificationBlockSize);

        // Calculate the range that contains the signatures.
        long offsetSignData = (offset >> _verificationBlockOrder) * HashSize;
        long sizeSignData = (size >> _verificationBlockOrder) * HashSize;
        Assert.SdkGreaterEqual(destination.Length, sizeSignData);

        // Validate the hash storage contains the calculated range.
        Result rc = _hashStorage.GetSize(out long sizeHash);
        if (rc.IsFailure()) return rc.Miss();

        Assert.SdkLessEqual(offsetSignData + sizeSignData, sizeHash);

        if (offsetSignData + sizeSignData > sizeHash)
            return ResultFs.OutOfRange.Log();

        // Read the signature.
        rc = _hashStorage.Read(offsetSignData, destination.Slice(0, (int)sizeSignData));
        if (rc.IsFailure())
        {
            // Clear any read signature data if something goes wrong.
            destination.Slice(0, (int)sizeSignData);
            return rc.Miss();
        }

        return Result.Success;
    }

    private Result WriteBlockSignature(ReadOnlySpan<byte> source, long offset, int size)
    {
        Assert.SdkRequiresAligned(offset, _verificationBlockSize);

        long offsetSignData = (offset >> _verificationBlockOrder) * HashSize;
        long sizeSignData = (size >> _verificationBlockOrder) * HashSize;
        Assert.SdkGreaterEqual(source.Length, sizeSignData);

        Result rc = _hashStorage.Write(offsetSignData, source.Slice(0, (int)sizeSignData));
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public Result CalcBlockHash(out BlockHash outHash, ReadOnlySpan<byte> buffer, int verificationBlockSize)
    {
        UnsafeHelpers.SkipParamInit(out outHash);

        using var hashGenerator = new UniqueRef<IHash256Generator>();
        Result rc = _hashGeneratorFactory.Create(ref hashGenerator.Ref());
        if (rc.IsFailure()) return rc.Miss();

        CalcBlockHash(out outHash, buffer, verificationBlockSize, in hashGenerator);
        return Result.Success;
    }

    private void CalcBlockHash(out BlockHash outHash, ReadOnlySpan<byte> buffer,
        in UniqueRef<IHash256Generator> hashGenerator)
    {
        CalcBlockHash(out outHash, buffer, _verificationBlockSize, in hashGenerator);
    }

    private void CalcBlockHash(out BlockHash outHash, ReadOnlySpan<byte> buffer, int verificationBlockSize,
        in UniqueRef<IHash256Generator> hashGenerator)
    {
        UnsafeHelpers.SkipParamInit(out outHash);

        if (_isWritable)
        {
            if (_hashSalt.HasValue)
            {
                // Calculate the hash using the salt if enabled.
                hashGenerator.Get.Initialize();
                hashGenerator.Get.Update(_hashSalt.ValueRo.HashRo);

                hashGenerator.Get.Update(buffer.Slice(0, verificationBlockSize));
                hashGenerator.Get.GetHash(SpanHelpers.AsByteSpan(ref outHash));
            }
            else
            {
                // Otherwise calculate the hash of just the data.
                _hashGeneratorFactory.GenerateHash(SpanHelpers.AsByteSpan(ref outHash),
                    buffer.Slice(0, verificationBlockSize));
            }

            // The hashes of all writable blocks have the validation bit set.
            SetValidationBit(ref outHash);
        }
        else
        {
            // Nothing special needed for read-only blocks. Just calculate the hash.
            _hashGeneratorFactory.GenerateHash(SpanHelpers.AsByteSpan(ref outHash),
                buffer.Slice(0, verificationBlockSize));
        }
    }

    private Result VerifyHash(ReadOnlySpan<byte> buffer, ref BlockHash hash,
        in UniqueRef<IHash256Generator> hashGenerator)
    {
        Assert.SdkRequiresGreaterEqual(buffer.Length, HashSize);

        // Writable storages allow using an all-zeros hash to indicate an empty block.
        if (_isWritable)
        {
            Result rc = IsCleared(out bool isCleared, in hash);
            if (rc.IsFailure()) return rc.Miss();

            if (isCleared)
                return ResultFs.ClearedRealDataVerificationFailed.Log();
        }

        CalcBlockHash(out BlockHash actualHash, buffer, hashGenerator);

        if (!CryptoUtil.IsSameBytes(SpanHelpers.AsReadOnlyByteSpan(in hash),
                SpanHelpers.AsReadOnlyByteSpan(in actualHash), Unsafe.SizeOf<BlockHash>()))
        {
            hash = default;

            if (_isRealData)
            {
                return ResultFs.UnclearedRealDataVerificationFailed.Log();
            }
            else
            {
                return ResultFs.NonRealDataVerificationFailed.Log();
            }
        }

        return Result.Success;
    }

    private Result IsCleared(out bool isCleared, in BlockHash hash)
    {
        Assert.SdkRequires(_isWritable);

        isCleared = false;

        if (IsValidationBit(in hash))
            return Result.Success;

        for (int i = 0; i < hash.Hash.ItemsRo.Length; i++)
        {
            if (hash.Hash.ItemsRo[i] != 0)
                return ResultFs.InvalidZeroHash.Log();
        }

        isCleared = true;
        return Result.Success;
    }
}