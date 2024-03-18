using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Crypto;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Os;
using LibHac.Util;

namespace LibHac.FsSystem;

file static class Anonymous
{
    public static int Log2(int value)
    {
        Assert.SdkRequiresGreater(value, 0);
        Assert.SdkRequires(BitUtil.IsPowerOfTwo(value));

        int log = 0;
        while ((value >>= 1) > 0)
        {
            log++;
        }

        return log;
    }
}

/// <summary>
/// Read and writes the storages generally used by code filesystems in NCAs. These use a Merkle tree to verify the
/// integrity of the data.
/// </summary>
/// <remarks>Based on nnSdk 17.5.0 (FS 17.0.0)</remarks>
public class HierarchicalSha256Storage : IStorage
{
    private const int LayerCount = 3;
    private const int HashSize = Sha256.DigestSize;

    private ValueSubStorage _baseStorage;
    private long _baseStorageSize;
    private Memory<byte> _hashBuffer;
    private int _hashTargetBlockSize;
    private int _log2SizeRatio;
    private IHash256GeneratorFactory _hashGeneratorFactory;
    private SdkMutexType _mutex;

    [InlineArray(LayerCount), NonCopyableDisposable]
    public struct BaseStorages : IDisposable
    {
        private ValueSubStorage _0;

        public void Dispose()
        {
            for (int i = 0; i < LayerCount; i++)
            {
                this[i].Dispose();
            }
        }
    }

    public HierarchicalSha256Storage()
    {
        _baseStorage = new ValueSubStorage();
        _mutex = new SdkMutexType();
    }

    public override void Dispose()
    {
        _baseStorage.Dispose();
        base.Dispose();
    }

    public Result Initialize(ref readonly BaseStorages baseStorages, int layerCount, uint hashTargetBlockSize,
        Memory<byte> hashBuffer, IHash256GeneratorFactory hashGeneratorFactory)
    {
        Assert.SdkRequiresEqual(layerCount, LayerCount);
        Assert.SdkRequires(BitUtil.IsPowerOfTwo(hashTargetBlockSize));
        Assert.SdkRequiresNotNull(hashGeneratorFactory);

        _hashTargetBlockSize = (int)hashTargetBlockSize;
        _log2SizeRatio = Anonymous.Log2((int)(hashTargetBlockSize / HashSize));
        _hashGeneratorFactory = hashGeneratorFactory;

        Result res = baseStorages[2].GetSize(out _baseStorageSize);
        if (res.IsFailure()) return res.Miss();

        if (_baseStorageSize > HashSize << _log2SizeRatio << _log2SizeRatio)
        {
            _baseStorageSize = 0;
            return ResultFs.HierarchicalSha256BaseStorageTooLarge.Log();
        }

        _hashBuffer = hashBuffer;
        _baseStorage.Set(in baseStorages[2]);

        Span<byte> masterHash = stackalloc byte[HashSize];
        res = baseStorages[0].Read(0, masterHash);
        if (res.IsFailure()) return res.Miss();

        res = baseStorages[1].GetSize(out long hashStorageSize);
        if (res.IsFailure()) return res.Miss();

        Assert.SdkRequiresAligned(hashStorageSize, HashSize);
        Assert.SdkRequiresLessEqual(hashStorageSize, _hashTargetBlockSize);
        Assert.SdkRequiresLessEqual(hashStorageSize, hashBuffer.Length);

        Span<byte> buffer = _hashBuffer.Span.Slice(0, (int)hashStorageSize);
        res = baseStorages[1].Read(0, buffer);
        if (res.IsFailure()) return res.Miss();

        Span<byte> hash = stackalloc byte[HashSize];
        res = _hashGeneratorFactory.GenerateHash(hash, buffer);
        if (res.IsFailure()) return res.Miss();

        if (!CryptoUtil.IsSameBytes(hash, masterHash, HashSize))
            return ResultFs.HierarchicalSha256HashVerificationFailed.Log();

        return Result.Success;
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        if (destination.Length == 0)
            return Result.Success;

        if (!Alignment.IsAligned(offset, (ulong)_hashTargetBlockSize))
            return ResultFs.InvalidArgument.Log();

        if (!Alignment.IsAligned(destination.Length, (ulong)_hashTargetBlockSize))
            return ResultFs.InvalidArgument.Log();

        // The last block in a HierarchicalSha256Storage is allowed to be a partial block, but reads are required to be
        // in complete blocks. Calculate the actual amount of data we need to read from the base storage.
        long reducedSize = Math.Min(_baseStorageSize, Alignment.AlignUp(offset + destination.Length, (ulong)_hashTargetBlockSize)) - offset;

        Result res = _baseStorage.Read(offset, destination.Slice(0, (int)reducedSize));
        if (res.IsFailure()) return res.Miss();

        using var changeThreadPriority = new ScopedThreadPriorityChanger(1, ScopedThreadPriorityChanger.Mode.Relative);

        Span<byte> hash = stackalloc byte[HashSize];
        ReadOnlySpan<byte> hashBuffer = _hashBuffer.Span;

        long currentOffset = offset;
        long remainingSize = reducedSize;
        while (remainingSize > 0)
        {
            long currentSize = Math.Min(_hashTargetBlockSize, remainingSize);
            res = _hashGeneratorFactory.GenerateHash(hash, destination.Slice((int)(currentOffset - offset), (int)currentSize));
            if (res.IsFailure()) return res.Miss();

            Assert.SdkAssert((currentOffset >> _log2SizeRatio) < _hashBuffer.Length);

            using (new ScopedLock<SdkMutexType>(ref _mutex))
            {
                if (!CryptoUtil.IsSameBytes(hash, hashBuffer.Slice((int)(currentOffset >> _log2SizeRatio)), HashSize))
                {
                    destination.Clear();
                    return ResultFs.HierarchicalSha256HashVerificationFailed.Log();
                }
            }

            currentOffset += currentSize;
            remainingSize -= currentOffset;
        }

        return Result.Success;
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        // Succeed if zero-size.
        if (source.Length == 0)
            return Result.Success;

        // Validate preconditions.
        if (!Alignment.IsAligned(offset, (ulong)_hashTargetBlockSize))
            return ResultFs.InvalidArgument.Log();

        if (!Alignment.IsAligned(source.Length, (ulong)_hashTargetBlockSize))
            return ResultFs.InvalidArgument.Log();

        Span<byte> newHash = stackalloc byte[HashSize];
        Span<byte> hashBuffer = _hashBuffer.Span;

        // Setup tracking variables.
        long reducedSize = Math.Min(_baseStorageSize, Alignment.AlignUp(offset + source.Length, (ulong)_hashTargetBlockSize)) - offset;
        long currentOffset = offset;
        long remainingSize = reducedSize;

        while (remainingSize > 0)
        {
            Result res;

            // Generate the hash of the region we're validating.
            long currentSize = Math.Min(_hashTargetBlockSize, remainingSize);

            // Temporarily increase our thread priority.
            using (new ScopedThreadPriorityChanger(1, ScopedThreadPriorityChanger.Mode.Relative))
            {
                res = _hashGeneratorFactory.GenerateHash(newHash, source.Slice((int)(currentOffset - offset), (int)currentSize));
                if (res.IsFailure()) return res.Miss();
            }

            // Write the data.
            res = _baseStorage.Write(currentOffset, source.Slice((int)(currentOffset - offset), (int)currentSize));
            if (res.IsFailure()) return res.Miss();

            // Write the hash.
            using (new ScopedLock<SdkMutexType>(ref _mutex))
            {
                newHash.CopyTo(hashBuffer.Slice((int)(currentOffset >> _log2SizeRatio), HashSize));
            }

            // Advance.
            currentOffset += currentSize;
            remainingSize -= currentSize;
        }

        return Result.Success;
    }

    public override Result Flush()
    {
        return _baseStorage.Flush().Ret();
    }

    public override Result GetSize(out long size)
    {
        return _baseStorage.GetSize(out size).Ret();
    }

    public override Result SetSize(long size)
    {
        return ResultFs.UnsupportedSetSizeForHierarchicalSha256Storage.Log();
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
    {
        if (operationId == OperationId.InvalidateCache)
        {
            return _baseStorage.OperateRange(OperationId.InvalidateCache, offset, size).Ret();
        }

        if (!Alignment.IsAligned(offset, (ulong)_hashTargetBlockSize))
            return ResultFs.InvalidArgument.Log();

        if (!Alignment.IsAligned(size, (ulong)_hashTargetBlockSize))
            return ResultFs.InvalidArgument.Log();

        long reducedSize = Math.Min(_baseStorageSize, Alignment.AlignUp(offset + size, (ulong)_hashTargetBlockSize)) - offset;

        return _baseStorage.OperateRange(outBuffer, operationId, offset, reducedSize, inBuffer).Ret();
    }
}