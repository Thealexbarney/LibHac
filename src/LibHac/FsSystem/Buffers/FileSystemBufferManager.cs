using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Os;
using LibHac.Util;
using Buffer = LibHac.Mem.Buffer;
using CacheHandle = System.UInt64;

// ReSharper disable once CheckNamespace
namespace LibHac.FsSystem;

/// <summary>
/// An <see cref="IBufferManager"/> that uses a <see cref="FileSystemBuddyHeap"/> as an allocator.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public class FileSystemBufferManager : IBufferManager
{
    private class CacheHandleTable : IDisposable
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

        private Entry[] _entries;
        private int _entryCount;
        private int _entryCountMax;
        private LinkedList<AttrInfo> _attrList;
        private int _cacheCountMin;
        private int _cacheSizeMin;
        private int _totalCacheSize;
        private CacheHandle _currentHandle;

        public CacheHandleTable()
        {
            _attrList = new LinkedList<AttrInfo>();
        }

        public void Dispose()
        {
            FinalizeObject();
        }

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
            Assert.SdkRequiresNull(_entries);

            // Note: We don't have the option of using an external Entry buffer like the original C++ code
            // because Entry includes managed references so we can't cast a byte* to Entry* without pinning.
            // If we don't have an external buffer, try to allocate an internal one.

            _entries = new Entry[maxCacheCount];

            if (_entries == null)
            {
                return ResultFs.AllocationMemoryFailedInFileSystemBufferManagerA.Log();
            }

            // Set entries.
            _entryCount = 0;
            _entryCountMax = maxCacheCount;

            Assert.SdkNotNull(_entries);

            _cacheCountMin = maxCacheCount / 16;
            _cacheSizeMin = _cacheCountMin * 0x100;

            return Result.Success;
        }

        public void FinalizeObject()
        {
            if (_entries is null)
                return;

            Assert.SdkAssert(_entryCount == 0);

            _attrList.Clear();
            _entries = null;
            _totalCacheSize = 0;
        }

        // ReSharper disable once UnusedParameter.Local
        private int GetCacheCountMin(BufferAttribute attr)
        {
            return _cacheCountMin;
        }

        // ReSharper disable once UnusedParameter.Local
        private int GetCacheSizeMin(BufferAttribute attr)
        {
            return _cacheSizeMin;
        }

        public bool Register(out CacheHandle handle, Buffer buffer, BufferAttribute attr)
        {
            UnsafeHelpers.SkipParamInit(out handle);

            // Validate pre-conditions.
            Assert.SdkRequiresNotNull(_entries);
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
                _attrList.AddLast(newInfo);
            }

            _totalCacheSize += buffer.Length;
            handle = entry.GetHandle();
            return true;
        }

        public bool Unregister(out Buffer buffer, CacheHandle handle)
        {
            // Validate pre-conditions.
            Unsafe.SkipInit(out buffer);
            Assert.SdkRequiresNotNull(_entries);
            Assert.SdkRequiresNotNull(ref buffer);

            UnsafeHelpers.SkipParamInit(out buffer);

            // Find the lower bound for the entry.
            for (int i = 0; i < _entryCount; i++)
            {
                if (_entries[i].GetHandle() == handle)
                {
                    UnregisterCore(out buffer, ref _entries[i]);
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
            Assert.SdkRequiresNotNull(_entries);
            Assert.SdkRequiresNotNull(ref buffer);

            UnsafeHelpers.SkipParamInit(out buffer);

            // If we have no entries, we can't unregister any.
            if (_entryCount == 0)
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
            for (int i = 0; i < _entryCount; i++)
            {
                if (CanUnregister(this, ref _entries[i]))
                {
                    entry = ref _entries[i];
                    break;
                }
            }

            if (Unsafe.IsNullRef(ref entry))
            {
                entry = ref _entries[0];
            }

            Assert.SdkNotNull(ref entry);
            UnregisterCore(out buffer, ref entry);
            return true;
        }

        private void UnregisterCore(out Buffer buffer, ref Entry entry)
        {
            // Validate pre-conditions.
            Unsafe.SkipInit(out buffer);
            Assert.SdkRequiresNotNull(_entries);
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
            Assert.SdkGreaterEqual(_totalCacheSize, entry.GetSize());
            _totalCacheSize -= entry.GetSize();

            // Release the entry.
            buffer = entry.GetBuffer();
            ReleaseEntry(ref entry);
        }

        public CacheHandle PublishCacheHandle()
        {
            Assert.SdkRequires(_entries != null);
            return ++_currentHandle;
        }

        public int GetTotalCacheSize()
        {
            return _totalCacheSize;
        }

        private ref Entry AcquireEntry(Buffer buffer, BufferAttribute attr)
        {
            // Validate pre-conditions.
            Assert.SdkRequiresNotNull(_entries);

            ref Entry entry = ref Unsafe.NullRef<Entry>();
            if (_entryCount < _entryCountMax)
            {
                entry = ref _entries[_entryCount];
                entry.Initialize(PublishCacheHandle(), buffer, attr);
                _entryCount++;
                Assert.SdkAssert(_entryCount == 1 || _entries[_entryCount - 2].GetHandle() < entry.GetHandle());
            }

            return ref entry;
        }

        private void ReleaseEntry(ref Entry entry)
        {
            // Validate pre-conditions.
            Assert.SdkRequiresNotNull(_entries);
            Assert.SdkRequiresNotNull(ref entry);

            // Ensure the entry is valid.
            Span<Entry> entryBuffer = _entries;
            Assert.SdkAssert(!Unsafe.IsAddressLessThan(ref entry, ref MemoryMarshal.GetReference(entryBuffer)));
            Assert.SdkAssert(Unsafe.IsAddressLessThan(ref entry,
                ref Unsafe.Add(ref MemoryMarshal.GetReference(entryBuffer), entryBuffer.Length)));

            // Get the index of the entry.
            int index = Unsafe.ByteOffset(ref MemoryMarshal.GetReference(entryBuffer), ref entry).ToInt32() /
                        Unsafe.SizeOf<Entry>();

            // Copy the entries back by one.
            Span<Entry> source = entryBuffer.Slice(index + 1, _entryCount - (index + 1));
            Span<Entry> dest = entryBuffer.Slice(index);
            source.CopyTo(dest);

            // Decrement our entry count.
            _entryCount--;
        }

        private ref AttrInfo FindAttrInfo(BufferAttribute attr)
        {
            LinkedListNode<AttrInfo> curNode = _attrList.First;

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

    private FileSystemBuddyHeap _buddyHeap;
    private CacheHandleTable _cacheTable;
    private int _totalSize;
    private int _peakFreeSize;
    private int _peakTotalAllocatableSize;
    private int _retriedCount;
    private SdkMutexType _mutex;

    public FileSystemBufferManager()
    {
        _buddyHeap = new FileSystemBuddyHeap();
        _cacheTable = new CacheHandleTable();
        _mutex = new SdkMutexType();
    }

    public override void Dispose()
    {
        _cacheTable.Dispose();
        _buddyHeap.Dispose();
        base.Dispose();
    }

    public Result Initialize(int maxCacheCount, Memory<byte> heapBuffer, int blockSize)
    {
        Result res = _cacheTable.Initialize(maxCacheCount);
        if (res.IsFailure()) return res.Miss();

        res = _buddyHeap.Initialize(heapBuffer, blockSize);
        if (res.IsFailure()) return res.Miss();

        _totalSize = (int)_buddyHeap.GetTotalFreeSize();
        _peakFreeSize = _totalSize;
        _peakTotalAllocatableSize = _totalSize;

        return Result.Success;
    }

    public Result Initialize(int maxCacheCount, Memory<byte> heapBuffer, int blockSize, int maxOrder)
    {
        Result res = _cacheTable.Initialize(maxCacheCount);
        if (res.IsFailure()) return res.Miss();

        res = _buddyHeap.Initialize(heapBuffer, blockSize, maxOrder);
        if (res.IsFailure()) return res.Miss();

        _totalSize = (int)_buddyHeap.GetTotalFreeSize();
        _peakFreeSize = _totalSize;
        _peakTotalAllocatableSize = _totalSize;

        return Result.Success;
    }

    public Result Initialize(int maxCacheCount, Memory<byte> heapBuffer, int blockSize, Memory<byte> workBuffer)
    {
        // Note: We can't use an external buffer for the cache handle table since it contains managed pointers,
        // so pass the work buffer directly to the buddy heap.

        Result res = _cacheTable.Initialize(maxCacheCount);
        if (res.IsFailure()) return res.Miss();

        res = _buddyHeap.Initialize(heapBuffer, blockSize, workBuffer);
        if (res.IsFailure()) return res.Miss();

        _totalSize = (int)_buddyHeap.GetTotalFreeSize();
        _peakFreeSize = _totalSize;
        _peakTotalAllocatableSize = _totalSize;

        return Result.Success;
    }

    public Result Initialize(int maxCacheCount, Memory<byte> heapBuffer, int blockSize, int maxOrder,
        Memory<byte> workBuffer)
    {
        // Note: We can't use an external buffer for the cache handle table since it contains managed pointers,
        // so pass the work buffer directly to the buddy heap.

        Result res = _cacheTable.Initialize(maxCacheCount);
        if (res.IsFailure()) return res.Miss();

        res = _buddyHeap.Initialize(heapBuffer, blockSize, maxOrder, workBuffer);
        if (res.IsFailure()) return res.Miss();

        _totalSize = (int)_buddyHeap.GetTotalFreeSize();
        _peakFreeSize = _totalSize;
        _peakTotalAllocatableSize = _totalSize;

        return Result.Success;
    }

    protected override Buffer DoAllocateBuffer(int size, BufferAttribute attribute)
    {
        using var lk = new ScopedLock<SdkMutexType>(ref _mutex);

        return AllocateBufferImpl(size, attribute);
    }

    private Buffer AllocateBufferImpl(int size, BufferAttribute attribute)
    {
        int order = _buddyHeap.GetOrderFromBytes((nuint)size);
        Assert.SdkAssert(order >= 0);

        // Allocate space on the heap
        Buffer buffer;
        while ((buffer = _buddyHeap.AllocateBufferByOrder(order)).IsNull)
        {
            // Not enough space in heap. Deallocate cached buffer and try again.
            _retriedCount++;

            if (!_cacheTable.UnregisterOldest(out Buffer deallocateBuffer, attribute, size))
            {
                // No cached buffers left to deallocate.
                return Buffer.Empty;
            }

            DeallocateBufferImpl(deallocateBuffer);
        }

        // Successfully allocated a buffer.
        int allocatedSize = (int)_buddyHeap.GetBytesFromOrder(order);
        Assert.SdkAssert(size <= allocatedSize);

        // Update heap stats
        int freeSize = (int)_buddyHeap.GetTotalFreeSize();
        _peakFreeSize = Math.Min(_peakFreeSize, freeSize);

        int totalAllocatableSize = freeSize + _cacheTable.GetTotalCacheSize();
        _peakTotalAllocatableSize = Math.Min(_peakTotalAllocatableSize, totalAllocatableSize);

        return buffer;
    }

    protected override void DoDeallocateBuffer(Buffer buffer)
    {
        using var lk = new ScopedLock<SdkMutexType>(ref _mutex);

        DeallocateBufferImpl(buffer);
    }

    private void DeallocateBufferImpl(Buffer buffer)
    {
        Assert.SdkRequires(BitUtil.IsPowerOfTwo(buffer.Length));

        _buddyHeap.Free(buffer);
    }

    protected override CacheHandle DoRegisterCache(Buffer buffer, BufferAttribute attribute)
    {
        using var lk = new ScopedLock<SdkMutexType>(ref _mutex);

        return RegisterCacheImpl(buffer, attribute);
    }

    private CacheHandle RegisterCacheImpl(Buffer buffer, BufferAttribute attribute)
    {
        // ReSharper disable once RedundantAssignment
        CacheHandle handle = 0;

        // Try to register the handle.
        while (!_cacheTable.Register(out handle, buffer, attribute))
        {
            // Unregister a buffer and try registering again.
            _retriedCount++;
            if (!_cacheTable.UnregisterOldest(out Buffer deallocateBuffer, attribute))
            {
                // Can't unregister any existing buffers.
                // Register the input buffer to /dev/null.
                DeallocateBufferImpl(buffer);
                return _cacheTable.PublishCacheHandle();
            }

            // Deallocate the unregistered buffer.
            DeallocateBufferImpl(deallocateBuffer);
        }

        return handle;
    }

    protected override Buffer DoAcquireCache(CacheHandle handle)
    {
        using var lk = new ScopedLock<SdkMutexType>(ref _mutex);

        return AcquireCacheImpl(handle);
    }

    private Buffer AcquireCacheImpl(CacheHandle handle)
    {
        if (_cacheTable.Unregister(out Buffer range, handle))
        {
            int totalAllocatableSize = (int)_buddyHeap.GetTotalFreeSize() + _cacheTable.GetTotalCacheSize();
            _peakTotalAllocatableSize = Math.Min(_peakTotalAllocatableSize, totalAllocatableSize);
        }
        else
        {
            range = Buffer.Empty;
        }

        return range;
    }

    protected override int DoGetTotalSize()
    {
        return _totalSize;
    }

    protected override int DoGetFreeSize()
    {
        using var lk = new ScopedLock<SdkMutexType>(ref _mutex);

        return GetFreeSizeImpl();
    }

    private int GetFreeSizeImpl()
    {
        return (int)_buddyHeap.GetTotalFreeSize();
    }

    protected override int DoGetTotalAllocatableSize()
    {
        using var lk = new ScopedLock<SdkMutexType>(ref _mutex);

        return GetTotalAllocatableSizeImpl();
    }

    private int GetTotalAllocatableSizeImpl()
    {
        return GetFreeSizeImpl() + _cacheTable.GetTotalCacheSize();
    }

    protected override int DoGetFreeSizePeak()
    {
        using var lk = new ScopedLock<SdkMutexType>(ref _mutex);

        return GetFreeSizePeakImpl();
    }

    private int GetFreeSizePeakImpl()
    {
        return _peakFreeSize;
    }

    protected override int DoGetTotalAllocatableSizePeak()
    {
        using var lk = new ScopedLock<SdkMutexType>(ref _mutex);

        return GetTotalAllocatableSizePeakImpl();
    }

    private int GetTotalAllocatableSizePeakImpl()
    {
        return _peakTotalAllocatableSize;
    }

    protected override int DoGetRetriedCount()
    {
        using var lk = new ScopedLock<SdkMutexType>(ref _mutex);

        return GetRetriedCountImpl();
    }

    private int GetRetriedCountImpl()
    {
        return _retriedCount;
    }

    protected override void DoClearPeak()
    {
        using var lk = new ScopedLock<SdkMutexType>(ref _mutex);

        ClearPeakImpl();
    }

    private void ClearPeakImpl()
    {
        _peakFreeSize = GetFreeSizeImpl();
        _peakTotalAllocatableSize = GetTotalAllocatableSizeImpl();
        _retriedCount = 0;
    }
}