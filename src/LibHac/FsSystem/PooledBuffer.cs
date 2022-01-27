using System;
using System.Buffers;
using LibHac.Diag;
using LibHac.FsSrv;
using LibHac.Os;

namespace LibHac.FsSystem;

public static class PooledBufferGlobalMethods
{
    public static long GetPooledBufferRetriedCount(this FileSystemServer fsSrv)
    {
        return fsSrv.Globals.PooledBuffer.CountRetried;
    }

    public static long GetPooledBufferReduceAllocationCount(this FileSystemServer fsSrv)
    {
        return fsSrv.Globals.PooledBuffer.CountReduceAllocation;
    }

    public static long GetPooledBufferFailedIdealAllocationCountOnAsyncAccess(this FileSystemServer fsSrv)
    {
        return fsSrv.Globals.PooledBuffer.CountFailedIdealAllocationOnAsyncAccess;
    }

    public static long GetPooledBufferFreeSizePeak(this FileSystemServer fsSrv)
    {
        ref PooledBufferGlobals g = ref fsSrv.Globals.PooledBuffer;

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref g.HeapMutex);
        return g.SizeHeapFreePeak;
    }

    public static void ClearPooledBufferPeak(this FileSystemServer fsSrv)
    {
        ref PooledBufferGlobals g = ref fsSrv.Globals.PooledBuffer;

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref g.HeapMutex);

        // Missing: Get SizeHeapFreePeak from the heap object 
        g.CountRetried = 0;
        g.CountReduceAllocation = 0;
        g.CountFailedIdealAllocationOnAsyncAccess = 0;
    }
}

internal struct PooledBufferGlobals
{
    public SdkMutexType HeapMutex;
    public long SizeHeapFreePeak;
    public Memory<byte> HeapBuffer;
    public long CountRetried;
    public long CountReduceAllocation;
    public long CountFailedIdealAllocationOnAsyncAccess;

    public void Initialize()
    {
        HeapMutex = new SdkMutexType();
    }
}

// Implement the PooledBuffer interface using .NET ArrayPools
public struct PooledBuffer : IDisposable
{
    // It's faster to create new smaller arrays than rent them
    private const int RentThresholdBytes = 512;

    private const int HeapBlockSize = 1024 * 4;

    // Keep the max sizes that FS uses.
    // A heap block is 4KB.An order is a power of two. 
    // This gives blocks of the order 512KB, 4MB.
    private const int HeapOrderMax = 7;
    private const int HeapOrderMaxForLarge = HeapOrderMax + 3;

    private const int HeapAllocatableSizeMax = HeapBlockSize * (1 << HeapOrderMax);
    private const int HeapAllocatableSizeMaxForLarge = HeapBlockSize * (1 << HeapOrderMaxForLarge);

    private byte[] _array;
    private int _length;

    public PooledBuffer(int idealSize, int requiredSize)
    {
        _array = null;
        _length = default;
        Allocate(idealSize, requiredSize);
    }

    public Span<byte> GetBuffer()
    {
        Assert.SdkRequiresNotNull(_array);
        return _array.AsSpan(0, _length);
    }

    public int GetSize()
    {
        Assert.SdkRequiresNotNull(_array);
        return _length;
    }

    public static int GetAllocatableSizeMax() => GetAllocatableSizeMaxCore(false);
    public static int GetAllocatableParticularlyLargeSizeMax => GetAllocatableSizeMaxCore(true);

    private static int GetAllocatableSizeMaxCore(bool enableLargeCapacity)
    {
        return enableLargeCapacity ? HeapAllocatableSizeMaxForLarge : HeapAllocatableSizeMax;
    }

    public void Allocate(int idealSize, int requiredSize) => AllocateCore(idealSize, requiredSize, false);
    public void AllocateParticularlyLarge(int idealSize, int requiredSize) => AllocateCore(idealSize, requiredSize, true);

    private void AllocateCore(int idealSize, int requiredSize, bool enableLargeCapacity)
    {
        Assert.SdkRequiresNull(_array);

        // Check that we can allocate this size.
        Assert.SdkRequiresLessEqual(requiredSize, GetAllocatableSizeMaxCore(enableLargeCapacity));

        int targetSize = Math.Min(Math.Max(idealSize, requiredSize),
            GetAllocatableSizeMaxCore(enableLargeCapacity));

        if (targetSize >= RentThresholdBytes)
        {
            _array = ArrayPool<byte>.Shared.Rent(targetSize);
        }
        else
        {
            _array = new byte[targetSize];
        }

        _length = _array.Length;
    }

    public void Deallocate()
    {
        // Shrink the buffer to empty.
        Shrink(0);
        Assert.SdkNull(_array);
    }

    public void Shrink(int idealSize)
    {
        Assert.SdkRequiresLessEqual(idealSize, GetAllocatableSizeMaxCore(true));

        // Check if we actually need to shrink.
        if (_length > idealSize)
        {
            Assert.SdkRequiresNotNull(_array);

            // Pretend we shrank the buffer.
            _length = idealSize;

            // Shrinking to zero means that we have no buffer.
            if (_length == 0)
            {
                // Return the array if we rented it.
                if (_array?.Length >= RentThresholdBytes)
                {
                    ArrayPool<byte>.Shared.Return(_array);
                }

                _array = null;
            }
        }
    }

    public void Dispose()
    {
        Deallocate();
    }
}
