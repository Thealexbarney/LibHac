using System;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Crypto;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Util;

namespace LibHac.FsSystem;

/// <summary>
/// Reads and writes to an <see cref="IStorage"/> that's encrypted with AES-XTS-128.
/// All encryption or decryption will be done externally via a provided function.
/// This allows for using hardware decryption where the FS process doesn't has access to the actual keys.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
public class AesXtsStorageExternal : IStorage
{
    public const int AesBlockSize = Aes.BlockSize;
    public const int KeySize = Aes.KeySize128;
    public const int IvSize = Aes.KeySize128;

    private IStorage _baseStorage;
    private Array2<Array16<byte>> _key;
    private Array16<byte> _iv;
    private uint _blockSize;
    private CryptAesXtsFunction _encryptFunction;
    private CryptAesXtsFunction _decryptFunction;

    // The original class uses a template for both the shared and non-shared IStorage which avoids needing this field.
    private SharedRef<IStorage> _baseStorageShared;

    public AesXtsStorageExternal(IStorage baseStorage, ReadOnlySpan<byte> key1, ReadOnlySpan<byte> key2,
        ReadOnlySpan<byte> iv, uint blockSize, CryptAesXtsFunction encryptFunction, CryptAesXtsFunction decryptFunction)
    {
        _baseStorage = baseStorage;
        _blockSize = blockSize;
        _encryptFunction = encryptFunction;
        _decryptFunction = decryptFunction;

        Assert.SdkRequires(key1.Length is 0 or KeySize);
        Assert.SdkRequires(key2.Length is 0 or KeySize);
        Assert.SdkRequiresEqual(IvSize, iv.Length);
        Assert.SdkRequiresAligned(blockSize, AesBlockSize);

        if (key1.Length != 0)
            key1.CopyTo(_key[0].Items);

        if (key2.Length != 0)
            key2.CopyTo(_key[1].Items);

        iv.CopyTo(_iv.Items);
    }

    public AesXtsStorageExternal(in SharedRef<IStorage> baseStorage, ReadOnlySpan<byte> key1, ReadOnlySpan<byte> key2,
        ReadOnlySpan<byte> iv, uint blockSize, CryptAesXtsFunction encryptFunction, CryptAesXtsFunction decryptFunction)
        : this(baseStorage.Get, key1, key2, iv, blockSize, encryptFunction, decryptFunction)
    {
        _baseStorageShared = SharedRef<IStorage>.CreateCopy(in baseStorage);
    }

    public override void Dispose()
    {
        _baseStorageShared.Destroy();

        base.Dispose();
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        // Allow zero size.
        if (destination.Length == 0)
            return Result.Success;

        // Ensure we can decrypt.
        if (_decryptFunction is null)
            return ResultFs.NullptrArgument.Log();

        // We can only read at block aligned offsets.
        if (!Alignment.IsAlignedPow2(offset, AesBlockSize))
            return ResultFs.InvalidArgument.Log();

        if (!Alignment.IsAlignedPow2(destination.Length, AesBlockSize))
            return ResultFs.InvalidArgument.Log();

        // Read the encrypted data.
        Result res = _baseStorage.Read(offset, destination);
        if (res.IsFailure()) return res.Miss();

        // Temporarily increase our thread priority while decrypting.
        using var changePriority = new ScopedThreadPriorityChanger(1, ScopedThreadPriorityChanger.Mode.Relative);

        // Setup the counter.
        Span<byte> counter = stackalloc byte[IvSize];
        _iv.ItemsRo.CopyTo(counter);
        Utility.AddCounter(counter, (ulong)offset / _blockSize);

        // Handle any unaligned data before the start.
        int processedSize = 0;
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        if (offset % _blockSize != 0)
        {
            // Determine the size of the pre-data read.
            int skipSize = (int)(offset - Alignment.AlignDownPow2(offset, _blockSize));
            int dataSize = (int)Math.Min(destination.Length, _blockSize - skipSize);

            // Decrypt into a pooled buffer.
            using (var tmpBuffer = new PooledBuffer((int)_blockSize, (int)_blockSize))
            {
                Assert.SdkAssert(tmpBuffer.GetSize() >= _blockSize);

                tmpBuffer.GetBuffer().Slice(0, skipSize).Clear();
                destination.Slice(0, dataSize).CopyTo(tmpBuffer.GetBuffer().Slice(skipSize, dataSize));
                Span<byte> decryptionBuffer = tmpBuffer.GetBuffer().Slice(0, (int)_blockSize);

                // Decrypt and copy the partial block to the output buffer.
                res = _decryptFunction(decryptionBuffer, _key[0], _key[1], counter, decryptionBuffer);
                if (res.IsFailure()) return res.Miss();

                tmpBuffer.GetBuffer().Slice(skipSize, dataSize).CopyTo(destination);
            }

            Utility.AddCounter(counter, 1);
            processedSize += dataSize;
            Assert.SdkAssert(processedSize == Math.Min(destination.Length, _blockSize - skipSize));
        }

        // Decrypt aligned chunks.
        Span<byte> currentOutput = destination.Slice(processedSize);
        int remainingSize = destination.Length - processedSize;

        while (remainingSize > 0)
        {
            Span<byte> currentBlock = currentOutput.Slice(0, Math.Min((int)_blockSize, remainingSize));

            res = _decryptFunction(currentBlock, _key[0], _key[1], counter, currentBlock);
            if (res.IsFailure()) return res.Miss();

            remainingSize -= currentBlock.Length;
            currentOutput = currentBlock.Slice(currentBlock.Length);

            Utility.AddCounter(counter, 1);
        }

        return Result.Success;
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        Result res;

        // Allow zero-size writes.
        if (source.Length == 0)
            return Result.Success;

        // Ensure we can encrypt.
        if (_encryptFunction is null)
            return ResultFs.NullptrArgument.Log();

        // We can only write at block aligned offsets.
        if (!Alignment.IsAlignedPow2(offset, AesBlockSize))
            return ResultFs.InvalidArgument.Log();

        if (!Alignment.IsAlignedPow2(source.Length, AesBlockSize))
            return ResultFs.InvalidArgument.Log();

        // Get a pooled buffer.
        using var pooledBuffer = new PooledBuffer();
        bool useWorkBuffer = !PooledBufferGlobalMethods.IsDeviceAddress(source);
        if (useWorkBuffer)
        {
            pooledBuffer.Allocate(source.Length, (int)_blockSize);
        }

        // Setup the counter.
        Span<byte> counter = stackalloc byte[IvSize];
        _iv.ItemsRo.CopyTo(counter);
        Utility.AddCounter(counter, (ulong)offset / _blockSize);

        // Handle any unaligned data before the start.
        int processedSize = 0;

        // Todo: remove when fixed in Resharper
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        if (offset % _blockSize != 0)
        {
            // Determine the size of the pre-data write.
            int skipSize = (int)(offset - Alignment.AlignDownPow2(offset, _blockSize));
            int dataSize = (int)Math.Min(source.Length, _blockSize - skipSize);

            // Encrypt into a pooled buffer.
            // Note: Nintendo allocates a second pooled buffer here despite having one already allocated above.
            using (var tmpBuffer = new PooledBuffer((int)_blockSize, (int)_blockSize))
            {
                Assert.SdkAssert(tmpBuffer.GetSize() >= _blockSize);

                tmpBuffer.GetBuffer().Slice(0, skipSize).Clear();
                source.Slice(0, dataSize).CopyTo(tmpBuffer.GetBuffer().Slice(skipSize, dataSize));
                Span<byte> encryptionBuffer = tmpBuffer.GetBuffer().Slice(0, (int)_blockSize);

                res = _encryptFunction(encryptionBuffer, _key[0], _key[1], counter, encryptionBuffer);
                if (res.IsFailure()) return res.Miss();

                res = _baseStorage.Write(offset, tmpBuffer.GetBuffer().Slice(skipSize, dataSize));
                if (res.IsFailure()) return res.Miss();
            }

            Utility.AddCounter(counter, 1);
            processedSize += dataSize;
            Assert.SdkAssert(processedSize == Math.Min(source.Length, _blockSize - skipSize));
        }

        // Encrypt aligned chunks.
        int remainingSize = source.Length - processedSize;
        long currentOffset = offset + processedSize;

        while (remainingSize > 0)
        {
            // Determine data we're writing and where.
            int writeSize = useWorkBuffer ? Math.Min(pooledBuffer.GetSize(), remainingSize) : remainingSize;

            // Encrypt the data with temporarily increased priority.
            using (new ScopedThreadPriorityChanger(1, ScopedThreadPriorityChanger.Mode.Relative))
            {
                int remainingEncryptSize = writeSize;
                int encryptOffset = 0;

                // Encrypt one block at a time.
                while (remainingEncryptSize > 0)
                {
                    int currentSize = Math.Min(remainingEncryptSize, (int)_blockSize);
                    ReadOnlySpan<byte> encryptSource = source.Slice(processedSize + encryptOffset, currentSize);

                    // const_cast the input buffer and encrypt in-place if it's a "device buffer".
                    Span<byte> encryptDest = useWorkBuffer
                        ? pooledBuffer.GetBuffer().Slice(encryptOffset, currentSize)
                        : MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(encryptSource), encryptSource.Length);

                    res = _encryptFunction(encryptDest, _key[0], _key[1], counter, encryptSource);
                    if (res.IsFailure()) return res.Miss();

                    Utility.AddCounter(counter, 1);

                    encryptOffset += currentSize;
                    remainingEncryptSize -= currentSize;
                }
            }

            // Write the encrypted data.
            ReadOnlySpan<byte> writeBuffer = useWorkBuffer
                ? pooledBuffer.GetBuffer().Slice(0, writeSize)
                : source.Slice(processedSize, writeSize);

            res = _baseStorage.Write(currentOffset, writeBuffer);
            if (res.IsFailure()) return res.Miss();

            // Advance.
            currentOffset += writeSize;
            processedSize += writeSize;
            remainingSize -= writeSize;
        }

        return Result.Success;
    }

    public override Result Flush()
    {
        return _baseStorage.Flush();
    }

    public override Result SetSize(long size)
    {
        return _baseStorage.SetSize(size);
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
            // Handle the zero size case.
            if (size == 0)
                return Result.Success;

            // Ensure alignment.
            if (!Alignment.IsAlignedPow2(offset, AesBlockSize))
                return ResultFs.InvalidArgument.Log();

            if (!Alignment.IsAlignedPow2(size, AesBlockSize))
                return ResultFs.InvalidArgument.Log();
        }

        Result res = _baseStorage.OperateRange(outBuffer, operationId, offset, size, inBuffer);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }
}