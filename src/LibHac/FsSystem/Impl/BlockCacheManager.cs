using System;
using LibHac.Diag;
using LibHac.Fs;

using Buffer = LibHac.Mem.Buffer;
using CacheHandle = System.Int64;

namespace LibHac.FsSystem.Impl;

public interface IBlockCacheManagerEntry<TRange> where TRange : struct, IBlockCacheManagerRange
{
    TRange Range { get; }
    bool IsValid { get; set; }
    bool IsWriteBack { get; set; }
    bool IsCached { get; set; }
    bool IsFlushing { set; }
    CacheHandle Handle { get; set; }
    Buffer Buffer { get; set; }
    short Age { get; set; }

    void Invalidate();
    bool IsAllocated();
}

public interface IBlockCacheManagerRange
{
    long Offset { get; }
    long GetEndOffset();
}

public class BlockCacheManager<TEntry, TRange> : IDisposable
    where TEntry : struct, IBlockCacheManagerEntry<TRange>
    where TRange : struct, IBlockCacheManagerRange
{
    private IBufferManager _allocator;
    private TEntry[] _cacheEntries;
    private int _cacheEntriesCount;

    public void Dispose()
    {
    }

    public Result Initialize(IBufferManager allocator, int maxCacheEntries)
    {
        Assert.SdkRequiresNull(_allocator);
        Assert.SdkRequiresNull(_cacheEntries);
        Assert.SdkRequiresNotNull(allocator);

        if (maxCacheEntries > 0)
        {
            _cacheEntries = new TEntry[maxCacheEntries];
        }

        _allocator = allocator;
        _cacheEntriesCount = maxCacheEntries;

        return Result.Success;
    }

    public void FinalizeObject()
    {
        _allocator = null;
        _cacheEntries = null;
        _cacheEntriesCount = 0;
    }

    public ref readonly TEntry this[int index]
    {
        get
        {
            Assert.SdkRequires(IsInitialized());
            Assert.SdkRequiresInRange(index, 0, _cacheEntriesCount);

            return ref _cacheEntries[index];
        }
    }

    public bool IsInitialized() => _allocator is not null;

    public IBufferManager GetAllocator() => _allocator;

    public int GetCount() => _cacheEntriesCount;

    public void GetEmptyCacheIndex(out int emptyIndex, out int leastRecentlyUsedIndex)
    {
        int empty = -1;
        int leastRecentlyUsed = -1;

        for (int i = 0; i < GetCount(); i++)
        {
            if (!_cacheEntries[i].IsValid)
            {
                // Get the first empty cache index
                if (empty < 0)
                    empty = i;
            }
            else
            {
                // Protect against overflow
                if (_cacheEntries[i].Age != short.MaxValue)
                {
                    _cacheEntries[i].Age++;
                }

                // Get the cache index that was least recently used
                if (leastRecentlyUsed < 0 || _cacheEntries[leastRecentlyUsed].Age < _cacheEntries[i].Age)
                {
                    leastRecentlyUsed = i;
                }
            }
        }

        emptyIndex = empty;
        leastRecentlyUsedIndex = leastRecentlyUsed;
    }

    public void InvalidateCacheEntry(int index)
    {
        Assert.SdkRequires(IsInitialized());
        Assert.SdkRequiresLess(index, GetCount());

        ref TEntry entry = ref _cacheEntries[index];

        Assert.SdkAssert(entry.IsValid);

        if (entry.IsWriteBack)
        {
            Assert.SdkAssert(!entry.Buffer.IsNull && entry.Handle == 0);
            _allocator.DeallocateBuffer(entry.Buffer);
            entry.Buffer = new Buffer();
        }
        else
        {
            Assert.SdkAssert(entry.Buffer.IsNull && entry.Handle != 0);
            Buffer buffer = _allocator.AcquireCache(entry.Handle);

            if (!buffer.IsNull)
                _allocator.DeallocateBuffer(buffer);
        }

        entry.IsValid = false;
        entry.Invalidate();
    }

    public void Invalidate()
    {
        Assert.SdkRequires(IsInitialized());

        int count = _cacheEntriesCount;
        for (int i = 0; i < count; i++)
        {
            if (_cacheEntries[i].IsValid)
                InvalidateCacheEntry(i);
        }
    }

    public void ReleaseCacheEntry(int index, Buffer buffer)
    {
        ReleaseCacheEntry(ref _cacheEntries[index], buffer);
    }

    public void ReleaseCacheEntry(ref TEntry entry, Buffer buffer)
    {
        Assert.SdkRequires(IsInitialized());

        _allocator.DeallocateBuffer(buffer);
        entry.IsValid = false;
        entry.IsCached = false;
    }

    public void RegisterCacheEntry(int index, Buffer buffer, IBufferManager.BufferAttribute attribute)
    {
        Assert.SdkRequires(IsInitialized());

        ref TEntry entry = ref _cacheEntries[index];

        if (entry.IsWriteBack)
        {
            entry.Handle = 0;
            entry.Buffer = buffer;
        }
        else
        {
            entry.Handle = _allocator.RegisterCache(buffer, attribute);
            entry.Buffer = new Buffer();
        }
    }

    public void AcquireCacheEntry(out TEntry outEntry, out Buffer outBuffer, int index)
    {
        Assert.SdkRequires(IsInitialized());
        Assert.SdkRequiresLess(index, GetCount());

        ref TEntry entry = ref _cacheEntries[index];

        if (entry.IsWriteBack)
        {
            outBuffer = entry.Buffer;
        }
        else
        {
            outBuffer = _allocator.AcquireCache(entry.Handle);
        }

        outEntry = entry;

        Assert.SdkAssert(outEntry.IsValid);
        Assert.SdkAssert(outEntry.IsCached);

        entry.IsValid = false;
        entry.Handle = 0;
        entry.Buffer = new Buffer();
        entry.Age = 0;

        outEntry.IsValid = true;
        outEntry.Handle = 0;
        outEntry.Buffer = new Buffer();
        outEntry.Age = 0;
    }

    private bool ExistsRedundantCacheEntry(in TEntry entry)
    {
        Assert.SdkRequires(IsInitialized());

        for (int i = 0; i < GetCount(); i++)
        {
            ref TEntry currentEntry = ref _cacheEntries[i];

            if (currentEntry.IsAllocated() &&
                currentEntry.Range.Offset < entry.Range.GetEndOffset() &&
                entry.Range.Offset < currentEntry.Range.GetEndOffset())
            {
                return true;
            }
        }

        return false;
    }

    public bool SetCacheEntry(int index, in TEntry entry, Buffer buffer)
    {
        return SetCacheEntry(index, in entry, buffer, new IBufferManager.BufferAttribute());
    }

    public bool SetCacheEntry(int index, in TEntry entry, Buffer buffer, IBufferManager.BufferAttribute attribute)
    {
        Assert.SdkRequires(IsInitialized());
        Assert.SdkRequiresInRange(index, 0, _cacheEntriesCount);

        _cacheEntries[index] = entry;

        Assert.SdkAssert(entry.IsValid);
        Assert.SdkAssert(entry.IsCached);
        Assert.SdkAssert(entry.Handle == 0);
        Assert.SdkAssert(entry.Buffer.IsNull);

        // Get rid of the input entry if it overlaps with anything currently in the cache
        if (ExistsRedundantCacheEntry(in entry))
        {
            ReleaseCacheEntry(index, buffer);
            return false;
        }

        RegisterCacheEntry(index, buffer, attribute);
        return true;
    }

    public void SetFlushing(int index, bool isFlushing)
    {
        _cacheEntries[index].IsFlushing = isFlushing;
    }

    public void SetWriteBack(int index, bool isWriteBack)
    {
        _cacheEntries[index].IsWriteBack = isWriteBack;
    }
}