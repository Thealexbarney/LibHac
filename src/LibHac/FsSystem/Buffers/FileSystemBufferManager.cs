using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Util;
using Buffer = LibHac.Fs.Buffer;
using CacheHandle = System.Int64;

// ReSharper disable once CheckNamespace
namespace LibHac.FsSystem
{
    public class FileSystemBufferManager : IBufferManager
    {
        private class CacheHandleTable
        {
            private struct Entry
            {
                private CacheHandle _handle;
                private Buffer _buffer;
                private BufferAttribute _attribute;

                public void Initialize(CacheHandle handle, Buffer buffer, BufferAttribute attribute)
                {
                    _handle = handle;
                    _buffer = buffer;
                    _attribute = attribute;
                }

                public readonly CacheHandle GetHandle() => _handle;
                public readonly Buffer GetBuffer() => _buffer;
                public readonly int GetSize() => _buffer.Length;
                public readonly BufferAttribute GetBufferAttribute() => _attribute;
            }

            private struct AttrInfo
            {
                private int _level;
                private int _cacheCount;
                private int _cacheSize;

                public AttrInfo(int level, int cacheCount, int cacheSize)
                {
                    _level = level;
                    _cacheCount = cacheCount;
                    _cacheSize = cacheSize;
                }

                public int GetLevel() => _level;
                public int GetCacheCount() => _cacheCount;
                public void IncrementCacheCount() => _cacheCount++;
                public void DecrementCacheCount() => _cacheCount--;
                public int GetCacheSize() => _cacheSize;
                public void AddCacheSize(int diff) => _cacheSize += diff;

                public void SubtractCacheSize(int diff)
                {
                    Assert.SdkRequiresGreaterEqual(_cacheSize, diff);
                    _cacheSize -= diff;
                }
            }

            private Entry[] Entries { get; set; }
            private int EntryCount { get; set; }
            private int EntryCountMax { get; set; }
            private LinkedList<AttrInfo> AttrList { get; set; } = new();
            private int CacheCountMin { get; set; }
            private int CacheSizeMin { get; set; }
            private int TotalCacheSize { get; set; }
            private CacheHandle CurrentHandle { get; set; }

            // ReSharper disable once UnusedMember.Local
            // We can't use an external buffer in C# without ensuring all allocated buffers are pinned.
            // This function is left here anyway for completion's sake.
            public static int QueryWorkBufferSize(int maxCacheCount)
            {
                Assert.SdkRequiresGreater(maxCacheCount, 0);

                int entryAlignment = sizeof(CacheHandle);
                int attrInfoAlignment = Unsafe.SizeOf<nuint>();

                int entrySize = Unsafe.SizeOf<Entry>() * maxCacheCount;
                int attrListSize = Unsafe.SizeOf<AttrInfo>() * 0x100;
                return (int)Alignment.AlignUpPow2(
                    (ulong)(entrySize + attrListSize + entryAlignment + attrInfoAlignment), 8);
            }

            public Result Initialize(int maxCacheCount)
            {
                // Validate pre-conditions.
                Assert.SdkRequiresNull(Entries);

                // Note: We don't have the option of using an external Entry buffer like the original C++ code
                // because Entry includes managed references so we can't cast a byte* to Entry* without pinning.
                // If we don't have an external buffer, try to allocate an internal one.

                Entries = new Entry[maxCacheCount];

                if (Entries == null)
                {
                    return ResultFs.AllocationMemoryFailedInFileSystemBufferManagerA.Log();
                }

                // Set entries.
                EntryCount = 0;
                EntryCountMax = maxCacheCount;

                Assert.SdkNotNull(Entries);

                CacheCountMin = maxCacheCount / 16;
                CacheSizeMin = CacheCountMin * 0x100;

                return Result.Success;
            }

            // ReSharper disable once UnusedParameter.Local
            private int GetCacheCountMin(BufferAttribute attr)
            {
                return CacheCountMin;
            }

            // ReSharper disable once UnusedParameter.Local
            private int GetCacheSizeMin(BufferAttribute attr)
            {
                return CacheSizeMin;
            }

            public bool Register(out CacheHandle handle, Buffer buffer, BufferAttribute attr)
            {
                UnsafeHelpers.SkipParamInit(out handle);

                // Validate pre-conditions.
                Assert.SdkRequiresNotNull(Entries);
                Assert.SdkRequiresNotNull(ref handle);

                // Get the entry.
                ref Entry entry = ref AcquireEntry(buffer, attr);

                // If we don't have an entry, we can't register.
                if (Unsafe.IsNullRef(ref entry))
                    return false;

                // Get the attr info. If we have one, increment.
                ref AttrInfo attrInfo = ref FindAttrInfo(attr);
                if (!Unsafe.IsNullRef(ref attrInfo))
                {
                    attrInfo.IncrementCacheCount();
                    attrInfo.AddCacheSize(buffer.Length);
                }
                else
                {
                    // Make a new attr info and add it to the list.
                    // Note: Not using attr info buffer
                    var newInfo = new AttrInfo(attr.Level, 1, buffer.Length);
                    AttrList.AddLast(newInfo);
                }

                TotalCacheSize += buffer.Length;
                handle = entry.GetHandle();
                return true;
            }

            public bool Unregister(out Buffer buffer, CacheHandle handle)
            {
                // Validate pre-conditions.
                Unsafe.SkipInit(out buffer);
                Assert.SdkRequiresNotNull(Entries);
                Assert.SdkRequiresNotNull(ref buffer);

                UnsafeHelpers.SkipParamInit(out buffer);

                // Find the lower bound for the entry.
                for (int i = 0; i < EntryCount; i++)
                {
                    if (Entries[i].GetHandle() == handle)
                    {
                        UnregisterCore(out buffer, ref Entries[i]);
                        return true;
                    }
                }

                return false;
            }

            // ReSharper disable UnusedParameter.Local
            public bool UnregisterOldest(out Buffer buffer, BufferAttribute attr, int requiredSize = 0)
            // ReSharper restore UnusedParameter.Local
            {
                // Validate pre-conditions.
                Unsafe.SkipInit(out buffer);
                Assert.SdkRequiresNotNull(Entries);
                Assert.SdkRequiresNotNull(ref buffer);

                UnsafeHelpers.SkipParamInit(out buffer);

                // If we have no entries, we can't unregister any.
                if (EntryCount == 0)
                {
                    return false;
                }

                static bool CanUnregister(CacheHandleTable table, ref Entry entry)
                {
                    ref AttrInfo attrInfo = ref table.FindAttrInfo(entry.GetBufferAttribute());
                    Assert.SdkNotNull(ref attrInfo);

                    int ccm = table.GetCacheCountMin(entry.GetBufferAttribute());
                    int csm = table.GetCacheSizeMin(entry.GetBufferAttribute());

                    return ccm < attrInfo.GetCacheCount() && csm + entry.GetSize() <= attrInfo.GetCacheSize();
                }

                // Find an entry, falling back to the first entry.
                ref Entry entry = ref Unsafe.NullRef<Entry>();
                for (int i = 0; i < EntryCount; i++)
                {
                    if (CanUnregister(this, ref Entries[i]))
                    {
                        entry = ref Entries[i];
                        break;
                    }
                }

                if (Unsafe.IsNullRef(ref entry))
                {
                    entry = ref Entries[0];
                }

                Assert.SdkNotNull(ref entry);
                UnregisterCore(out buffer, ref entry);
                return true;
            }

            private void UnregisterCore(out Buffer buffer, ref Entry entry)
            {
                // Validate pre-conditions.
                Unsafe.SkipInit(out buffer);
                Assert.SdkRequiresNotNull(Entries);
                Assert.SdkRequiresNotNull(ref buffer);
                Assert.SdkRequiresNotNull(ref entry);

                UnsafeHelpers.SkipParamInit(out buffer);

                // Get the attribute info.
                ref AttrInfo attrInfo = ref FindAttrInfo(entry.GetBufferAttribute());
                Assert.SdkNotNull(ref attrInfo);
                Assert.SdkGreater(attrInfo.GetCacheCount(), 0);
                Assert.SdkGreaterEqual(attrInfo.GetCacheSize(), entry.GetSize());

                // Release from the attr info.
                attrInfo.DecrementCacheCount();
                attrInfo.SubtractCacheSize(entry.GetSize());

                // Release from cached size.
                Assert.SdkGreaterEqual(TotalCacheSize, entry.GetSize());
                TotalCacheSize -= entry.GetSize();

                // Release the entry.
                buffer = entry.GetBuffer();
                ReleaseEntry(ref entry);
            }

            public CacheHandle PublishCacheHandle()
            {
                Assert.SdkRequires(Entries != null);
                return ++CurrentHandle;
            }

            public int GetTotalCacheSize()
            {
                return TotalCacheSize;
            }

            private ref Entry AcquireEntry(Buffer buffer, BufferAttribute attr)
            {
                // Validate pre-conditions.
                Assert.SdkRequiresNotNull(Entries);

                ref Entry entry = ref Unsafe.NullRef<Entry>();
                if (EntryCount < EntryCountMax)
                {
                    entry = ref Entries[EntryCount];
                    entry.Initialize(PublishCacheHandle(), buffer, attr);
                    EntryCount++;
                    Assert.SdkAssert(EntryCount == 1 || Entries[EntryCount - 2].GetHandle() < entry.GetHandle());
                }

                return ref entry;
            }

            private void ReleaseEntry(ref Entry entry)
            {
                // Validate pre-conditions.
                Assert.SdkRequiresNotNull(Entries);
                Assert.SdkRequiresNotNull(ref entry);

                // Ensure the entry is valid.
                Span<Entry> entryBuffer = Entries;
                Assert.SdkAssert(!Unsafe.IsAddressLessThan(ref entry, ref MemoryMarshal.GetReference(entryBuffer)));
                Assert.SdkAssert(Unsafe.IsAddressLessThan(ref entry,
                    ref Unsafe.Add(ref MemoryMarshal.GetReference(entryBuffer), entryBuffer.Length)));

                // Get the index of the entry.
                int index = Unsafe.ByteOffset(ref MemoryMarshal.GetReference(entryBuffer), ref entry).ToInt32() /
                            Unsafe.SizeOf<Entry>();

                // Copy the entries back by one.
                Span<Entry> source = entryBuffer.Slice(index + 1, EntryCount - (index + 1));
                Span<Entry> dest = entryBuffer.Slice(index);
                source.CopyTo(dest);

                // Decrement our entry count.
                EntryCount--;
            }

            private ref AttrInfo FindAttrInfo(BufferAttribute attr)
            {
                LinkedListNode<AttrInfo> curNode = AttrList.First;

                while (curNode != null)
                {
                    if (curNode.ValueRef.GetLevel() == attr.Level)
                    {
                        return ref curNode.ValueRef;
                    }

                    curNode = curNode.Next;
                }

                return ref Unsafe.NullRef<AttrInfo>();
            }
        }

        private FileSystemBuddyHeap BuddyHeap { get; } = new();
        private CacheHandleTable CacheTable { get; } = new();
        private int TotalSize { get; set; }
        private int PeakFreeSize { get; set; }
        private int PeakTotalAllocatableSize { get; set; }
        private int RetriedCount { get; set; }
        private object Locker { get; } = new();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                BuddyHeap.Dispose();
            }

            base.Dispose(disposing);
        }

        public Result Initialize(int maxCacheCount, Memory<byte> heapBuffer, int blockSize)
        {
            Result rc = CacheTable.Initialize(maxCacheCount);
            if (rc.IsFailure()) return rc;

            rc = BuddyHeap.Initialize(heapBuffer, blockSize);
            if (rc.IsFailure()) return rc;

            TotalSize = (int)BuddyHeap.GetTotalFreeSize();
            PeakFreeSize = TotalSize;
            PeakTotalAllocatableSize = TotalSize;

            return Result.Success;
        }

        public Result Initialize(int maxCacheCount, Memory<byte> heapBuffer, int blockSize, int maxOrder)
        {
            Result rc = CacheTable.Initialize(maxCacheCount);
            if (rc.IsFailure()) return rc;

            rc = BuddyHeap.Initialize(heapBuffer, blockSize, maxOrder);
            if (rc.IsFailure()) return rc;

            TotalSize = (int)BuddyHeap.GetTotalFreeSize();
            PeakFreeSize = TotalSize;
            PeakTotalAllocatableSize = TotalSize;

            return Result.Success;
        }

        public Result Initialize(int maxCacheCount, Memory<byte> heapBuffer, int blockSize, Memory<byte> workBuffer)
        {
            // Note: We can't use an external buffer for the cache handle table since it contains managed pointers,
            // so pass the work buffer directly to the buddy heap.

            Result rc = CacheTable.Initialize(maxCacheCount);
            if (rc.IsFailure()) return rc;

            rc = BuddyHeap.Initialize(heapBuffer, blockSize, workBuffer);
            if (rc.IsFailure()) return rc;

            TotalSize = (int)BuddyHeap.GetTotalFreeSize();
            PeakFreeSize = TotalSize;
            PeakTotalAllocatableSize = TotalSize;

            return Result.Success;
        }

        public Result Initialize(int maxCacheCount, Memory<byte> heapBuffer, int blockSize, int maxOrder,
            Memory<byte> workBuffer)
        {
            // Note: We can't use an external buffer for the cache handle table since it contains managed pointers,
            // so pass the work buffer directly to the buddy heap.

            Result rc = CacheTable.Initialize(maxCacheCount);
            if (rc.IsFailure()) return rc;

            rc = BuddyHeap.Initialize(heapBuffer, blockSize, maxOrder, workBuffer);
            if (rc.IsFailure()) return rc;

            TotalSize = (int)BuddyHeap.GetTotalFreeSize();
            PeakFreeSize = TotalSize;
            PeakTotalAllocatableSize = TotalSize;

            return Result.Success;
        }

        protected override Buffer DoAllocateBuffer(int size, BufferAttribute attribute)
        {
            lock (Locker)
            {
                return AllocateBufferImpl(size, attribute);
            }
        }

        private Buffer AllocateBufferImpl(int size, BufferAttribute attribute)
        {
            int order = BuddyHeap.GetOrderFromBytes((nuint)size);
            Assert.SdkAssert(order >= 0);

            // Allocate space on the heap
            Buffer buffer;
            while ((buffer = BuddyHeap.AllocateBufferByOrder(order)).IsNull)
            {
                // Not enough space in heap. Deallocate cached buffer and try again.
                RetriedCount++;

                if (!CacheTable.UnregisterOldest(out Buffer deallocateBuffer, attribute, size))
                {
                    // No cached buffers left to deallocate.
                    return Buffer.Empty;
                }

                DeallocateBufferImpl(deallocateBuffer);
            }

            // Successfully allocated a buffer.
            int allocatedSize = (int)BuddyHeap.GetBytesFromOrder(order);
            Assert.SdkAssert(size <= allocatedSize);

            // Update heap stats
            int freeSize = (int)BuddyHeap.GetTotalFreeSize();
            PeakFreeSize = Math.Min(PeakFreeSize, freeSize);

            int totalAllocatableSize = freeSize + CacheTable.GetTotalCacheSize();
            PeakTotalAllocatableSize = Math.Min(PeakTotalAllocatableSize, totalAllocatableSize);

            return buffer;
        }

        protected override void DoDeallocateBuffer(Buffer buffer)
        {
            lock (Locker)
            {
                DeallocateBufferImpl(buffer);
            }
        }

        private void DeallocateBufferImpl(Buffer buffer)
        {
            Assert.SdkRequires(BitUtil.IsPowerOfTwo(buffer.Length));

            BuddyHeap.Free(buffer);
        }

        protected override CacheHandle DoRegisterCache(Buffer buffer, BufferAttribute attribute)
        {
            lock (Locker)
            {
                return RegisterCacheImpl(buffer, attribute);
            }
        }

        private CacheHandle RegisterCacheImpl(Buffer buffer, BufferAttribute attribute)
        {
            CacheHandle handle;

            // Try to register the handle.
            while (!CacheTable.Register(out handle, buffer, attribute))
            {
                // Unregister a buffer and try registering again.
                RetriedCount++;
                if (!CacheTable.UnregisterOldest(out Buffer deallocateBuffer, attribute))
                {
                    // Can't unregister any existing buffers.
                    // Register the input buffer to /dev/null.
                    DeallocateBufferImpl(buffer);
                    return CacheTable.PublishCacheHandle();
                }

                // Deallocate the unregistered buffer.
                DeallocateBufferImpl(deallocateBuffer);
            }

            return handle;
        }

        protected override Buffer DoAcquireCache(CacheHandle handle)
        {
            lock (Locker)
            {
                return AcquireCacheImpl(handle);
            }
        }

        private Buffer AcquireCacheImpl(CacheHandle handle)
        {
            if (CacheTable.Unregister(out Buffer range, handle))
            {
                int totalAllocatableSize = (int)BuddyHeap.GetTotalFreeSize() + CacheTable.GetTotalCacheSize();
                PeakTotalAllocatableSize = Math.Min(PeakTotalAllocatableSize, totalAllocatableSize);
            }
            else
            {
                range = Buffer.Empty;
            }

            return range;
        }

        protected override int DoGetTotalSize()
        {
            return TotalSize;
        }

        protected override int DoGetFreeSize()
        {
            lock (Locker)
            {
                return GetFreeSizeImpl();
            }
        }

        private int GetFreeSizeImpl()
        {
            return (int)BuddyHeap.GetTotalFreeSize();
        }

        protected override int DoGetTotalAllocatableSize()
        {
            lock (Locker)
            {
                return GetTotalAllocatableSizeImpl();
            }
        }

        private int GetTotalAllocatableSizeImpl()
        {
            return GetFreeSizeImpl() + CacheTable.GetTotalCacheSize();
        }

        protected override int DoGetFreeSizePeak()
        {
            lock (Locker)
            {
                return GetFreeSizePeakImpl();
            }
        }

        private int GetFreeSizePeakImpl()
        {
            return PeakFreeSize;
        }

        protected override int DoGetTotalAllocatableSizePeak()
        {
            lock (Locker)
            {
                return GetTotalAllocatableSizePeakImpl();
            }
        }

        private int GetTotalAllocatableSizePeakImpl()
        {
            return PeakTotalAllocatableSize;
        }

        protected override int DoGetRetriedCount()
        {
            lock (Locker)
            {
                return GetRetriedCountImpl();
            }
        }

        private int GetRetriedCountImpl()
        {
            return RetriedCount;
        }

        protected override void DoClearPeak()
        {
            lock (Locker)
            {
                ClearPeakImpl();
            }
        }

        private void ClearPeakImpl()
        {
            PeakFreeSize = GetFreeSizeImpl();
            PeakTotalAllocatableSize = GetTotalAllocatableSizeImpl();
            RetriedCount = 0;
        }
    }
}