using System;
using System.Buffers;
using LibHac.Diag;

namespace LibHac.FsSystem
{
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

        private byte[] Array { get; set; }
        private int Length { get; set; }

        public PooledBuffer(int idealSize, int requiredSize)
        {
            Array = null;
            Length = default;
            Allocate(idealSize, requiredSize);
        }

        public Span<byte> GetBuffer()
        {
            Assert.SdkRequiresNotNull(Array);
            return Array.AsSpan(0, Length);
        }

        public int GetSize()
        {
            Assert.SdkRequiresNotNull(Array);
            return Length;
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
            Assert.SdkRequiresNull(Array);

            // Check that we can allocate this size.
            Assert.SdkRequiresLessEqual(requiredSize, GetAllocatableSizeMaxCore(enableLargeCapacity));

            int targetSize = Math.Min(Math.Max(idealSize, requiredSize),
                GetAllocatableSizeMaxCore(enableLargeCapacity));

            if (targetSize >= RentThresholdBytes)
            {
                Array = ArrayPool<byte>.Shared.Rent(targetSize);
            }
            else
            {
                Array = new byte[targetSize];
            }

            Length = Array.Length;
        }

        public void Deallocate()
        {
            // Shrink the buffer to empty.
            Shrink(0);
            Assert.SdkNull(Array);
        }

        public void Shrink(int idealSize)
        {
            Assert.SdkRequiresLessEqual(idealSize, GetAllocatableSizeMaxCore(true));

            // Check if we actually need to shrink.
            if (Length > idealSize)
            {
                Assert.SdkRequiresNotNull(Array);

                // Pretend we shrank the buffer.
                Length = idealSize;

                // Shrinking to zero means that we have no buffer.
                if (Length == 0)
                {
                    // Return the array if we rented it.
                    if (Array?.Length >= RentThresholdBytes)
                    {
                        ArrayPool<byte>.Shared.Return(Array);
                    }

                    Array = null;
                }
            }
        }

        public void Dispose()
        {
            Deallocate();
        }
    }
}
