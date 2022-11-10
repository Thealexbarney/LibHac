using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSystem.Buffers;
using LibHac.Os;
using LibHac.Util;

using Buffer = LibHac.Mem.Buffer;
using CacheHandle = System.UInt64;

namespace LibHac.FsSystem;

/// <summary>
/// An <see cref="IStorage"/> that provides buffered access to a base <see cref="IStorage"/>.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public class BufferedStorage : IStorage
{
    private const long InvalidOffset = long.MaxValue;
    private const int InvalidIndex = -1;

    /// <summary>
    /// Caches a single block of data for a <see cref="FsSystem.BufferedStorage"/>
    /// </summary>
    private struct Cache : IDisposable
    {
        private ref struct FetchParameter
        {
            public long Offset;
            public Span<byte> Buffer;
        }

        private BufferedStorage _bufferedStorage;
        private Buffer _memoryRange;
        private CacheHandle _cacheHandle;
        private long _offset;

        // Todo: Atomic<T> type for these two bools?
        private bool _isValid;
        private bool _isDirty;
        private int _referenceCount;

        // Instead of storing pointers to the next Cache we store indexes
        private int _index;
        private int _nextIndex;
        private int _prevIndex;

        private ref Cache Next => ref _bufferedStorage._caches[_nextIndex];
        private ref Cache Prev => ref _bufferedStorage._caches[_prevIndex];

        public Cache(int index)
        {
            _bufferedStorage = null;
            _memoryRange = Buffer.Empty;
            _cacheHandle = 0;
            _offset = InvalidOffset;
            _isValid = false;
            _isDirty = false;
            _referenceCount = 1;
            _index = index;
            _nextIndex = InvalidIndex;
            _prevIndex = InvalidIndex;
        }

        public void Dispose()
        {
            FinalizeObject();
        }

        public void Initialize(BufferedStorage bufferedStorage)
        {
            Assert.SdkRequiresNotNull(bufferedStorage);
            Assert.SdkRequires(_bufferedStorage == null);

            _bufferedStorage = bufferedStorage;
            Link();
        }

        public void FinalizeObject()
        {
            Assert.SdkRequiresNotNull(_bufferedStorage);
            Assert.SdkRequiresNotNull(_bufferedStorage._bufferManager);
            Assert.SdkRequiresEqual(0, _referenceCount);

            // If we're valid, acquire our cache handle and free our buffer.
            if (IsValid())
            {
                IBufferManager bufferManager = _bufferedStorage._bufferManager;
                if (!_isDirty)
                {
                    Assert.SdkAssert(_memoryRange.IsNull);
                    _memoryRange = bufferManager.AcquireCache(_cacheHandle);
                }

                if (!_memoryRange.IsNull)
                {
                    bufferManager.DeallocateBuffer(_memoryRange);
                    _memoryRange = Buffer.Empty;
                }
            }

            // Clear all our members.
            _bufferedStorage = null;
            _offset = InvalidOffset;
            _isValid = false;
            _isDirty = false;
            _nextIndex = InvalidIndex;
            _prevIndex = InvalidIndex;
        }

        /// <summary>
        /// Decrements the ref-count and adds the <see cref="Cache"/> to its <see cref="FsSystem.BufferedStorage"/>'s
        /// fetch list if the <see cref="Cache"/> has no more references, and registering the buffer with
        /// the <see cref="IBufferManager"/> if not dirty.
        /// </summary>
        public void Link()
        {
            Assert.SdkRequiresNotNull(_bufferedStorage);
            Assert.SdkRequiresNotNull(_bufferedStorage._bufferManager);
            Assert.SdkRequiresLess(0, _referenceCount);

            _referenceCount--;
            if (_referenceCount == 0)
            {
                Assert.SdkAssert(_nextIndex == InvalidIndex);
                Assert.SdkAssert(_prevIndex == InvalidIndex);

                // If the fetch list is empty we can simply add it as the only cache in the list.
                if (_bufferedStorage._nextFetchCacheIndex == InvalidIndex)
                {
                    _bufferedStorage._nextFetchCacheIndex = _index;
                    _nextIndex = _index;
                    _prevIndex = _index;
                }
                else
                {
                    // Check against a cache being registered twice.
                    ref Cache cache = ref _bufferedStorage.NextFetchCache;
                    do
                    {
                        if (cache.IsValid() && Hits(cache._offset, _bufferedStorage._blockSize))
                        {
                            _isValid = false;
                            break;
                        }

                        cache = ref cache.Next;
                    } while (cache._index != _bufferedStorage._nextFetchCacheIndex);

                    // Verify the end of the fetch list loops back to the start.
                    Assert.SdkAssert(_bufferedStorage.NextFetchCache._prevIndex != InvalidIndex);
                    Assert.SdkEqual(_bufferedStorage.NextFetchCache.Prev._nextIndex,
                        _bufferedStorage._nextFetchCacheIndex);

                    // Link into the fetch list.
                    _nextIndex = _bufferedStorage._nextFetchCacheIndex;
                    _prevIndex = _bufferedStorage.NextFetchCache._prevIndex;
                    Next._prevIndex = _index;
                    Prev._nextIndex = _index;

                    // Insert invalid caches at the start of the list so they'll
                    // be used first when a fetch cache is needed.
                    if (!IsValid())
                        _bufferedStorage._nextFetchCacheIndex = _index;
                }

                // If we're not valid, clear our offset.
                if (!IsValid())
                {
                    _offset = InvalidOffset;
                    _isDirty = false;
                }

                // Ensure our buffer state is coherent.
                // We can let go of our buffer if it's not dirty, allowing the buffer to be used elsewhere if needed.
                if (!_memoryRange.IsNull && !IsDirty())
                {
                    // If we're valid, register the buffer with the buffer manager for possible later retrieval.
                    // Otherwise the the data in the buffer isn't needed, so deallocate it.
                    if (IsValid())
                    {
                        _cacheHandle = _bufferedStorage._bufferManager.RegisterCache(_memoryRange,
                            new IBufferManager.BufferAttribute());
                    }
                    else
                    {
                        _bufferedStorage._bufferManager.DeallocateBuffer(_memoryRange);
                    }

                    _memoryRange = default;
                }
            }
        }

        /// <summary>
        /// Increments the ref-count and removes the <see cref="Cache"/> from its
        /// <see cref="FsSystem.BufferedStorage"/>'s fetch list if needed.
        /// </summary>
        public void Unlink()
        {
            Assert.SdkRequiresNotNull(_bufferedStorage);
            Assert.SdkRequiresGreaterEqual(_referenceCount, 0);

            _referenceCount++;
            if (_referenceCount == 1)
            {
                // If we're the first to grab this Cache, the Cache should be in the BufferedStorage's fetch list.
                Assert.SdkNotEqual(_nextIndex, InvalidIndex);
                Assert.SdkNotEqual(_prevIndex, InvalidIndex);
                Assert.SdkEqual(Next._prevIndex, _index);
                Assert.SdkEqual(Prev._nextIndex, _index);

                // Set the new fetch list head if this Cache is the current head
                if (_bufferedStorage._nextFetchCacheIndex == _index)
                {
                    if (_nextIndex != _index)
                    {
                        _bufferedStorage._nextFetchCacheIndex = _nextIndex;
                    }
                    else
                    {
                        _bufferedStorage._nextFetchCacheIndex = InvalidIndex;
                    }
                }

                _bufferedStorage._nextAcquireCacheIndex = _index;

                Next._prevIndex = _prevIndex;
                Prev._nextIndex = _nextIndex;
                _nextIndex = InvalidIndex;
                _prevIndex = InvalidIndex;
            }
            else
            {
                Assert.SdkEqual(_nextIndex, InvalidIndex);
                Assert.SdkEqual(_prevIndex, InvalidIndex);
            }
        }

        /// <summary>
        /// Reads the data from the base <see cref="IStorage"/> contained in this <see cref="Cache"/>'s buffer.
        /// The <see cref="Cache"/> must contain valid data before calling, and the <paramref name="offset"/>
        /// must be inside the block of data held by this <see cref="Cache"/>.
        /// </summary>
        /// <param name="offset">The offset in the base <see cref="IStorage"/> to be read from.</param>
        /// <param name="buffer">The buffer in which to place the read data.</param>
        public readonly void Read(long offset, Span<byte> buffer)
        {
            Assert.SdkRequiresNotNull(_bufferedStorage);
            Assert.SdkRequiresEqual(_nextIndex, InvalidIndex);
            Assert.SdkRequiresEqual(_prevIndex, InvalidIndex);
            Assert.SdkRequires(IsValid());
            Assert.SdkRequires(Hits(offset, 1));
            Assert.SdkRequires(!_memoryRange.IsNull);

            long readOffset = offset - _offset;
            long readableOffsetMax = _bufferedStorage._blockSize - buffer.Length;

            Assert.SdkLessEqual(0, readOffset);
            Assert.SdkLessEqual(readOffset, readableOffsetMax);

            Span<byte> cacheBuffer = _memoryRange.Span.Slice((int)readOffset, buffer.Length);
            cacheBuffer.CopyTo(buffer);
        }

        /// <summary>
        /// Buffers data to be written to the base <see cref="IStorage"/> when this <see cref="Cache"/> is flushed.
        /// The <see cref="Cache"/> must contain valid data before calling, and the <paramref name="offset"/>
        /// must be inside the block of data held by this <see cref="Cache"/>.
        /// </summary>
        /// <param name="offset">The offset in the base <see cref="IStorage"/> to be written to.</param>
        /// <param name="buffer">The buffer containing the data to be written.</param>
        public void Write(long offset, ReadOnlySpan<byte> buffer)
        {
            Assert.SdkRequiresNotNull(_bufferedStorage);
            Assert.SdkRequiresEqual(_nextIndex, InvalidIndex);
            Assert.SdkRequiresEqual(_prevIndex, InvalidIndex);
            Assert.SdkRequires(IsValid());
            Assert.SdkRequires(Hits(offset, 1));
            Assert.SdkRequires(!_memoryRange.IsNull);

            long writeOffset = offset - _offset;
            long writableOffsetMax = _bufferedStorage._blockSize - buffer.Length;

            Assert.SdkLessEqual(0, writeOffset);
            Assert.SdkLessEqual(writeOffset, writableOffsetMax);

            Span<byte> cacheBuffer = _memoryRange.Span.Slice((int)writeOffset, buffer.Length);
            buffer.CopyTo(cacheBuffer);
            _isDirty = true;
        }

        /// <summary>
        /// If this <see cref="Cache"/> is dirty, flushes its data to the base <see cref="IStorage"/>.
        /// The <see cref="Cache"/> must contain valid data before calling.
        /// </summary>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        public Result Flush()
        {
            Assert.SdkRequiresNotNull(_bufferedStorage);
            Assert.SdkRequiresEqual(_nextIndex, InvalidIndex);
            Assert.SdkRequiresEqual(_prevIndex, InvalidIndex);
            Assert.SdkRequires(IsValid());

            if (_isDirty)
            {
                Assert.SdkRequires(!_memoryRange.IsNull);

                long baseSize = _bufferedStorage._baseStorageSize;
                long blockSize = _bufferedStorage._blockSize;
                long flushSize = Math.Min(blockSize, baseSize - _offset);

                ref ValueSubStorage baseStorage = ref _bufferedStorage._baseStorage;
                Span<byte> cacheBuffer = _memoryRange.Span;
                Assert.SdkEqual(flushSize, cacheBuffer.Length);

                Result rc = baseStorage.Write(_offset, cacheBuffer);
                if (rc.IsFailure()) return rc.Miss();

                _isDirty = false;

                BufferManagerUtility.EnableBlockingBufferManagerAllocation();
            }

            return Result.Success;
        }

        /// <summary>
        /// Prepares this <see cref="Cache"/> to fetch a new block from the base <see cref="IStorage"/>.
        /// If the caller has the only reference to this Cache,
        /// the Cache's buffer will be flushed and the Cache invalidated. While the Cache is
        /// prepared to fetch, <see cref="SharedCache"/> will skip it when iterating all the Caches.
        /// </summary>
        /// <returns>The <see cref="Result"/> of any attempted flush, and <see langword="true"/> if the
        /// <see cref="Cache"/> is prepared to fetch; <see langword="false"/> if not.</returns>
        public (Result Result, bool IsPrepared) PrepareFetch()
        {
            Assert.SdkRequiresNotNull(_bufferedStorage);
            Assert.SdkRequiresNotNull(_bufferedStorage._bufferManager);
            Assert.SdkRequiresEqual(_nextIndex, InvalidIndex);
            Assert.SdkRequiresEqual(_prevIndex, InvalidIndex);
            Assert.SdkRequires(IsValid());
            Assert.SdkRequires(_bufferedStorage._mutex.IsLockedByCurrentThread());

            (Result Result, bool IsPrepared) result = (Result.Success, false);

            if (_referenceCount == 1)
            {
                result.Result = Flush();

                if (result.Result.IsSuccess())
                {
                    _isValid = false;
                    _referenceCount = 0;
                    result.IsPrepared = true;
                }
            }

            return result;
        }

        /// <summary>
        /// Marks the <see cref="Cache"/> as unprepared to cache a new block,
        /// allowing <see cref="SharedCache"/> to acquire it while iterating.
        /// </summary>
        public void UnprepareFetch()
        {
            Assert.SdkRequiresNotNull(_bufferedStorage);
            Assert.SdkRequiresNotNull(_bufferedStorage._bufferManager);
            Assert.SdkRequiresEqual(_nextIndex, InvalidIndex);
            Assert.SdkRequiresEqual(_prevIndex, InvalidIndex);
            Assert.SdkRequires(!IsValid());
            Assert.SdkRequires(!_isDirty);
            Assert.SdkRequires(_bufferedStorage._mutex.IsLockedByCurrentThread());

            _isValid = true;
            _referenceCount = 1;
        }

        /// <summary>
        /// Reads the storage block containing the specified offset into this <see cref="Cache"/>'s buffer.
        /// </summary>
        /// <param name="offset">An offset in the block to fetch.</param>
        /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
        /// <see cref="ResultFs.BufferAllocationFailed"/>: A buffer could not be allocated.</returns>
        public Result Fetch(long offset)
        {
            Assert.SdkRequiresNotNull(_bufferedStorage);
            Assert.SdkRequiresNotNull(_bufferedStorage._bufferManager);
            Assert.SdkRequiresEqual(_nextIndex, InvalidIndex);
            Assert.SdkRequiresEqual(_prevIndex, InvalidIndex);
            Assert.SdkRequires(!IsValid());
            Assert.SdkRequires(!_isDirty);

            Result rc;

            // Make sure this Cache has an allocated buffer
            if (_memoryRange.IsNull)
            {
                rc = AllocateFetchBuffer();
                if (rc.IsFailure()) return rc.Miss();
            }

            CalcFetchParameter(out FetchParameter fetchParam, offset);

            rc = _bufferedStorage._baseStorage.Read(fetchParam.Offset, fetchParam.Buffer);
            if (rc.IsFailure()) return rc.Miss();

            _offset = fetchParam.Offset;
            Assert.SdkAssert(Hits(offset, 1));

            return Result.Success;
        }

        /// <summary>
        /// Fills this <see cref="Cache"/>'s buffer from an input buffer containing a block of data
        /// read from the base <see cref="IStorage"/>.
        /// </summary>
        /// <param name="offset">The start offset of the block in the base <see cref="IStorage"/>
        /// that the data was read from.</param>
        /// <param name="buffer">A buffer containing the data read from the base <see cref="IStorage"/>.</param>
        /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
        /// <see cref="ResultFs.BufferAllocationFailed"/>: A buffer could not be allocated.</returns>
        public Result FetchFromBuffer(long offset, ReadOnlySpan<byte> buffer)
        {
            Assert.SdkRequiresNotNull(_bufferedStorage);
            Assert.SdkRequiresNotNull(_bufferedStorage._bufferManager);
            Assert.SdkRequiresEqual(_nextIndex, InvalidIndex);
            Assert.SdkRequiresEqual(_prevIndex, InvalidIndex);
            Assert.SdkRequires(!IsValid());
            Assert.SdkRequires(!_isDirty);
            Assert.SdkRequiresAligned((ulong)offset, (int)_bufferedStorage._blockSize);

            // Make sure this Cache has an allocated buffer
            if (_memoryRange.IsNull)
            {
                Result rc = AllocateFetchBuffer();
                if (rc.IsFailure()) return rc.Miss();
            }

            CalcFetchParameter(out FetchParameter fetchParam, offset);
            Assert.SdkEqual(fetchParam.Offset, offset);
            Assert.SdkLessEqual(fetchParam.Buffer.Length, buffer.Length);

            buffer.Slice(0, fetchParam.Buffer.Length).CopyTo(fetchParam.Buffer);
            _offset = fetchParam.Offset;
            Assert.SdkAssert(Hits(offset, 1));

            return Result.Success;
        }

        /// <summary>
        /// Tries to retrieve the cache's memory buffer from the <see cref="IBufferManager"/>.
        /// </summary>
        /// <returns><see langword="true"/> if the memory buffer was available.
        /// <see langword="false"/> if the buffer has been evicted from the <see cref="IBufferManager"/> cache.</returns>
        public bool TryAcquireCache()
        {
            Assert.SdkRequiresNotNull(_bufferedStorage);
            Assert.SdkRequiresNotNull(_bufferedStorage._bufferManager);
            Assert.SdkRequires(IsValid());

            if (!_memoryRange.IsNull)
                return true;

            _memoryRange = _bufferedStorage._bufferManager.AcquireCache(_cacheHandle);
            _isValid = !_memoryRange.IsNull;
            return _isValid;
        }

        /// <summary>
        /// Invalidates the data in this <see cref="Cache"/>.
        /// </summary>
        public void Invalidate()
        {
            Assert.SdkRequiresNotNull(_bufferedStorage);
            _isValid = false;
        }

        /// <summary>
        /// Does this <see cref="Cache"/> have a valid buffer or are there any references to this Cache?
        /// </summary>
        /// <returns><see langword="true"/> if this <see cref="Cache"/> has a valid buffer
        /// or if anybody currently has a reference to this Cache. Otherwise, <see langword="false"/>.</returns>
        public readonly bool IsValid()
        {
            Assert.SdkRequiresNotNull(_bufferedStorage);

            return _isValid || _referenceCount > 0;
        }

        /// <summary>
        /// Does this <see cref="Cache"/> have modified data that needs
        /// to be flushed to the base <see cref="IStorage"/>?
        /// </summary>
        /// <returns><see langword="true"/> if this <see cref="Cache"/> has unflushed data.
        /// Otherwise, <see langword="false"/>.</returns>
        public readonly bool IsDirty()
        {
            Assert.SdkRequiresNotNull(_bufferedStorage);

            return _isDirty;
        }

        /// <summary>
        /// Checks if the <see cref="Cache"/> covers any of the specified range.
        /// </summary>
        /// <param name="offset">The start offset of the range to check.</param>
        /// <param name="size">The size of the range to check.</param>
        /// <returns><see langword="true"/> if this <see cref="Cache"/>'s range covers any of the input range.
        /// Otherwise, <see langword="false"/>.</returns>
        public readonly bool Hits(long offset, long size)
        {
            Assert.SdkRequiresNotNull(_bufferedStorage);

            long blockSize = _bufferedStorage._blockSize;
            return (offset < _offset + blockSize) && (_offset < offset + size);
        }

        /// <summary>
        /// Allocates a buffer for this <see cref="Cache"/>.
        /// Should only be called when there is not already an allocated buffer.
        /// </summary>
        /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
        /// <see cref="ResultFs.BufferAllocationFailed"/>: A buffer could not be allocated.</returns>
        private Result AllocateFetchBuffer()
        {
            IBufferManager bufferManager = _bufferedStorage._bufferManager;
            Assert.SdkAssert(bufferManager.AcquireCache(_cacheHandle).IsNull);

            Result rc = BufferManagerUtility.AllocateBufferUsingBufferManagerContext(out _memoryRange,
                bufferManager, (int)_bufferedStorage._blockSize, new IBufferManager.BufferAttribute(),
                static (in Buffer buffer) => !buffer.IsNull);

            // Clear the current MemoryRange if allocation failed.
            if (rc.IsFailure())
            {
                _memoryRange = Buffer.Empty;
                return rc.Log();
            }

            return Result.Success;
        }

        /// <summary>
        /// Calculates the parameters used to fetch the block containing the
        /// specified offset in the base <see cref="IStorage"/>.
        /// </summary>
        /// <param name="fetchParam">When this function returns, contains
        /// the parameters that can be used to fetch the block.</param>
        /// <param name="offset">The offset to be fetched.</param>
        private readonly void CalcFetchParameter(out FetchParameter fetchParam, long offset)
        {
            long blockSize = _bufferedStorage._blockSize;
            long storageOffset = Alignment.AlignDownPow2(offset, (uint)_bufferedStorage._blockSize);
            long baseSize = _bufferedStorage._baseStorageSize;
            long remainingSize = baseSize - storageOffset;
            long cacheSize = Math.Min(blockSize, remainingSize);
            Span<byte> cacheBuffer = _memoryRange.Span.Slice(0, (int)cacheSize);

            Assert.SdkLessEqual(0, offset);
            Assert.SdkLess(offset, baseSize);

            fetchParam = new FetchParameter
            {
                Offset = storageOffset,
                Buffer = cacheBuffer
            };
        }
    }

    /// <summary>
    /// Allows iteration over the <see cref="Cache"/> in a <see cref="FsSystem.BufferedStorage"/>.
    /// Several options exist for which Caches to iterate.
    /// </summary>
    private ref struct SharedCache
    {
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public Ref<Cache> Cache { get; private set; }
        private Ref<Cache> StartCache { get; }
        public BufferedStorage BufferedStorage { get; }

        public SharedCache(BufferedStorage bufferedStorage)
        {
            Cache = default;
            StartCache = new Ref<Cache>(ref bufferedStorage.NextAcquireCache);
            BufferedStorage = bufferedStorage;

            Assert.SdkRequiresNotNull(BufferedStorage);
        }

        public void Dispose()
        {
            using var lk = new ScopedLock<SdkMutexType>(ref BufferedStorage._mutex);

            Release();
        }

        /// <summary>
        /// Moves to the next <see cref="FsSystem.BufferedStorage.Cache"/> that contains data from the specified range.
        /// </summary>
        /// <param name="offset">The start offset of the range.</param>
        /// <param name="size">The size of the range.</param>
        /// <returns><see langword="true"/> if a <see cref="FsSystem.BufferedStorage.Cache"/> from the
        /// specified range was found. <see langword="false"/> if no matching Caches exist,
        /// or if all matching Caches have already been iterated.</returns>
        public bool AcquireNextOverlappedCache(long offset, long size)
        {
            Assert.SdkRequiresNotNull(BufferedStorage);

            bool isFirst = Cache.IsNull;
            ref Cache start = ref isFirst ? ref StartCache.Value : ref Unsafe.Add(ref Cache.Value, 1);

            // Make sure the Cache instance is in-range.
            Assert.SdkAssert(!Unsafe.IsAddressLessThan(ref start,
                ref MemoryMarshal.GetArrayDataReference(BufferedStorage._caches)));

            Assert.SdkAssert(!Unsafe.IsAddressGreaterThan(ref start,
                ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(BufferedStorage._caches),
                    BufferedStorage._cacheCount)));

            using var lk = new ScopedLock<SdkMutexType>(ref BufferedStorage._mutex);

            Release();
            Assert.SdkAssert(Cache.IsNull);

            for (ref Cache cache = ref start; ; cache = ref Unsafe.Add(ref cache, 1))
            {
                // Wrap to the front of the list if we've reached the end.
                ref Cache end = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(BufferedStorage._caches),
                    BufferedStorage._cacheCount);
                if (!Unsafe.IsAddressLessThan(ref cache, ref end))
                {
                    cache = ref MemoryMarshal.GetArrayDataReference(BufferedStorage._caches);
                }

                // Break if we've iterated all the Caches
                if (!isFirst && Unsafe.AreSame(ref cache, ref StartCache.Value))
                {
                    break;
                }

                if (cache.IsValid() && cache.Hits(offset, size) && cache.TryAcquireCache())
                {
                    cache.Unlink();
                    Cache = new Ref<Cache>(ref cache);
                    return true;
                }

                isFirst = false;
            }

            Cache = default;
            return false;
        }

        /// <summary>
        /// Moves to the next dirty <see cref="FsSystem.BufferedStorage.Cache"/>.
        /// </summary>
        /// <returns><see langword="true"/> if a dirty <see cref="FsSystem.BufferedStorage.Cache"/> was found.
        /// <see langword="false"/> if no dirty Caches exist,
        /// or if all dirty Caches have already been iterated.</returns>
        public bool AcquireNextDirtyCache()
        {
            Assert.SdkRequiresNotNull(BufferedStorage);

            ref Cache start = ref Cache.IsNull
                ? ref MemoryMarshal.GetArrayDataReference(BufferedStorage._caches)
                : ref Unsafe.Add(ref Cache.Value, 1);

            ref Cache end = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(BufferedStorage._caches),
                BufferedStorage._cacheCount);

            using var lk = new ScopedLock<SdkMutexType>(ref BufferedStorage._mutex);

            // Validate the range.
            Assert.SdkAssert(!Unsafe.IsAddressLessThan(ref start,
                ref MemoryMarshal.GetArrayDataReference(BufferedStorage._caches)));

            Assert.SdkAssert(!Unsafe.IsAddressGreaterThan(ref start, ref end));

            Release();
            Assert.SdkAssert(Cache.IsNull);

            // Find the next dirty Cache
            for (ref Cache cache = ref start;
                 Unsafe.IsAddressLessThan(ref cache, ref end);
                 cache = ref Unsafe.Add(ref cache, 1))
            {
                if (cache.IsValid() && cache.IsDirty() && cache.TryAcquireCache())
                {
                    cache.Unlink();
                    Cache = new Ref<Cache>(ref cache);
                    return true;
                }
            }

            Cache = default;
            return false;
        }

        /// <summary>
        /// Moves to the next valid <see cref="FsSystem.BufferedStorage.Cache"/>.
        /// </summary>
        /// <returns><see langword="true"/> if a valid <see cref="FsSystem.BufferedStorage.Cache"/> was found.
        /// <see langword="false"/> if no valid Caches exist,
        /// or if all valid Caches have already been iterated.</returns>
        public bool AcquireNextValidCache()
        {
            Assert.SdkRequiresNotNull(BufferedStorage);

            ref Cache start = ref Cache.IsNull
                ? ref MemoryMarshal.GetArrayDataReference(BufferedStorage._caches)
                : ref Unsafe.Add(ref Cache.Value, 1);

            ref Cache end = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(BufferedStorage._caches),
                BufferedStorage._cacheCount);

            using var lk = new ScopedLock<SdkMutexType>(ref BufferedStorage._mutex);

            // Validate the range.
            Assert.SdkAssert(!Unsafe.IsAddressLessThan(ref start,
                ref MemoryMarshal.GetArrayDataReference(BufferedStorage._caches)));

            Assert.SdkAssert(!Unsafe.IsAddressGreaterThan(ref start, ref end));

            Release();
            Assert.SdkAssert(Cache.IsNull);

            // Find the next valid Cache
            for (ref Cache cache = ref start;
                Unsafe.IsAddressLessThan(ref cache, ref end);
                cache = ref Unsafe.Add(ref cache, 1))
            {
                if (cache.IsValid() && cache.TryAcquireCache())
                {
                    cache.Unlink();
                    Cache = new Ref<Cache>(ref cache);
                    return true;
                }
            }

            Cache = default;
            return false;
        }

        /// <summary>
        /// Moves to a <see cref="FsSystem.BufferedStorage.Cache"/> that can be used for
        /// fetching a new block from the base <see cref="IStorage"/>.
        /// </summary>
        /// <returns><see langword="true"/> if a <see cref="FsSystem.BufferedStorage.Cache"/> was acquired.
        /// Otherwise, <see langword="false"/>.</returns>
        public bool AcquireFetchableCache()
        {
            Assert.SdkRequiresNotNull(BufferedStorage);

            using var lk = new ScopedLock<SdkMutexType>(ref BufferedStorage._mutex);

            Release();
            Assert.SdkAssert(Cache.IsNull);

            Cache = new Ref<Cache>(ref BufferedStorage.NextFetchCache);

            if (!Cache.IsNull)
            {
                if (Cache.Value.IsValid())
                    Cache.Value.TryAcquireCache();

                Cache.Value.Unlink();
            }

            return !Cache.IsNull;
        }

        /// <summary>
        /// Reads from the current <see cref="FsSystem.BufferedStorage.Cache"/>'s buffer.
        /// The provided <paramref name="offset"/> must be inside the block of
        /// data held by the <see cref="FsSystem.BufferedStorage.Cache"/>.
        /// </summary>
        /// <param name="offset">The offset in the base <see cref="IStorage"/> to be read from.</param>
        /// <param name="buffer">The buffer in which to place the read data.</param>
        public void Read(long offset, Span<byte> buffer)
        {
            Assert.SdkRequires(!Cache.IsNull);
            Cache.Value.Read(offset, buffer);
        }

        /// <summary>
        /// Buffers data to be written to the base <see cref="IStorage"/> when the current
        /// <see cref="FsSystem.BufferedStorage.Cache"/> is flushed. The provided <paramref name="offset"/>
        /// must be contained by the block of data held by the <see cref="FsSystem.BufferedStorage.Cache"/>.
        /// </summary>
        /// <param name="offset">The offset in the base <see cref="IStorage"/> to be written to.</param>
        /// <param name="buffer">The buffer containing the data to be written.</param>
        public void Write(long offset, ReadOnlySpan<byte> buffer)
        {
            Assert.SdkRequires(!Cache.IsNull);
            Cache.Value.Write(offset, buffer);
        }

        /// <summary>
        /// If the current <see cref="FsSystem.BufferedStorage.Cache"/> is dirty,
        /// flushes its data to the base <see cref="IStorage"/>.
        /// </summary>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        public Result Flush()
        {
            Assert.SdkRequires(!Cache.IsNull);
            return Cache.Value.Flush();
        }

        /// <summary>
        /// Invalidates the data in the current <see cref="FsSystem.BufferedStorage.Cache"/>.
        /// Any dirty data will be discarded.
        /// </summary>
        public void Invalidate()
        {
            Assert.SdkRequires(!Cache.IsNull);
            Cache.Value.Invalidate();
        }

        /// <summary>
        /// Checks if the current <see cref="FsSystem.BufferedStorage.Cache"/> covers any of the specified range.
        /// </summary>
        /// <param name="offset">The start offset of the range to check.</param>
        /// <param name="size">The size of the range to check.</param>
        /// <returns><see langword="true"/> if the current <see cref="FsSystem.BufferedStorage.Cache"/>'s range
        /// covers any of the input range. Otherwise, <see langword="false"/>.</returns>
        public bool Hits(long offset, long size)
        {
            Assert.SdkRequires(!Cache.IsNull);
            return Cache.Value.Hits(offset, size);
        }

        /// <summary>
        /// Releases the current <see cref="FsSystem.BufferedStorage.Cache"/> to return to the fetch list.
        /// </summary>
        private void Release()
        {
            if (!Cache.IsNull)
            {
                // Make sure the Cache instance is in-range.
                Assert.SdkAssert(!Unsafe.IsAddressLessThan(ref Cache.Value,
                    ref MemoryMarshal.GetArrayDataReference(BufferedStorage._caches)));

                Assert.SdkAssert(!Unsafe.IsAddressGreaterThan(ref Cache.Value,
                    ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(BufferedStorage._caches),
                        BufferedStorage._cacheCount)));

                Cache.Value.Link();
                Cache = default;
            }
        }
    }

    /// <summary>
    /// Provides exclusive access to a <see cref="Cache"/>
    /// entry in a <see cref="FsSystem.BufferedStorage"/>.
    /// </summary>
    private ref struct UniqueCache
    {
        private Ref<Cache> _cache;
        private BufferedStorage _bufferedStorage;

        public UniqueCache(BufferedStorage bufferedStorage)
        {
            _cache = default;
            _bufferedStorage = bufferedStorage;
            Assert.SdkRequiresNotNull(_bufferedStorage);
        }

        /// <summary>
        /// Disposes the <see cref="UniqueCache"/>, releasing any held <see cref="FsSystem.BufferedStorage.Cache"/>.
        /// </summary>
        public void Dispose()
        {
            if (!_cache.IsNull)
            {
                using var lk = new ScopedLock<SdkMutexType>(ref _bufferedStorage._mutex);

                _cache.Value.UnprepareFetch();

            }
        }

        /// <summary>
        /// Attempts to gain exclusive access to the <see cref="FsSystem.BufferedStorage.Cache"/> held by
        /// <paramref name="sharedCache"/> and prepare it to read a new block from the base <see cref="IStorage"/>.
        /// </summary>
        /// <param name="sharedCache">The <see cref="SharedCache"/> to gain exclusive access to.</param>
        /// <returns>The <see cref="Result"/> of the operation, and <see langword="true"/> if exclusive
        /// access to the <see cref="_cache"/> was gained; <see langword="false"/> if not.</returns>
        public (Result Result, bool wasUpgradeSuccessful) Upgrade(in SharedCache sharedCache)
        {
            Assert.SdkRequires(_bufferedStorage == sharedCache.BufferedStorage);
            Assert.SdkRequires(!sharedCache.Cache.IsNull);

            using var lk = new ScopedLock<SdkMutexType>(ref _bufferedStorage._mutex);

            (Result Result, bool wasUpgradeSuccessful) result = sharedCache.Cache.Value.PrepareFetch();

            if (result.Result.IsSuccess() && result.wasUpgradeSuccessful)
                _cache = sharedCache.Cache;

            return result;
        }

        /// <summary>
        /// Reads the storage block containing the specified offset into the
        /// <see cref="FsSystem.BufferedStorage.Cache"/>'s buffer, and sets the Cache to that offset.
        /// </summary>
        /// <param name="offset">An offset in the block to fetch.</param>
        /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
        /// <see cref="ResultFs.BufferAllocationFailed"/>: A buffer could not be allocated.</returns>
        public Result Fetch(long offset)
        {
            Assert.SdkRequires(!_cache.IsNull);

            return _cache.Value.Fetch(offset);
        }

        /// <summary>
        /// Fills the <see cref="FsSystem.BufferedStorage.Cache"/>'s buffer from an input buffer containing a block of data
        /// read from the base <see cref="IStorage"/>, and sets the Cache to that offset.
        /// </summary>
        /// <param name="offset">The start offset of the block in the base <see cref="IStorage"/>
        /// that the data was read from.</param>
        /// <param name="buffer">A buffer containing the data read from the base <see cref="IStorage"/>.</param>
        /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
        /// <see cref="ResultFs.BufferAllocationFailed"/>: A buffer could not be allocated.</returns>
        public Result FetchFromBuffer(long offset, ReadOnlySpan<byte> buffer)
        {
            Assert.SdkRequires(!_cache.IsNull);

            return _cache.Value.FetchFromBuffer(offset, buffer).Miss();
        }
    }

    private ValueSubStorage _baseStorage;
    private IBufferManager _bufferManager;
    private long _blockSize;
    private long _baseStorageSize;
    private Cache[] _caches;
    private int _cacheCount;
    private int _nextAcquireCacheIndex;
    private int _nextFetchCacheIndex;
    private SdkMutexType _mutex;
    private bool _bulkReadEnabled;

    /// <summary>
    /// The <see cref="Cache"/> at which new <see cref="SharedCache"/>s will begin iterating.
    /// </summary>
    private ref Cache NextAcquireCache => ref _caches[_nextAcquireCacheIndex];

    /// <summary>
    /// A list of <see cref="Cache"/>s that can be used for fetching
    /// new blocks of data from the base <see cref="IStorage"/>.
    /// </summary>
    private ref Cache NextFetchCache => ref _caches[_nextFetchCacheIndex];

    /// <summary>
    /// Creates an uninitialized <see cref="BufferedStorage"/>.
    /// </summary>
    public BufferedStorage()
    {
        _nextAcquireCacheIndex = InvalidIndex;
        _nextFetchCacheIndex = InvalidIndex;
        _mutex = new SdkMutexType();
    }

    /// <summary>
    /// Disposes the <see cref="BufferedStorage"/>, flushing any cached data.
    /// </summary>
    public override void Dispose()
    {
        FinalizeObject();
        _baseStorage.Dispose();
        base.Dispose();
    }

    /// <summary>
    /// Initializes the <see cref="BufferedStorage"/>.
    /// Calling this method again afterwards will flush the current cache and
    /// reinitialize the <see cref="BufferedStorage"/> with the new parameters.
    /// </summary>
    /// <param name="baseStorage">The base storage to use.</param>
    /// <param name="bufferManager">The buffer manager used to allocate and cache memory.</param>
    /// <param name="blockSize">The size of each cached block. Must be a power of 2.</param>
    /// <param name="bufferCount">The maximum number of blocks that can be cached at one time.</param>
    /// <returns></returns>
    public Result Initialize(in ValueSubStorage baseStorage, IBufferManager bufferManager, int blockSize, int bufferCount)
    {
        Assert.SdkRequiresNotNull(bufferManager);
        Assert.SdkRequiresLess(0, blockSize);
        Assert.SdkRequires(BitUtil.IsPowerOfTwo(blockSize));
        Assert.SdkRequiresLess(0, bufferCount);

        // Get the base storage size.
        Result rc = baseStorage.GetSize(out _baseStorageSize);
        if (rc.IsFailure()) return rc.Miss();

        // Set members.
        _baseStorage.Set(in baseStorage);
        _bufferManager = bufferManager;
        _blockSize = blockSize;
        _cacheCount = bufferCount;

        // Allocate the caches.
        if (_caches != null)
        {
            for (int i = 0; i < _caches.Length; i++)
            {
                _caches[i].FinalizeObject();
            }
        }

        _caches = new Cache[bufferCount];
        if (_caches == null)
        {
            return ResultFs.AllocationMemoryFailedInBufferedStorageA.Log();
        }

        for (int i = 0; i < _caches.Length; i++)
        {
            _caches[i] = new Cache(i);
        }

        // Initialize the caches.
        for (int i = 0; i < _caches.Length; i++)
        {
            _caches[i].Initialize(this);
        }

        _nextFetchCacheIndex = 0;
        _nextAcquireCacheIndex = 0;
        return Result.Success;
    }

    /// <summary>
    /// Finalizes this <see cref="BufferedStorage"/>, flushing all buffers and leaving it in an uninitialized state.
    /// </summary>
    public void FinalizeObject()
    {
        using (var emptyStorage = new ValueSubStorage())
        {
            _baseStorage.Set(in emptyStorage);
        }

        _baseStorageSize = 0;

        foreach (Cache cache in _caches)
        {
            cache.Dispose();
        }

        _caches = null;
        _cacheCount = 0;
        _nextFetchCacheIndex = InvalidIndex;
    }

    /// <summary>
    /// Has this <see cref="BufferedStorage"/> been initialized?
    /// </summary>
    /// <returns><see langword="true"/> if this <see cref="BufferedStorage"/> is initialized.
    /// Otherwise, <see langword="false"/>.</returns>
    public bool IsInitialized() => _caches != null;

    public override Result Read(long offset, Span<byte> destination)
    {
        Assert.SdkRequires(IsInitialized());

        // Succeed if zero size.
        if (destination.Length == 0)
            return Result.Success;

        // Do the read.
        return ReadCore(offset, destination).Miss();
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        Assert.SdkRequires(IsInitialized());

        // Succeed if zero size.
        if (source.Length == 0)
            return Result.Success;

        // Do the read.
        return WriteCore(offset, source).Miss();
    }

    public override Result GetSize(out long size)
    {
        Assert.SdkRequires(IsInitialized());

        size = _baseStorageSize;
        return Result.Success;
    }

    public override Result SetSize(long size)
    {
        Assert.SdkRequires(IsInitialized());

        Result rc;
        long prevSize = _baseStorageSize;
        if (prevSize < size)
        {
            // Prepare to expand.
            if (!Alignment.IsAlignedPow2(prevSize, (uint)_blockSize))
            {
                using var cache = new SharedCache(this);
                long invalidateOffset = prevSize;
                long invalidateSize = size - prevSize;

                if (cache.AcquireNextOverlappedCache(invalidateOffset, invalidateSize))
                {
                    rc = cache.Flush();
                    if (rc.IsFailure()) return rc.Miss();

                    cache.Invalidate();
                }

                Assert.SdkAssert(!cache.AcquireNextOverlappedCache(invalidateOffset, invalidateSize));
            }
        }
        else if (size < prevSize)
        {
            // Prepare to shrink.
            using var cache = new SharedCache(this);
            long invalidateOffset = prevSize;
            long invalidateSize = size - prevSize;
            bool isFragment = Alignment.IsAlignedPow2(size, (uint)_blockSize);

            while (cache.AcquireNextOverlappedCache(invalidateOffset, invalidateSize))
            {
                if (isFragment && cache.Hits(invalidateOffset, 1))
                {
                    rc = cache.Flush();
                    if (rc.IsFailure()) return rc.Miss();
                }

                cache.Invalidate();
            }
        }

        // Set the size.
        rc = _baseStorage.SetSize(size);
        if (rc.IsFailure()) return rc.Miss();

        // Get our new size.
        rc = _baseStorage.GetSize(out long newSize);
        if (rc.IsFailure()) return rc.Miss();

        _baseStorageSize = newSize;
        return Result.Success;
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        Assert.SdkRequires(IsInitialized());

        // Invalidate caches if needed.
        if (operationId == OperationId.InvalidateCache)
        {
            InvalidateCaches();
        }

        return _baseStorage.OperateRange(outBuffer, operationId, offset, size, inBuffer);
    }

    public override Result Flush()
    {
        Assert.SdkRequires(IsInitialized());

        // Flush caches.
        using var cache = new SharedCache(this);
        while (cache.AcquireNextDirtyCache())
        {
            Result rc = cache.Flush();
            if (rc.IsFailure()) return rc.Miss();
        }

        // Flush the base storage.
        return _baseStorage.Flush().Miss();
    }

    /// <summary>
    /// Invalidates all cached data. Any unflushed data will be discarded.
    /// </summary>
    public void InvalidateCaches()
    {
        Assert.SdkRequires(IsInitialized());

        using var cache = new SharedCache(this);
        while (cache.AcquireNextValidCache())
            cache.Invalidate();
    }

    /// <summary>
    /// Gets the <see cref="IBufferManager"/> used by this <see cref="BufferedStorage"/>.
    /// </summary>
    /// <returns>The buffer manager.</returns>
    public IBufferManager GetBufferManager() => _bufferManager;

    public void EnableBulkRead() => _bulkReadEnabled = true;

    /// <summary>
    /// Flushes the cache to the base <see cref="IStorage"/> if less than 1/8 of the
    /// <see cref="IBufferManager"/>'s space can be used for allocation.
    /// </summary>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    private Result PrepareAllocation()
    {
        uint flushThreshold = (uint)_bufferManager.GetTotalSize() / 8;

        if (_bufferManager.GetTotalAllocatableSize() < flushThreshold)
        {
            Result rc = Flush();
            if (rc.IsFailure()) return rc.Miss();
        }

        return Result.Success;
    }

    /// <summary>
    /// Flushes all dirty caches if less than 25% of the space
    /// in the <see cref="IBufferManager"/> is allocatable.
    /// </summary>
    /// <returns></returns>
    private Result ControlDirtiness()
    {
        uint flushThreshold = (uint)_bufferManager.GetTotalSize() / 4;

        if (_bufferManager.GetTotalAllocatableSize() < flushThreshold)
        {
            using var cache = new SharedCache(this);
            int dirtyCount = 0;

            while (cache.AcquireNextDirtyCache())
            {
                if (++dirtyCount > 1)
                {
                    Result rc = cache.Flush();
                    if (rc.IsFailure()) return rc.Miss();

                    cache.Invalidate();
                }
            }
        }

        return Result.Success;
    }

    /// <summary>
    /// Reads data from the base <see cref="IStorage"/> into the destination buffer.
    /// </summary>
    /// <param name="offset">The offset in the <see cref="IStorage"/> at which to begin reading.</param>
    /// <param name="destination">The buffer where the read bytes will be stored.
    /// The number of bytes read will be equal to the length of the buffer.</param>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    private Result ReadCore(long offset, Span<byte> destination)
    {
        Assert.SdkRequiresNotNull(_caches);
        Assert.SdkRequiresNotNull(destination);

        // Validate the offset.
        long baseStorageSize = _baseStorageSize;
        if (offset < 0 || offset > baseStorageSize)
            return ResultFs.InvalidOffset.Log();

        // Setup tracking variables.
        long remainingSize = Math.Min(destination.Length, baseStorageSize - offset);
        long currentOffset = offset;
        long bufferOffset = 0;

        // Try doing a bulk read if enabled.
        //
        // The behavior of which blocks are cached should be the same between bulk reads and non-bulk reads.
        // If the head and tail offsets of the range to be read are not aligned to block boundaries, those 
        // head and/or tail partial blocks will end up in the cache if doing a non-bulk read.
        //
        // This is imitated during bulk reads by tracking if there are any partial head or tail blocks that aren't
        // already in the cache. After the bulk read is complete these partial blocks will be added to the cache.
        if (_bulkReadEnabled)
        {
            // Read any blocks at the head of the range that are cached.
            bool headCacheNeeded =
                ReadHeadCache(ref currentOffset, destination, ref remainingSize, ref bufferOffset);
            if (remainingSize == 0) return Result.Success;

            // Read any blocks at the tail of the range that are cached.
            bool tailCacheNeeded = ReadTailCache(currentOffset, destination, ref remainingSize, bufferOffset);
            if (remainingSize == 0) return Result.Success;

            // Perform bulk reads.
            const long bulkReadSizeMax = 1024 * 1024 * 2; // 2 MB

            if (remainingSize <= bulkReadSizeMax)
            {
                // Try to do a bulk read.
                Result rc = BulkRead(currentOffset, destination.Slice((int)bufferOffset, (int)remainingSize),
                    headCacheNeeded, tailCacheNeeded);

                // If the read fails due to insufficient pooled buffer size
                // then we want to fall back to the normal read path.
                if (!ResultFs.AllocationPooledBufferNotEnoughSize.Includes(rc))
                    return rc.Miss();
            }
        }

        // Repeatedly read until we're done.
        while (remainingSize > 0)
        {
            // Determine how much to read this iteration.
            int currentSize;

            // If the offset is in the middle of a block, read the remaining part of that block.
            if (!Alignment.IsAlignedPow2(currentOffset, (uint)_blockSize))
            {
                long alignedSize = _blockSize - (currentOffset & (_blockSize - 1));
                currentSize = (int)Math.Min(alignedSize, remainingSize);
            }
            // If we only have a partial block left to read, read that partial block.
            else if (remainingSize < _blockSize)
            {
                currentSize = (int)remainingSize;
            }
            // We have at least one full block to read. Read all the remaining full blocks at once.
            else
            {
                currentSize = (int)Alignment.AlignDownPow2(remainingSize, (uint)_blockSize);
            }

            Span<byte> currentDestination = destination.Slice((int)bufferOffset, currentSize);

            // If reading a single block or less, read it using the cache
            if (currentSize <= _blockSize)
            {
                using var cache = new SharedCache(this);

                // Get the cache for our current block
                if (!cache.AcquireNextOverlappedCache(currentOffset, currentSize))
                {
                    // The block wasn't in the cache. Read the block from the base storage
                    Result rc = PrepareAllocation();
                    if (rc.IsFailure()) return rc.Miss();

                    // Loop until we can get exclusive access to the cache block
                    while (true)
                    {
                        if (!cache.AcquireFetchableCache())
                            return ResultFs.OutOfResource.Log();

                        // Try to upgrade out SharedCache to a UniqueCache
                        using var fetchCache = new UniqueCache(this);
                        (Result Result, bool wasUpgradeSuccessful) upgradeResult = fetchCache.Upgrade(in cache);
                        if (upgradeResult.Result.IsFailure())
                            return upgradeResult.Result.Miss();

                        // Fetch the data from the base storage into the cache buffer if successful
                        if (upgradeResult.wasUpgradeSuccessful)
                        {
                            rc = fetchCache.Fetch(currentOffset);
                            if (rc.IsFailure()) return rc.Miss();

                            break;
                        }
                    }

                    rc = ControlDirtiness();
                    if (rc.IsFailure()) return rc.Miss();
                }

                // Copy the data from the cache buffer to the destination buffer
                cache.Read(currentOffset, currentDestination);
            }
            // If reading multiple blocks, flush the cache entries for all those blocks and
            // read directly from the base storage into the destination buffer in a single read.
            else
            {
                // Flush all the cache blocks in the storage range being read
                using (var cache = new SharedCache(this))
                {
                    while (cache.AcquireNextOverlappedCache(currentOffset, currentSize))
                    {
                        Result rc = cache.Flush();
                        if (rc.IsFailure()) return rc.Miss();

                        cache.Invalidate();
                    }
                }

                // Read directly from the base storage to the destination buffer
                Result rcRead = _baseStorage.Read(currentOffset, currentDestination);
                if (rcRead.IsFailure()) return rcRead.Miss();
            }

            remainingSize -= currentSize;
            currentOffset += currentSize;
            bufferOffset += currentSize;
        }

        return Result.Success;
    }

    /// <summary>
    /// Reads as much data into the beginning of the buffer that can be found in the cache. Returns
    /// <see langword="true"/> if the next uncached data to read from the base <see cref="IStorage"/>
    /// is not aligned to the beginning of a block.
    /// </summary>
    /// <param name="offset">The storage offset at which to begin reading. When this function returns, contains
    /// the new offset at which to begin reading if any data was read by this function.</param>
    /// <param name="buffer">The buffer to read data into.</param>
    /// <param name="size">The size of the data to read. When this function returns, contains the new size
    /// if any data was read by this function.</param>
    /// <param name="bufferOffset">The offset of the buffer to begin writing data to. When this function returns,
    /// contains the new offset to write data to if any data was read by this function.</param>
    /// <returns><see langword="true"/> if the remaining data to read contains a partial block at the start.
    /// Otherwise, <see langword="false"/>.</returns>
    private bool ReadHeadCache(ref long offset, Span<byte> buffer, ref long size, ref long bufferOffset)
    {
        bool isCacheNeeded = !Alignment.IsAlignedPow2(offset, (uint)_blockSize);

        while (size > 0)
        {
            long currentSize;

            if (!Alignment.IsAlignedPow2(offset, (uint)_blockSize))
            {
                long alignedSize = Alignment.AlignUpPow2(offset, (uint)_blockSize) - offset;
                currentSize = Math.Min(alignedSize, size);
            }
            else if (size < _blockSize)
            {
                currentSize = size;
            }
            else
            {
                currentSize = _blockSize;
            }

            using var cache = new SharedCache(this);

            if (!cache.AcquireNextOverlappedCache(offset, currentSize))
                break;

            cache.Read(offset, buffer.Slice((int)bufferOffset, (int)currentSize));
            offset += currentSize;
            bufferOffset += currentSize;
            size -= currentSize;
            isCacheNeeded = false;
        }

        return isCacheNeeded;
    }

    private bool ReadTailCache(long offset, Span<byte> buffer, ref long size, long bufferOffset)
    {
        bool isCacheNeeded = !Alignment.IsAlignedPow2(offset + size, (uint)_blockSize);

        while (size > 0)
        {
            long currentOffsetEnd = offset + size;
            long currentSize;

            if (!Alignment.IsAlignedPow2(currentOffsetEnd, (uint)_blockSize))
            {
                long alignedSize = currentOffsetEnd - Alignment.AlignDownPow2(currentOffsetEnd, (uint)_blockSize);
                currentSize = Math.Min(alignedSize, size);
            }
            else if (size < _blockSize)
            {
                currentSize = size;
            }
            else
            {
                currentSize = _blockSize;
            }

            long currentOffset = currentOffsetEnd - currentSize;
            Assert.SdkGreaterEqual(currentOffset, 0);

            using var cache = new SharedCache(this);

            if (!cache.AcquireNextOverlappedCache(currentOffset, currentSize))
                break;

            int currentBufferOffset = (int)(bufferOffset + currentOffset - offset);
            cache.Read(currentOffset, buffer.Slice(currentBufferOffset, (int)currentSize));
            size -= currentSize;
            isCacheNeeded = false;
        }

        return isCacheNeeded;
    }

    /// <summary>
    /// Reads directly from the base <see cref="IStorage"/> to the destination
    /// <paramref name="buffer"/> using a single read.
    /// </summary>
    /// <param name="offset">The offset at which to begin reading</param>
    /// <param name="buffer">The buffer where the read bytes will be stored.
    /// The number of bytes read will be equal to the length of the buffer.</param>
    /// <param name="isHeadCacheNeeded">Should the head block of the read data be cached?</param>
    /// <param name="isTailCacheNeeded">Should the tail block of the read data be cached?</param>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    private Result BulkRead(long offset, Span<byte> buffer, bool isHeadCacheNeeded, bool isTailCacheNeeded)
    {
        Result rc;

        // Determine aligned extents.
        long alignedOffset = Alignment.AlignDownPow2(offset, (uint)_blockSize);
        long alignedOffsetEnd = Math.Min(Alignment.AlignUpPow2(offset + buffer.Length, (uint)_blockSize),
            _baseStorageSize);
        long alignedSize = alignedOffsetEnd - alignedOffset;

        // Allocate a work buffer if either the head or tail of the range isn't aligned.
        // Otherwise directly use the output buffer.
        Span<byte> workBuffer;
        using var pooledBuffer = new PooledBuffer();

        if (offset == alignedOffset && buffer.Length == alignedSize)
        {
            workBuffer = buffer;
        }
        else
        {
            pooledBuffer.AllocateParticularlyLarge((int)alignedSize, 1);
            if (pooledBuffer.GetSize() < alignedSize)
                return ResultFs.AllocationPooledBufferNotEnoughSize.Log();

            workBuffer = pooledBuffer.GetBuffer();
        }

        // Ensure cache is coherent.
        using (var cache = new SharedCache(this))
        {
            while (cache.AcquireNextOverlappedCache(alignedOffset, alignedSize))
            {
                rc = cache.Flush();
                if (rc.IsFailure()) return rc.Miss();

                cache.Invalidate();
            }
        }

        // Read from the base storage.
        rc = _baseStorage.Read(alignedOffset, workBuffer.Slice(0, (int)alignedSize));
        if (rc.IsFailure()) return rc.Miss();
        if (workBuffer != buffer)
        {
            workBuffer.Slice((int)(offset - alignedOffset), buffer.Length).CopyTo(buffer);
        }

        bool cached = false;

        // Cache the head block if needed.
        if (isHeadCacheNeeded)
        {
            rc = PrepareAllocation();
            if (rc.IsFailure()) return rc.Miss();

            using var cache = new SharedCache(this);
            while (true)
            {
                if (!cache.AcquireFetchableCache())
                    return ResultFs.OutOfResource.Log();

                using var fetchCache = new UniqueCache(this);
                (Result Result, bool wasUpgradeSuccessful) upgradeResult = fetchCache.Upgrade(in cache);
                if (upgradeResult.Result.IsFailure())
                    return upgradeResult.Result.Miss();

                if (upgradeResult.wasUpgradeSuccessful)
                {
                    rc = fetchCache.FetchFromBuffer(alignedOffset, workBuffer.Slice(0, (int)alignedSize));
                    if (rc.IsFailure()) return rc.Miss();
                    break;
                }
            }

            cached = true;
        }

        // Cache the tail block if needed.
        if (isTailCacheNeeded && (!isHeadCacheNeeded || alignedSize > _blockSize))
        {
            if (!cached)
            {
                rc = PrepareAllocation();
                if (rc.IsFailure()) return rc.Miss();
            }

            using var cache = new SharedCache(this);
            while (true)
            {
                if (!cache.AcquireFetchableCache())
                    return ResultFs.OutOfResource.Log();

                using var fetchCache = new UniqueCache(this);
                (Result Result, bool wasUpgradeSuccessful) upgradeResult = fetchCache.Upgrade(in cache);
                if (upgradeResult.Result.IsFailure())
                    return upgradeResult.Result.Miss();

                if (upgradeResult.wasUpgradeSuccessful)
                {
                    long tailCacheOffset = Alignment.AlignDownPow2(offset + buffer.Length, (uint)_blockSize);
                    long tailCacheSize = alignedSize - tailCacheOffset + alignedOffset;

                    rc = fetchCache.FetchFromBuffer(tailCacheOffset,
                        workBuffer.Slice((int)(tailCacheOffset - alignedOffset), (int)tailCacheSize));
                    if (rc.IsFailure()) return rc.Miss();
                    break;
                }
            }
        }

        if (cached)
        {
            rc = ControlDirtiness();
            if (rc.IsFailure()) return rc.Miss();
        }

        return Result.Success;
    }

    private Result WriteCore(long offset, ReadOnlySpan<byte> source)
    {
        Assert.SdkRequiresNotNull(_caches);
        Assert.SdkRequiresNotNull(source);

        // Validate the offset.
        long baseStorageSize = _baseStorageSize;

        if (offset < 0 || baseStorageSize < offset)
            return ResultFs.InvalidOffset.Log();

        // Setup tracking variables.
        int remainingSize = (int)Math.Min(source.Length, baseStorageSize - offset);
        long currentOffset = offset;
        int bufferOffset = 0;

        // Repeatedly read until we're done.
        while (remainingSize > 0)
        {
            // Determine how much to read this iteration.
            ReadOnlySpan<byte> currentSource = source.Slice(bufferOffset);
            int currentSize;

            if (!Alignment.IsAlignedPow2(currentOffset, (uint)_blockSize))
            {
                int alignedSize = (int)(_blockSize - (currentOffset & (_blockSize - 1)));
                currentSize = Math.Min(alignedSize, remainingSize);
            }
            else if (remainingSize < _blockSize)
            {
                currentSize = remainingSize;
            }
            else
            {
                currentSize = Alignment.AlignDownPow2(remainingSize, (uint)_blockSize);
            }

            Result rc;
            if (currentSize <= _blockSize)
            {
                using var cache = new SharedCache(this);

                if (!cache.AcquireNextOverlappedCache(currentOffset, currentSize))
                {
                    rc = PrepareAllocation();
                    if (rc.IsFailure()) return rc.Miss();

                    while (true)
                    {
                        if (!cache.AcquireFetchableCache())
                            return ResultFs.OutOfResource.Log();

                        using var fetchCache = new UniqueCache(this);
                        (Result Result, bool wasUpgradeSuccessful) upgradeResult = fetchCache.Upgrade(in cache);
                        if (upgradeResult.Result.IsFailure())
                            return upgradeResult.Result.Miss();

                        if (upgradeResult.wasUpgradeSuccessful)
                        {
                            rc = fetchCache.Fetch(currentOffset);
                            if (rc.IsFailure()) return rc.Miss();
                            break;
                        }
                    }
                }
                cache.Write(currentOffset, currentSource.Slice(0, currentSize));

                BufferManagerUtility.EnableBlockingBufferManagerAllocation();

                rc = ControlDirtiness();
                if (rc.IsFailure()) return rc.Miss();
            }
            else
            {
                using (var cache = new SharedCache(this))
                {
                    while (cache.AcquireNextOverlappedCache(currentOffset, currentSize))
                    {
                        rc = cache.Flush();
                        if (rc.IsFailure()) return rc.Miss();

                        cache.Invalidate();
                    }
                }

                rc = _baseStorage.Write(currentOffset, currentSource.Slice(0, currentSize));
                if (rc.IsFailure()) return rc.Miss();

                BufferManagerUtility.EnableBlockingBufferManagerAllocation();
            }

            remainingSize -= currentSize;
            currentOffset += currentSize;
            bufferOffset += currentSize;
        }

        return Result.Success;
    }
}