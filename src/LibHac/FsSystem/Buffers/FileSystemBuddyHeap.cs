using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Util;
using Buffer = LibHac.Fs.Buffer;

// ReSharper disable once CheckNamespace
namespace LibHac.FsSystem
{
    public unsafe class FileSystemBuddyHeap : IDisposable
    {
        private static readonly nuint BufferAlignment = (nuint)Unsafe.SizeOf<nuint>();
        private static readonly nuint BlockSizeMin = (nuint)(2 * Unsafe.SizeOf<nuint>());
        private const int OrderUpperLimit = 8 * sizeof(int) - 1;

        private nuint BlockSize { get; set; }
        private int OrderMax { get; set; }
        private UIntPtr HeapStart { get; set; }
        private nuint HeapSize { get; set; }

        private PageList* FreeLists { get; set; }
        private nuint TotalFreeSize { get; set; }
        private PageList* ExternalFreeLists { get; set; }
        private PageList[] InternalFreeLists { get; set; }

        private struct PageList
        {
            private PageEntry* FirstPageEntry { get; set; }
            private PageEntry* LastPageEntry { get; set; }
            private int EntryCount { get; set; }

            public bool IsEmpty() => EntryCount == 0;
            public int GetSize() => EntryCount;

            public PageEntry* GetFront() => FirstPageEntry;

            public PageEntry* PopFront()
            {
                Assert.True(EntryCount > 0);

                // Get the first entry.
                PageEntry* pageEntry = FirstPageEntry;

                // Advance our list.
                FirstPageEntry = pageEntry->Next;
                pageEntry->Next = null;

                // Decrement our count.
                EntryCount--;
                Assert.True(EntryCount >= 0);

                // If this was our last page, clear our last entry.
                if (EntryCount == 0)
                {
                    LastPageEntry = null;
                }

                return pageEntry;
            }

            public void PushBack(PageEntry* pageEntry)
            {
                Assert.True(pageEntry != null);

                // If we're empty, we want to set the first page entry.
                if (IsEmpty())
                {
                    FirstPageEntry = pageEntry;
                }
                else
                {
                    // We're not empty, so push the page to the back.
                    Assert.True(LastPageEntry != pageEntry);
                    LastPageEntry->Next = pageEntry;
                }

                // Set our last page entry to be this one, and link it to the list.
                LastPageEntry = pageEntry;
                LastPageEntry->Next = null;

                // Increment our entry count.
                EntryCount++;
                Assert.True(EntryCount > 0);
            }

            public bool Remove(PageEntry* pageEntry)
            {
                Assert.True(pageEntry != null);

                // If we're empty, we can't remove the page list.
                if (IsEmpty())
                {
                    return false;
                }

                // We're going to loop over all pages to find this one, then unlink it.
                PageEntry* prevEntry = null;
                PageEntry* curEntry = FirstPageEntry;

                while (true)
                {
                    // Check if we found the page.
                    if (curEntry == pageEntry)
                    {
                        if (curEntry == FirstPageEntry)
                        {
                            // If it's the first page, we just set our first.
                            FirstPageEntry = curEntry->Next;
                        }
                        else if (curEntry == LastPageEntry)
                        {
                            // If it's the last page, we set our last.
                            LastPageEntry = prevEntry;
                            LastPageEntry->Next = null;
                        }
                        else
                        {
                            // If it's in the middle, we just unlink.
                            prevEntry->Next = curEntry->Next;
                        }

                        // Unlink this entry's next.
                        curEntry->Next = null;

                        // Update our entry count.
                        EntryCount--;
                        Assert.True(EntryCount >= 0);

                        return true;
                    }

                    // If we have no next page, we can't remove.
                    if (curEntry->Next == null)
                    {
                        return false;
                    }

                    // Advance to the next item in the list.
                    prevEntry = curEntry;
                    curEntry = curEntry->Next;
                }
            }
        }

        private struct PageEntry
        {
            public PageEntry* Next;
        }

        public void Dispose()
        {
            FreeLists = null;
            ExternalFreeLists = null;
            InternalFreeLists = null;
            PinnedHeapMemoryHandle.Dispose();
            PinnedWorkMemoryHandle.Dispose();
        }

        public static int GetBlockCountFromOrder(int order)
        {
            Assert.True(0 <= order);
            Assert.True(order < OrderUpperLimit);
            return 1 << order;
        }

        public static nuint QueryWorkBufferSize(int orderMax)
        {
            Assert.InRange(orderMax, 1, OrderUpperLimit);

            var pageListSize = (nint)Unsafe.SizeOf<PageList>();
            uint pageListAlignment = (uint)Unsafe.SizeOf<nint>();
            const uint ulongAlignment = 8;

            return (nuint)Alignment.AlignUpPow2(pageListSize * (orderMax + 1) + pageListAlignment, ulongAlignment);
        }

        public static int QueryOrderMax(nuint size, nuint blockSize)
        {
            Assert.True(size >= blockSize);
            Assert.True(blockSize >= BlockSizeMin);
            Assert.True(BitUtil.IsPowerOfTwo(blockSize));

            int blockCount = (int)(Alignment.AlignUpPow2(size, (uint)blockSize) / blockSize);
            for (int order = 1; ; order++)
            {
                if (blockCount <= GetBlockCountFromOrder(order))
                    return order;
            }
        }

        public Result Initialize(UIntPtr address, nuint size, nuint blockSize, void* workBuffer, nuint workBufferSize)
        {
            return Initialize(address, size, blockSize, QueryOrderMax(size, blockSize), workBuffer, workBufferSize);
        }

        public Result Initialize(UIntPtr address, nuint size, nuint blockSize, int orderMax, void* workBuffer,
            nuint workBufferSize)
        {
            Assert.True(workBufferSize >= QueryWorkBufferSize(orderMax));

            uint pageListAlignment = (uint)Unsafe.SizeOf<nint>();
            var alignedWork = (void*)Alignment.AlignUpPow2((ulong)workBuffer, pageListAlignment);
            ExternalFreeLists = (PageList*)alignedWork;

            // Note: The original code does not have a buffer size assert after adjusting for alignment.
            Assert.True(workBufferSize - ((nuint)alignedWork - (nuint)workBuffer) >= QueryWorkBufferSize(orderMax));

            return Initialize(address, size, blockSize, orderMax);
        }

        public Result Initialize(UIntPtr address, nuint size, nuint blockSize)
        {
            return Initialize(address, size, blockSize, QueryOrderMax(size, blockSize));
        }

        public Result Initialize(UIntPtr address, nuint size, nuint blockSize, int orderMax)
        {
            Assert.True(FreeLists == null);
            Assert.True(address != UIntPtr.Zero);
            Assert.True(Alignment.IsAlignedPow2(address.ToUInt64(), (uint)BufferAlignment));
            Assert.True(blockSize >= BlockSizeMin);
            Assert.True(BitUtil.IsPowerOfTwo(blockSize));
            Assert.True(size >= blockSize);
            Assert.True(orderMax > 0);
            Assert.True(orderMax < OrderUpperLimit);

            // Set up our basic member variables
            BlockSize = blockSize;
            OrderMax = orderMax;
            HeapStart = address;
            HeapSize = size;

            TotalFreeSize = 0;

            // Determine page sizes
            nuint maxPageSize = BlockSize << OrderMax;
            nuint maxPageCount = (nuint)Alignment.AlignUp(HeapSize, (uint)maxPageSize) / maxPageSize;
            Assert.True(maxPageCount > 0);

            // Setup the free lists
            if (ExternalFreeLists != null)
            {
                Assert.Null(InternalFreeLists);
                FreeLists = ExternalFreeLists;
            }
            else
            {
                InternalFreeLists = GC.AllocateArray<PageList>(OrderMax + 1, true);
                FreeLists = (PageList*)Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(InternalFreeLists));
                if (InternalFreeLists == null)
                    return ResultFs.AllocationFailureInFileSystemBuddyHeapA.Log();
            }

            // All but the last page region should go to the max order.
            for (nuint i = 0; i < maxPageCount - 1; i++)
            {
                PageEntry* pageEntry = GetPageEntryFromAddress(HeapStart + i * maxPageSize);
                FreeLists[orderMax].PushBack(pageEntry);
            }

            TotalFreeSize += (nuint)FreeLists[orderMax].GetSize() * GetBytesFromOrder(orderMax);

            // Allocate remaining space to smaller orders as possible.
            {
                nuint remaining = HeapSize - (maxPageCount - 1) * maxPageSize;
                nuint curAddress = HeapStart - (maxPageCount - 1) * maxPageSize;
                Assert.True(Alignment.IsAlignedPow2(remaining, (uint)BlockSize));

                do
                {
                    // Determine what order we can use.
                    int order = GetOrderFromBytes(remaining + 1);
                    if (order < 0)
                    {
                        Assert.True(GetOrderFromBytes(remaining) == orderMax);
                        order = OrderMax + 1;
                    }

                    Assert.True(0 < order);
                    Assert.True(order <= OrderMax + 1);

                    // Add to the correct free list.
                    FreeLists[order - 1].PushBack(GetPageEntryFromAddress(curAddress));
                    TotalFreeSize += GetBytesFromOrder(order - 1);

                    // Move on to the next order.
                    nuint pageSize = GetBytesFromOrder(order - 1);
                    curAddress += pageSize;
                    remaining -= pageSize;
                } while (BlockSize <= remaining);
            }

            return Result.Success;
        }

        public void* AllocateByOrder(int order)
        {
            Assert.True(FreeLists != null);
            Assert.True(order >= 0);
            Assert.True(order <= GetOrderMax());

            // Get the page entry.
            PageEntry* pageEntry = GetFreePageEntry(order);

            if (pageEntry != null)
            {
                // Ensure we're allocating an unlinked page.
                Assert.True(pageEntry->Next == null);

                // Return the address for this entry.
                return (void*)GetAddressFromPageEntry(pageEntry);
            }
            else
            {
                return null;
            }
        }

        public void Free(void* pointer, int order)
        {
            Assert.True(FreeLists != null);
            Assert.True(order >= 0);
            Assert.True(order <= GetOrderMax());

            // Allow Free(null)
            if (pointer == null)
                return;

            // Ensure the pointer is block aligned.
            Assert.True(Alignment.IsAlignedPow2((nuint)pointer - HeapStart, (uint)GetBlockSize()));

            // Get the page entry.
            PageEntry* pageEntry = GetPageEntryFromAddress((UIntPtr)pointer);
            Assert.True(IsAlignedToOrder(pageEntry, order));

            /* Reinsert into the free lists. */
            JoinBuddies(pageEntry, order);
        }

        public nuint GetTotalFreeSize()
        {
            Assert.True(FreeLists != null);
            return TotalFreeSize;
        }

        public nuint GetAllocatableSizeMax()
        {
            Assert.True(FreeLists != null);

            // The maximum allocatable size is a chunk from the biggest non-empty order.
            for (int order = GetOrderMax(); order >= 0; order--)
            {
                if (FreeLists[order].IsEmpty())
                {
                    return GetBytesFromOrder(order);
                }
            }

            // If all orders are empty, then we can't allocate anything.
            return 0;
        }

        public void Dump()
        {
            Assert.True(FreeLists != null);
            throw new NotImplementedException();
        }

        public int GetOrderFromBytes(nuint size)
        {
            Assert.True(FreeLists != null);
            return GetOrderFromBlockCount(GetBlockCountFromSize(size));
        }

        public nuint GetBytesFromOrder(int order)
        {
            Assert.True(FreeLists != null);
            Assert.True(order >= 0);
            Assert.True(order <= GetOrderMax());

            return GetBlockSize() << order;
        }

        public int GetOrderMax()
        {
            Assert.True(FreeLists != null);
            return OrderMax;
        }

        public nuint GetBlockSize()
        {
            Assert.True(FreeLists != null);
            return BlockSize;
        }

        private void DivideBuddies(PageEntry* pageEntry, int requiredOrder, int chosenOrder)
        {
            Assert.True(FreeLists != null);
            Assert.True(requiredOrder >= 0);
            Assert.True(chosenOrder >= requiredOrder);
            Assert.True(chosenOrder <= GetOrderMax());

            // Start at the end of the entry.
            nuint address = GetAddressFromPageEntry(pageEntry) + GetBytesFromOrder(chosenOrder);

            for (int order = chosenOrder; order > requiredOrder; order--)
            {
                // For each order, subtract that order's size from the address to get the start of a new block.
                address -= GetBytesFromOrder(order - 1);
                PageEntry* dividedEntry = GetPageEntryFromAddress(address);

                // Push back to the list.
                FreeLists[order - 1].PushBack(dividedEntry);
                TotalFreeSize += GetBytesFromOrder(order - 1);
            }
        }

        private void JoinBuddies(PageEntry* pageEntry, int order)
        {
            Assert.True(pageEntry != null);
            Assert.True(order >= 0);
            Assert.True(order <= GetOrderMax());

            PageEntry* curEntry = pageEntry;
            int curOrder = order;

            while (curOrder < GetOrderMax())
            {
                // Get the buddy page.
                PageEntry* buddyEntry = GetBuddy(curEntry, curOrder);

                // Check whether the buddy is in the relevant free list.
                if (buddyEntry != null && FreeLists[curOrder].Remove(buddyEntry))
                {
                    TotalFreeSize -= GetBytesFromOrder(curOrder);

                    // Ensure we coalesce with the correct buddy when page is aligned
                    if (!IsAlignedToOrder(curEntry, curOrder + 1))
                    {
                        curEntry = buddyEntry;
                    }

                    curOrder++;
                }
                else
                {
                    // Buddy isn't in the free list, so we can't coalesce.
                    break;
                }
            }

            // Insert the coalesced entry into the free list.
            FreeLists[curOrder].PushBack(curEntry);
            TotalFreeSize += GetBytesFromOrder(curOrder);
        }

        private PageEntry* GetBuddy(PageEntry* pageEntry, int order)
        {
            Assert.True(pageEntry != null);
            Assert.True(order >= 0);
            Assert.True(order <= GetOrderMax());

            nuint address = GetAddressFromPageEntry(pageEntry);
            nuint offset = (nuint)GetBlockCountFromOrder(order) * GetBlockSize();

            if (IsAlignedToOrder(pageEntry, order + 1))
            {
                // If the page entry is aligned to the next order,
                // return the buddy block to the right of the current entry.
                return address + offset < HeapStart + HeapSize ? GetPageEntryFromAddress(address + offset) : null;
            }
            else
            {
                // If the page entry isn't aligned, return the buddy block to the left of the current entry.
                return HeapStart <= address - offset ? GetPageEntryFromAddress(address - offset) : null;
            }
        }

        private PageEntry* GetFreePageEntry(int order)
        {
            Assert.True(order >= 0);
            Assert.True(order <= GetOrderMax());

            // Try orders from low to high until we find a free page entry.
            for (int curOrder = order; curOrder <= GetOrderMax(); curOrder++)
            {
                ref PageList freeList = ref FreeLists[curOrder];
                if (!freeList.IsEmpty())
                {
                    // The current list isn't empty, so grab an entry from it.
                    PageEntry* pageEntry = freeList.PopFront();
                    Assert.True(pageEntry != null);

                    // Update size bookkeeping.
                    TotalFreeSize -= GetBytesFromOrder(curOrder);

                    // If we allocated more memory than needed, free the unneeded portion.
                    DivideBuddies(pageEntry, order, curOrder);
                    Assert.True(pageEntry->Next == null);

                    // Return the newly-divided entry.
                    return pageEntry;
                }
            }

            // We failed to find a free page.
            return null;
        }

        private int GetOrderFromBlockCount(int blockCount)
        {
            Assert.True(blockCount >= 0);

            // Return the first order with a big enough block count.
            for (int order = 0; order <= GetOrderMax(); ++order)
            {
                if (blockCount <= GetBlockCountFromOrder(order))
                {
                    return order;
                }
            }

            return -1;
        }

        private int GetBlockCountFromSize(nuint size)
        {
            nuint blockSize = GetBlockSize();
            return (int)(Alignment.AlignUpPow2(size, (uint)blockSize) / blockSize);
        }

        private UIntPtr GetAddressFromPageEntry(PageEntry* pageEntry)
        {
            var address = new UIntPtr(pageEntry);

            Assert.True((nuint)HeapStart <= address);
            Assert.True(address < HeapStart + HeapSize);
            Assert.True(Alignment.IsAlignedPow2((nuint)address - HeapStart, (uint)GetBlockSize()));

            return address;
        }

        private PageEntry* GetPageEntryFromAddress(UIntPtr address)
        {
            Assert.True((nuint)HeapStart <= address);
            Assert.True(address < HeapStart + HeapSize);

            ulong blockStart = (ulong)HeapStart +
                               Alignment.AlignDownPow2((nuint)address - HeapStart, (uint)GetBlockSize());
            return (PageEntry*)blockStart;
        }

        private int GetIndexFromPageEntry(PageEntry* pageEntry)
        {
            var address = (nuint)pageEntry;

            Assert.True(HeapStart <= address);
            Assert.True(address < HeapStart + HeapSize);
            Assert.True(Alignment.IsAlignedPow2(address - HeapStart, (uint)GetBlockSize()));

            return (int)((address - HeapStart) / GetBlockSize());
        }

        private bool IsAlignedToOrder(PageEntry* pageEntry, int order)
        {
            return Alignment.IsAlignedPow2(GetIndexFromPageEntry(pageEntry), (uint)GetBlockCountFromOrder(order));
        }

        // Addition: The below fields and methods allow using Memory<byte> with the class instead
        // of raw pointers.
        private MemoryHandle PinnedHeapMemoryHandle { get; set; }
        private Memory<byte> HeapBuffer { get; set; }
        private MemoryHandle PinnedWorkMemoryHandle { get; set; }

        public Result Initialize(Memory<byte> heapBuffer, int blockSize, Memory<byte> workBuffer)
        {
            return Initialize(heapBuffer, blockSize, QueryOrderMax((nuint)heapBuffer.Length, (nuint)blockSize),
                workBuffer);
        }

        public Result Initialize(Memory<byte> heapBuffer, int blockSize, int orderMax, Memory<byte> workBuffer)
        {
            PinnedWorkMemoryHandle = workBuffer.Pin();

            PinnedHeapMemoryHandle = heapBuffer.Pin();
            HeapBuffer = heapBuffer;

            var heapAddress = (UIntPtr)PinnedHeapMemoryHandle.Pointer;
            var heapSize = (nuint)heapBuffer.Length;

            void* workAddress = PinnedWorkMemoryHandle.Pointer;
            var workSize = (nuint)workBuffer.Length;

            return Initialize(heapAddress, heapSize, (nuint)blockSize, orderMax, workAddress, workSize);
        }

        public Result Initialize(Memory<byte> heapBuffer, int blockSize)
        {
            return Initialize(heapBuffer, blockSize, QueryOrderMax((nuint)heapBuffer.Length, (nuint)blockSize));
        }

        public Result Initialize(Memory<byte> heapBuffer, int blockSize, int orderMax)
        {
            PinnedHeapMemoryHandle = heapBuffer.Pin();
            HeapBuffer = heapBuffer;

            var address = (UIntPtr)PinnedHeapMemoryHandle.Pointer;
            var size = (nuint)heapBuffer.Length;

            return Initialize(address, size, (nuint)blockSize, orderMax);
        }

        public Buffer AllocateBufferByOrder(int order)
        {
            Assert.True(!HeapBuffer.IsEmpty);

            void* address = AllocateByOrder(order);

            if (address == null)
                return Buffer.Empty;

            nuint size = GetBytesFromOrder(order);
            Assert.True(size <= int.MaxValue);

            // Get the offset relative to the heap start
            nuint offset = (nuint)address - (nuint)PinnedHeapMemoryHandle.Pointer;
            Assert.True(offset <= (nuint)HeapBuffer.Length);

            // Get a slice of the Memory<byte> containing the entire heap
            return new Buffer(HeapBuffer.Slice((int)offset, (int)size));
        }

        public void Free(Buffer buffer)
        {
            Assert.True(!HeapBuffer.IsEmpty);
            Assert.True(!buffer.IsNull);

            int order = GetOrderFromBytes((nuint)buffer.Length);
            void* pointer = Unsafe.AsPointer(ref MemoryMarshal.GetReference(buffer.Span));
            Free(pointer, order);
        }
    }
}
