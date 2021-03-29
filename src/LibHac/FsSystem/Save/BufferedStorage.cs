using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSystem.Buffers;
using LibHac.Util;
using Buffer = LibHac.Fs.Buffer;
using CacheHandle = System.Int64;

namespace LibHac.FsSystem.Save
{
    /// <summary>
    /// An <see cref="IStorage"/> that provides buffered access to a base <see cref="IStorage"/>.
    /// </summary>
    public class BufferedStorage : IStorage
    {
        private const long InvalidOffset = long.MaxValue;
        private const int InvalidIndex = -1;

        /// <summary>
        /// Caches a single block of data for a <see cref="Save.BufferedStorage"/>
        /// </summary>
        private struct Cache : IDisposable
        {
            private ref struct FetchParameter
            {
                public long Offset;
                public Span<byte> Buffer;
            }

            private BufferedStorage BufferedStorage { get; set; }
            private Buffer MemoryRange { get; set; }
            private CacheHandle CacheHandle { get; set; }
            private long Offset { get; set; }
            private bool _isValid;
            private bool _isDirty;
            private int ReferenceCount { get; set; }
            private int Index { get; set; }
            private int NextIndex { get; set; }
            private int PrevIndex { get; set; }

            private ref Cache Next => ref BufferedStorage.Caches[NextIndex];
            private ref Cache Prev => ref BufferedStorage.Caches[PrevIndex];

            public void Dispose()
            {
                FinalizeObject();
            }

            public void Initialize(BufferedStorage bufferedStorage, int index)
            {
                // Note: C# can't have default constructors on structs, so the default constructor code was
                // moved into Initialize since Initialize is always called right after the constructor.
                Offset = InvalidOffset;
                ReferenceCount = 1;
                Index = index;
                NextIndex = InvalidIndex;
                PrevIndex = InvalidIndex;
                // End default constructor code

                Assert.SdkRequiresNotNull(bufferedStorage);
                Assert.SdkRequires(BufferedStorage == null);

                BufferedStorage = bufferedStorage;
                Link();
            }

            public void FinalizeObject()
            {
                Assert.SdkRequiresNotNull(BufferedStorage);
                Assert.SdkRequiresNotNull(BufferedStorage.BufferManager);
                Assert.SdkRequiresEqual(0, ReferenceCount);

                // If we're valid, acquire our cache handle and free our buffer.
                if (IsValid())
                {
                    IBufferManager bufferManager = BufferedStorage.BufferManager;
                    if (!_isDirty)
                    {
                        Assert.SdkAssert(MemoryRange.IsNull);
                        MemoryRange = bufferManager.AcquireCache(CacheHandle);
                    }

                    if (!MemoryRange.IsNull)
                    {
                        bufferManager.DeallocateBuffer(MemoryRange);
                        MemoryRange = Buffer.Empty;
                    }
                }

                // Clear all our members.
                BufferedStorage = null;
                Offset = InvalidOffset;
                _isValid = false;
                _isDirty = false;
                NextIndex = InvalidIndex;
                PrevIndex = InvalidIndex;
            }

            /// <summary>
            /// Decrements the ref-count and adds the <see cref="Cache"/> to its <see cref="Save.BufferedStorage"/>'s
            /// fetch list if the <see cref="Cache"/> has no more references, and registering the buffer with
            /// the <see cref="IBufferManager"/> if not dirty.
            /// </summary>
            public void Link()
            {
                Assert.SdkRequiresNotNull(BufferedStorage);
                Assert.SdkRequiresNotNull(BufferedStorage.BufferManager);
                Assert.SdkRequiresLess(0, ReferenceCount);

                ReferenceCount--;
                if (ReferenceCount == 0)
                {
                    Assert.SdkAssert(NextIndex == InvalidIndex);
                    Assert.SdkAssert(PrevIndex == InvalidIndex);

                    // If the fetch list is empty we can simply add it as the only cache in the list.
                    if (BufferedStorage.NextFetchCacheIndex == InvalidIndex)
                    {
                        BufferedStorage.NextFetchCacheIndex = Index;
                        NextIndex = Index;
                        PrevIndex = Index;
                    }
                    else
                    {
                        // Check against a cache being registered twice.
                        ref Cache cache = ref BufferedStorage.NextFetchCache;
                        do
                        {
                            if (cache.IsValid() && Hits(cache.Offset, BufferedStorage.BlockSize))
                            {
                                _isValid = false;
                                break;
                            }

                            cache = ref cache.Next;
                        } while (cache.Index != BufferedStorage.NextFetchCacheIndex);

                        // Verify the end of the fetch list loops back to the start.
                        Assert.SdkAssert(BufferedStorage.NextFetchCache.PrevIndex != InvalidIndex);
                        Assert.SdkEqual(BufferedStorage.NextFetchCache.Prev.NextIndex,
                            BufferedStorage.NextFetchCacheIndex);

                        // Link into the fetch list.
                        NextIndex = BufferedStorage.NextFetchCacheIndex;
                        PrevIndex = BufferedStorage.NextFetchCache.PrevIndex;
                        Next.PrevIndex = Index;
                        Prev.NextIndex = Index;

                        // Insert invalid caches at the start of the list so they'll
                        // be used first when a fetch cache is needed.
                        if (!IsValid())
                            BufferedStorage.NextFetchCacheIndex = Index;
                    }

                    // If we're not valid, clear our offset.
                    if (!IsValid())
                    {
                        Offset = InvalidOffset;
                        _isDirty = false;
                    }

                    // Ensure our buffer state is coherent.
                    // We can let go of our buffer if it's not dirty, allowing the buffer to be used elsewhere if needed.
                    if (!MemoryRange.IsNull && !IsDirty())
                    {
                        // If we're valid, register the buffer with the buffer manager for possible later retrieval.
                        // Otherwise the the data in the buffer isn't needed, so deallocate it.
                        if (IsValid())
                        {
                            CacheHandle = BufferedStorage.BufferManager.RegisterCache(MemoryRange,
                                new IBufferManager.BufferAttribute());
                        }
                        else
                        {
                            BufferedStorage.BufferManager.DeallocateBuffer(MemoryRange);
                        }

                        MemoryRange = default;
                    }
                }
            }

            /// <summary>
            /// Increments the ref-count and removes the <see cref="Cache"/> from its
            /// <see cref="Save.BufferedStorage"/>'s fetch list if needed.
            /// </summary>
            public void Unlink()
            {
                Assert.SdkRequiresNotNull(BufferedStorage);
                Assert.SdkRequiresGreaterEqual(ReferenceCount, 0);

                ReferenceCount++;
                if (ReferenceCount == 1)
                {
                    // If we're the first to grab this Cache, the Cache should be in the BufferedStorage's fetch list.
                    Assert.SdkNotEqual(NextIndex, InvalidIndex);
                    Assert.SdkNotEqual(PrevIndex, InvalidIndex);
                    Assert.SdkEqual(Next.PrevIndex, Index);
                    Assert.SdkEqual(Prev.NextIndex, Index);

                    // Set the new fetch list head if this Cache is the current head
                    if (BufferedStorage.NextFetchCacheIndex == Index)
                    {
                        if (NextIndex != Index)
                        {
                            BufferedStorage.NextFetchCacheIndex = NextIndex;
                        }
                        else
                        {
                            BufferedStorage.NextFetchCacheIndex = InvalidIndex;
                        }
                    }

                    BufferedStorage.NextAcquireCacheIndex = Index;

                    Next.PrevIndex = PrevIndex;
                    Prev.NextIndex = NextIndex;
                    NextIndex = InvalidIndex;
                    PrevIndex = InvalidIndex;
                }
                else
                {
                    Assert.SdkEqual(NextIndex, InvalidIndex);
                    Assert.SdkEqual(PrevIndex, InvalidIndex);
                }
            }

            /// <summary>
            /// Reads the data from the base <see cref="IStorage"/> contained in this <see cref="Cache"/>'s buffer.
            /// The <see cref="Cache"/> must contain valid data before calling, and the <paramref name="offset"/>
            /// must be inside the block of data held by this <see cref="Cache"/>.
            /// </summary>
            /// <param name="offset">The offset in the base <see cref="IStorage"/> to be read from.</param>
            /// <param name="buffer">The buffer in which to place the read data.</param>
            public void Read(long offset, Span<byte> buffer)
            {
                Assert.SdkRequiresNotNull(BufferedStorage);
                Assert.SdkRequiresEqual(NextIndex, InvalidIndex);
                Assert.SdkRequiresEqual(PrevIndex, InvalidIndex);
                Assert.SdkRequires(IsValid());
                Assert.SdkRequires(Hits(offset, 1));
                Assert.SdkRequires(!MemoryRange.IsNull);

                long readOffset = offset - Offset;
                long readableOffsetMax = BufferedStorage.BlockSize - buffer.Length;

                Assert.SdkLessEqual(0, readOffset);
                Assert.SdkLessEqual(readOffset, readableOffsetMax);

                Span<byte> cacheBuffer = MemoryRange.Span.Slice((int)readOffset, buffer.Length);
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
                Assert.SdkRequiresNotNull(BufferedStorage);
                Assert.SdkRequiresEqual(NextIndex, InvalidIndex);
                Assert.SdkRequiresEqual(PrevIndex, InvalidIndex);
                Assert.SdkRequires(IsValid());
                Assert.SdkRequires(Hits(offset, 1));
                Assert.SdkRequires(!MemoryRange.IsNull);

                long writeOffset = offset - Offset;
                long writableOffsetMax = BufferedStorage.BlockSize - buffer.Length;

                Assert.SdkLessEqual(0, writeOffset);
                Assert.SdkLessEqual(writeOffset, writableOffsetMax);

                Span<byte> cacheBuffer = MemoryRange.Span.Slice((int)writeOffset, buffer.Length);
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
                Assert.SdkRequiresNotNull(BufferedStorage);
                Assert.SdkRequiresEqual(NextIndex, InvalidIndex);
                Assert.SdkRequiresEqual(PrevIndex, InvalidIndex);
                Assert.SdkRequires(IsValid());

                if (_isDirty)
                {
                    Assert.SdkRequires(!MemoryRange.IsNull);

                    long baseSize = BufferedStorage.BaseStorageSize;
                    long blockSize = BufferedStorage.BlockSize;
                    long flushSize = Math.Min(blockSize, baseSize - Offset);

                    SubStorage baseStorage = BufferedStorage.BaseStorage;
                    Span<byte> cacheBuffer = MemoryRange.Span;
                    Assert.SdkEqual(flushSize, cacheBuffer.Length);

                    Result rc = baseStorage.Write(Offset, cacheBuffer);
                    if (rc.IsFailure()) return rc;

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
                Assert.SdkRequiresNotNull(BufferedStorage);
                Assert.SdkRequiresNotNull(BufferedStorage.BufferManager);
                Assert.SdkRequiresEqual(NextIndex, InvalidIndex);
                Assert.SdkRequiresEqual(PrevIndex, InvalidIndex);
                Assert.SdkRequires(IsValid());
                Assert.SdkRequires(Monitor.IsEntered(BufferedStorage.Locker));

                (Result Result, bool IsPrepared) result = (Result.Success, false);

                if (ReferenceCount == 1)
                {
                    result.Result = Flush();

                    if (result.Result.IsSuccess())
                    {
                        _isValid = false;
                        ReferenceCount = 0;
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
                Assert.SdkRequiresNotNull(BufferedStorage);
                Assert.SdkRequiresNotNull(BufferedStorage.BufferManager);
                Assert.SdkRequiresEqual(NextIndex, InvalidIndex);
                Assert.SdkRequiresEqual(PrevIndex, InvalidIndex);
                Assert.SdkRequires(!IsValid());
                Assert.SdkRequires(!_isDirty);
                Assert.SdkRequires(Monitor.IsEntered(BufferedStorage.Locker));

                _isValid = true;
                ReferenceCount = 1;
            }

            /// <summary>
            /// Reads the storage block containing the specified offset into this <see cref="Cache"/>'s buffer.
            /// </summary>
            /// <param name="offset">An offset in the block to fetch.</param>
            /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
            /// <see cref="ResultFs.BufferAllocationFailed"/>: A buffer could not be allocated.</returns>
            public Result Fetch(long offset)
            {
                Assert.SdkRequiresNotNull(BufferedStorage);
                Assert.SdkRequiresNotNull(BufferedStorage.BufferManager);
                Assert.SdkRequiresEqual(NextIndex, InvalidIndex);
                Assert.SdkRequiresEqual(PrevIndex, InvalidIndex);
                Assert.SdkRequires(!IsValid());
                Assert.SdkRequires(!_isDirty);

                Result rc;

                // Make sure this Cache has an allocated buffer
                if (MemoryRange.IsNull)
                {
                    rc = AllocateFetchBuffer();
                    if (rc.IsFailure()) return rc;
                }

                CalcFetchParameter(out FetchParameter fetchParam, offset);

                rc = BufferedStorage.BaseStorage.Read(fetchParam.Offset, fetchParam.Buffer);
                if (rc.IsFailure()) return rc;

                Offset = fetchParam.Offset;
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
                Assert.SdkRequiresNotNull(BufferedStorage);
                Assert.SdkRequiresNotNull(BufferedStorage.BufferManager);
                Assert.SdkRequiresEqual(NextIndex, InvalidIndex);
                Assert.SdkRequiresEqual(PrevIndex, InvalidIndex);
                Assert.SdkRequires(!IsValid());
                Assert.SdkRequires(!_isDirty);
                Assert.SdkRequiresAligned((ulong)offset, (int)BufferedStorage.BlockSize);

                // Make sure this Cache has an allocated buffer
                if (MemoryRange.IsNull)
                {
                    Result rc = AllocateFetchBuffer();
                    if (rc.IsFailure()) return rc;
                }

                CalcFetchParameter(out FetchParameter fetchParam, offset);
                Assert.SdkEqual(fetchParam.Offset, offset);
                Assert.SdkLessEqual(fetchParam.Buffer.Length, buffer.Length);

                buffer.Slice(0, fetchParam.Buffer.Length).CopyTo(fetchParam.Buffer);
                Offset = fetchParam.Offset;
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
                Assert.SdkRequiresNotNull(BufferedStorage);
                Assert.SdkRequiresNotNull(BufferedStorage.BufferManager);
                Assert.SdkRequires(IsValid());

                if (!MemoryRange.IsNull)
                    return true;

                MemoryRange = BufferedStorage.BufferManager.AcquireCache(CacheHandle);
                _isValid = !MemoryRange.IsNull;
                return _isValid;
            }

            /// <summary>
            /// Invalidates the data in this <see cref="Cache"/>.
            /// </summary>
            public void Invalidate()
            {
                Assert.SdkRequiresNotNull(BufferedStorage);
                _isValid = false;
            }

            /// <summary>
            /// Does this <see cref="Cache"/> have a valid buffer or are there any references to this Cache?
            /// </summary>
            /// <returns><see langword="true"/> if this <see cref="Cache"/> has a valid buffer
            /// or if anybody currently has a reference to this Cache. Otherwise, <see langword="false"/>.</returns>
            public bool IsValid()
            {
                Assert.SdkRequiresNotNull(BufferedStorage);

                return _isValid || ReferenceCount > 0;
            }

            /// <summary>
            /// Does this <see cref="Cache"/> have modified data that needs
            /// to be flushed to the base <see cref="IStorage"/>?
            /// </summary>
            /// <returns><see langword="true"/> if this <see cref="Cache"/> has unflushed data.
            /// Otherwise, <see langword="false"/>.</returns>
            public bool IsDirty()
            {
                Assert.SdkRequiresNotNull(BufferedStorage);

                return _isDirty;
            }

            /// <summary>
            /// Checks if the <see cref="Cache"/> covers any of the specified range.
            /// </summary>
            /// <param name="offset">The start offset of the range to check.</param>
            /// <param name="size">The size of the range to check.</param>
            /// <returns><see langword="true"/> if this <see cref="Cache"/>'s range covers any of the input range.
            /// Otherwise, <see langword="false"/>.</returns>
            public bool Hits(long offset, long size)
            {
                Assert.SdkRequiresNotNull(BufferedStorage);

                long blockSize = BufferedStorage.BlockSize;
                return (offset < Offset + blockSize) && (Offset < offset + size);
            }

            /// <summary>
            /// Allocates a buffer for this <see cref="Cache"/>.
            /// Should only be called when there is not already an allocated buffer.
            /// </summary>
            /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
            /// <see cref="ResultFs.BufferAllocationFailed"/>: A buffer could not be allocated.</returns>
            private Result AllocateFetchBuffer()
            {
                IBufferManager bufferManager = BufferedStorage.BufferManager;
                Assert.SdkAssert(bufferManager.AcquireCache(CacheHandle).IsNull);

                Result rc = BufferManagerUtility.AllocateBufferUsingBufferManagerContext(out Buffer bufferTemp,
                    bufferManager, (int)BufferedStorage.BlockSize, new IBufferManager.BufferAttribute(),
                    static (in Buffer buffer) => !buffer.IsNull);

                // Clear the current MemoryRange if allocation failed.
                MemoryRange = rc.IsSuccess() ? bufferTemp : default;
                return Result.Success;
            }

            /// <summary>
            /// Calculates the parameters used to fetch the block containing the
            /// specified offset in the base <see cref="IStorage"/>.
            /// </summary>
            /// <param name="fetchParam">When this function returns, contains
            /// the parameters that can be used to fetch the block.</param>
            /// <param name="offset">The offset to be fetched.</param>
            private void CalcFetchParameter(out FetchParameter fetchParam, long offset)
            {
                long blockSize = BufferedStorage.BlockSize;
                long storageOffset = Alignment.AlignDownPow2(offset, (uint)BufferedStorage.BlockSize);
                long baseSize = BufferedStorage.BaseStorageSize;
                long remainingSize = baseSize - storageOffset;
                long cacheSize = Math.Min(blockSize, remainingSize);
                Span<byte> cacheBuffer = MemoryRange.Span.Slice(0, (int)cacheSize);

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
        /// Allows iteration over the <see cref="Save.BufferedStorage.Cache"/> in a <see cref="Save.BufferedStorage"/>.
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
                Assert.SdkRequiresNotNull(bufferedStorage);
                Cache = default;
                StartCache = new Ref<Cache>(ref bufferedStorage.NextAcquireCache);
                BufferedStorage = bufferedStorage;
            }

            public void Dispose()
            {
                lock (BufferedStorage.Locker)
                {
                    Release();
                }
            }

            /// <summary>
            /// Moves to the next <see cref="Save.BufferedStorage.Cache"/> that contains data from the specified range.
            /// </summary>
            /// <param name="offset">The start offset of the range.</param>
            /// <param name="size">The size of the range.</param>
            /// <returns><see langword="true"/> if a <see cref="Save.BufferedStorage.Cache"/> from the
            /// specified range was found. <see langword="false"/> if no matching Caches exist,
            /// or if all matching Caches have already been iterated.</returns>
            public bool AcquireNextOverlappedCache(long offset, long size)
            {
                Assert.SdkRequiresNotNull(BufferedStorage);

                bool isFirst = Cache.IsNull;
                ref Cache start = ref isFirst ? ref StartCache.Value : ref Unsafe.Add(ref Cache.Value, 1);

                // Make sure the Cache instance is in-range.
                Assert.SdkAssert(!Unsafe.IsAddressLessThan(ref start,
                    ref MemoryMarshal.GetArrayDataReference(BufferedStorage.Caches)));

                Assert.SdkAssert(!Unsafe.IsAddressGreaterThan(ref start,
                    ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(BufferedStorage.Caches),
                        BufferedStorage.CacheCount)));

                lock (BufferedStorage.Locker)
                {
                    Release();
                    Assert.SdkAssert(Cache.IsNull);

                    for (ref Cache cache = ref start; ; cache = ref Unsafe.Add(ref cache, 1))
                    {
                        // Wrap to the front of the list if we've reached the end.
                        ref Cache end = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(BufferedStorage.Caches),
                            BufferedStorage.CacheCount);
                        if (!Unsafe.IsAddressLessThan(ref cache, ref end))
                        {
                            cache = ref MemoryMarshal.GetArrayDataReference(BufferedStorage.Caches);
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
            }

            /// <summary>
            /// Moves to the next dirty <see cref="Save.BufferedStorage.Cache"/>.
            /// </summary>
            /// <returns><see langword="true"/> if a dirty <see cref="Save.BufferedStorage.Cache"/> was found.
            /// <see langword="false"/> if no dirty Caches exist,
            /// or if all dirty Caches have already been iterated.</returns>
            public bool AcquireNextDirtyCache()
            {
                Assert.SdkRequiresNotNull(BufferedStorage);

                ref Cache start = ref Cache.IsNull
                    ? ref MemoryMarshal.GetArrayDataReference(BufferedStorage.Caches)
                    : ref Unsafe.Add(ref Cache.Value, 1);

                ref Cache end = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(BufferedStorage.Caches),
                    BufferedStorage.CacheCount);

                // Validate the range.
                Assert.SdkAssert(!Unsafe.IsAddressLessThan(ref start,
                    ref MemoryMarshal.GetArrayDataReference(BufferedStorage.Caches)));

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
            /// Moves to the next valid <see cref="Save.BufferedStorage.Cache"/>.
            /// </summary>
            /// <returns><see langword="true"/> if a valid <see cref="Save.BufferedStorage.Cache"/> was found.
            /// <see langword="false"/> if no valid Caches exist,
            /// or if all valid Caches have already been iterated.</returns>
            public bool AcquireNextValidCache()
            {
                Assert.SdkRequiresNotNull(BufferedStorage);

                ref Cache start = ref Cache.IsNull
                    ? ref MemoryMarshal.GetArrayDataReference(BufferedStorage.Caches)
                    : ref Unsafe.Add(ref Cache.Value, 1);

                ref Cache end = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(BufferedStorage.Caches),
                    BufferedStorage.CacheCount);

                // Validate the range.
                Assert.SdkAssert(!Unsafe.IsAddressLessThan(ref start,
                    ref MemoryMarshal.GetArrayDataReference(BufferedStorage.Caches)));

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
            /// Moves to a <see cref="Save.BufferedStorage.Cache"/> that can be used for
            /// fetching a new block from the base <see cref="IStorage"/>.
            /// </summary>
            /// <returns><see langword="true"/> if a <see cref="Save.BufferedStorage.Cache"/> was acquired.
            /// Otherwise, <see langword="false"/>.</returns>
            public bool AcquireFetchableCache()
            {
                Assert.SdkRequiresNotNull(BufferedStorage);

                lock (BufferedStorage.Locker)
                {
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
            }

            /// <summary>
            /// Reads from the current <see cref="Save.BufferedStorage.Cache"/>'s buffer.
            /// The provided <paramref name="offset"/> must be inside the block of
            /// data held by the <see cref="Save.BufferedStorage.Cache"/>.
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
            /// <see cref="Save.BufferedStorage.Cache"/> is flushed. The provided <paramref name="offset"/>
            /// must be contained by the block of data held by the <see cref="Save.BufferedStorage.Cache"/>.
            /// </summary>
            /// <param name="offset">The offset in the base <see cref="IStorage"/> to be written to.</param>
            /// <param name="buffer">The buffer containing the data to be written.</param>
            public void Write(long offset, ReadOnlySpan<byte> buffer)
            {
                Assert.SdkRequires(!Cache.IsNull);
                Cache.Value.Write(offset, buffer);
            }

            /// <summary>
            /// If the current <see cref="Save.BufferedStorage.Cache"/> is dirty,
            /// flushes its data to the base <see cref="IStorage"/>.
            /// </summary>
            /// <returns>The <see cref="Result"/> of the operation.</returns>
            public Result Flush()
            {
                Assert.SdkRequires(!Cache.IsNull);
                return Cache.Value.Flush();
            }

            /// <summary>
            /// Invalidates the data in the current <see cref="Save.BufferedStorage.Cache"/>.
            /// Any dirty data will be discarded.
            /// </summary>
            public void Invalidate()
            {
                Assert.SdkRequires(!Cache.IsNull);
                Cache.Value.Invalidate();
            }

            /// <summary>
            /// Checks if the current <see cref="Save.BufferedStorage.Cache"/> covers any of the specified range.
            /// </summary>
            /// <param name="offset">The start offset of the range to check.</param>
            /// <param name="size">The size of the range to check.</param>
            /// <returns><see langword="true"/> if the current <see cref="Save.BufferedStorage.Cache"/>'s range
            /// covers any of the input range. Otherwise, <see langword="false"/>.</returns>
            public bool Hits(long offset, long size)
            {
                Assert.SdkRequires(!Cache.IsNull);
                return Cache.Value.Hits(offset, size);
            }

            /// <summary>
            /// Releases the current <see cref="Save.BufferedStorage.Cache"/> to return to the fetch list.
            /// </summary>
            private void Release()
            {
                if (!Cache.IsNull)
                {
                    // Make sure the Cache instance is in-range.
                    Assert.SdkAssert(!Unsafe.IsAddressLessThan(ref Cache.Value,
                        ref MemoryMarshal.GetArrayDataReference(BufferedStorage.Caches)));

                    Assert.SdkAssert(!Unsafe.IsAddressGreaterThan(ref Cache.Value,
                        ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(BufferedStorage.Caches),
                            BufferedStorage.CacheCount)));

                    Cache.Value.Link();
                    Cache = default;
                }
            }
        }

        /// <summary>
        /// Provides exclusive access to a <see cref="Save.BufferedStorage.Cache"/>
        /// entry in a <see cref="Save.BufferedStorage"/>.
        /// </summary>
        private ref struct UniqueCache
        {
            // ReSharper disable once MemberHidesStaticFromOuterClass
            private Ref<Cache> Cache { get; set; }
            private BufferedStorage BufferedStorage { get; }

            public UniqueCache(BufferedStorage bufferedStorage)
            {
                Assert.SdkRequiresNotNull(bufferedStorage);
                Cache = default;
                BufferedStorage = bufferedStorage;
            }

            /// <summary>
            /// Disposes the <see cref="UniqueCache"/>, releasing any held <see cref="Save.BufferedStorage.Cache"/>.
            /// </summary>
            public void Dispose()
            {
                if (!Cache.IsNull)
                {
                    lock (BufferedStorage.Locker)
                    {
                        Cache.Value.UnprepareFetch();
                    }
                }
            }

            /// <summary>
            /// Attempts to gain exclusive access to the <see cref="Save.BufferedStorage.Cache"/> held by
            /// <paramref name="sharedCache"/> and prepare it to read a new block from the base <see cref="IStorage"/>.
            /// </summary>
            /// <param name="sharedCache">The <see cref="SharedCache"/> to gain exclusive access to.</param>
            /// <returns>The <see cref="Result"/> of the operation, and <see langword="true"/> if exclusive
            /// access to the <see cref="Cache"/> was gained; <see langword="false"/> if not.</returns>
            public (Result Result, bool wasUpgradeSuccessful) Upgrade(in SharedCache sharedCache)
            {
                Assert.SdkRequires(BufferedStorage == sharedCache.BufferedStorage);
                Assert.SdkRequires(!sharedCache.Cache.IsNull);

                lock (BufferedStorage.Locker)
                {
                    (Result Result, bool wasUpgradeSuccessful) result = sharedCache.Cache.Value.PrepareFetch();

                    if (result.Result.IsSuccess() && result.wasUpgradeSuccessful)
                        Cache = sharedCache.Cache;

                    return result;
                }
            }

            /// <summary>
            /// Reads the storage block containing the specified offset into the
            /// <see cref="Save.BufferedStorage.Cache"/>'s buffer, and sets the Cache to that offset.
            /// </summary>
            /// <param name="offset">An offset in the block to fetch.</param>
            /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
            /// <see cref="ResultFs.BufferAllocationFailed"/>: A buffer could not be allocated.</returns>
            public Result Fetch(long offset)
            {
                Assert.SdkRequires(!Cache.IsNull);

                return Cache.Value.Fetch(offset);
            }

            /// <summary>
            /// Fills the <see cref="Save.BufferedStorage.Cache"/>'s buffer from an input buffer containing a block of data
            /// read from the base <see cref="IStorage"/>, and sets the Cache to that offset.
            /// </summary>
            /// <param name="offset">The start offset of the block in the base <see cref="IStorage"/>
            /// that the data was read from.</param>
            /// <param name="buffer">A buffer containing the data read from the base <see cref="IStorage"/>.</param>
            /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
            /// <see cref="ResultFs.BufferAllocationFailed"/>: A buffer could not be allocated.</returns>
            public Result FetchFromBuffer(long offset, ReadOnlySpan<byte> buffer)
            {
                Assert.SdkRequires(!Cache.IsNull);

                return Cache.Value.FetchFromBuffer(offset, buffer);
            }
        }

        private SubStorage BaseStorage { get; set; }
        private IBufferManager BufferManager { get; set; }
        private long BlockSize { get; set; }

        private long _baseStorageSize;
        private long BaseStorageSize
        {
            get => _baseStorageSize;
            set => _baseStorageSize = value;
        }

        private Cache[] Caches { get; set; }
        private int CacheCount { get; set; }
        private int NextAcquireCacheIndex { get; set; }
        private int NextFetchCacheIndex { get; set; }
        private object Locker { get; } = new();
        private bool BulkReadEnabled { get; set; }

        /// <summary>
        /// The <see cref="Cache"/> at which new <see cref="SharedCache"/>s will begin iterating.
        /// </summary>
        private ref Cache NextAcquireCache => ref Caches[NextAcquireCacheIndex];

        /// <summary>
        /// A list of <see cref="Cache"/>s that can be used for fetching
        /// new blocks of data from the base <see cref="IStorage"/>.
        /// </summary>
        private ref Cache NextFetchCache => ref Caches[NextFetchCacheIndex];

        /// <summary>
        /// Creates an uninitialized <see cref="BufferedStorage"/>.
        /// </summary>
        public BufferedStorage()
        {
            NextAcquireCacheIndex = InvalidIndex;
            NextFetchCacheIndex = InvalidIndex;
        }

        /// <summary>
        /// Disposes the <see cref="BufferedStorage"/>, flushing any cached data.
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                FinalizeObject();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Initializes the <see cref="BufferedStorage"/>.
        /// Calling this method again afterwards will flush the current cache and
        /// reinitialize the <see cref="BufferedStorage"/> with the new parameters.
        /// </summary>
        /// <param name="baseStorage">The base storage to use.</param>
        /// <param name="bufferManager">The buffer manager used to allocate and cache memory.</param>
        /// <param name="blockSize">The size of each cached block. Must be a power of 2.</param>
        /// <param name="cacheCount">The maximum number of blocks that can be cached at one time.</param>
        /// <returns></returns>
        public Result Initialize(SubStorage baseStorage, IBufferManager bufferManager, int blockSize, int cacheCount)
        {
            Assert.SdkRequiresNotNull(baseStorage);
            Assert.SdkRequiresNotNull(bufferManager);
            Assert.SdkRequiresLess(0, blockSize);
            Assert.SdkRequires(BitUtil.IsPowerOfTwo(blockSize));
            Assert.SdkRequiresLess(0, cacheCount);

            // Get the base storage size.
            Result rc = baseStorage.GetSize(out _baseStorageSize);
            if (rc.IsFailure()) return rc;

            // Set members.
            BaseStorage = baseStorage;
            BufferManager = bufferManager;
            BlockSize = blockSize;
            CacheCount = cacheCount;

            // Allocate the caches.
            if (Caches != null)
            {
                for (int i = 0; i < Caches.Length; i++)
                {
                    Caches[i].FinalizeObject();
                }
            }

            Caches = new Cache[cacheCount];
            if (Caches == null)
            {
                return ResultFs.AllocationMemoryFailedInBufferedStorageA.Log();
            }

            // Initialize the caches.
            for (int i = 0; i < Caches.Length; i++)
            {
                Caches[i].Initialize(this, i);
            }

            NextAcquireCacheIndex = 0;
            return Result.Success;
        }

        /// <summary>
        /// Finalizes this <see cref="BufferedStorage"/>, flushing all buffers and leaving it in an uninitialized state.
        /// </summary>
        public void FinalizeObject()
        {
            BaseStorage = null;
            BaseStorageSize = 0;

            foreach (Cache cache in Caches)
            {
                cache.Dispose();
            }

            Caches = null;
            CacheCount = 0;
            NextFetchCacheIndex = InvalidIndex;
        }

        /// <summary>
        /// Has this <see cref="BufferedStorage"/> been initialized?
        /// </summary>
        /// <returns><see langword="true"/> if this <see cref="BufferedStorage"/> is initialized.
        /// Otherwise, <see langword="false"/>.</returns>
        public bool IsInitialized() => Caches != null;

        protected override Result DoRead(long offset, Span<byte> destination)
        {
            Assert.SdkRequires(IsInitialized());

            // Succeed if zero size.
            if (destination.Length == 0)
                return Result.Success;

            // Do the read.
            return ReadCore(offset, destination);
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
        {
            Assert.SdkRequires(IsInitialized());

            // Succeed if zero size.
            if (source.Length == 0)
                return Result.Success;

            // Do the read.
            return WriteCore(offset, source);
        }

        protected override Result DoGetSize(out long size)
        {
            Assert.SdkRequires(IsInitialized());

            size = BaseStorageSize;
            return Result.Success;
        }

        protected override Result DoSetSize(long size)
        {
            Assert.SdkRequires(IsInitialized());

            Result rc;
            long prevSize = BaseStorageSize;
            if (prevSize < size)
            {
                // Prepare to expand.
                if (!Alignment.IsAlignedPow2(prevSize, (uint)BlockSize))
                {
                    using var cache = new SharedCache(this);
                    long invalidateOffset = prevSize;
                    long invalidateSize = size - prevSize;

                    if (cache.AcquireNextOverlappedCache(invalidateOffset, invalidateSize))
                    {
                        rc = cache.Flush();
                        if (rc.IsFailure()) return rc;

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
                bool isFragment = Alignment.IsAlignedPow2(size, (uint)BlockSize);

                while (cache.AcquireNextOverlappedCache(invalidateOffset, invalidateSize))
                {
                    if (isFragment && cache.Hits(invalidateOffset, 1))
                    {
                        rc = cache.Flush();
                        if (rc.IsFailure()) return rc;
                    }

                    cache.Invalidate();
                }
            }

            // Set the size.
            rc = BaseStorage.SetSize(size);
            if (rc.IsFailure()) return rc;

            // Get our new size.
            rc = BaseStorage.GetSize(out long newSize);
            if (rc.IsFailure()) return rc;

            BaseStorageSize = newSize;
            return Result.Success;
        }

        protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            Assert.SdkRequires(IsInitialized());

            // Invalidate caches if needed.
            if (operationId == OperationId.InvalidateCache)
            {
                using var cache = new SharedCache(this);

                while (cache.AcquireNextOverlappedCache(offset, size))
                    cache.Invalidate();
            }

            return BaseStorage.OperateRange(outBuffer, operationId, offset, size, inBuffer);
        }

        protected override Result DoFlush()
        {
            Assert.SdkRequires(IsInitialized());

            // Flush caches.
            using var cache = new SharedCache(this);
            while (cache.AcquireNextDirtyCache())
            {
                Result flushResult = cache.Flush();
                if (flushResult.IsFailure()) return flushResult;
            }

            // Flush the base storage.
            return BaseStorage.Flush();
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
        public IBufferManager GetBufferManager() => BufferManager;

        public void EnableBulkRead() => BulkReadEnabled = true;

        /// <summary>
        /// Flushes the cache to the base <see cref="IStorage"/> if less than 1/8 of the
        /// <see cref="IBufferManager"/>'s space can be used for allocation.
        /// </summary>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        private Result PrepareAllocation()
        {
            uint flushThreshold = (uint)BufferManager.GetTotalSize() / 8;

            if (BufferManager.GetTotalAllocatableSize() < flushThreshold)
            {
                Result rc = Flush();
                if (rc.IsFailure()) return rc;
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
            uint flushThreshold = (uint)BufferManager.GetTotalSize() / 4;

            if (BufferManager.GetTotalAllocatableSize() < flushThreshold)
            {
                using var cache = new SharedCache(this);
                int dirtyCount = 0;

                while (cache.AcquireNextDirtyCache())
                {
                    if (++dirtyCount > 1)
                    {
                        Result rc = cache.Flush();
                        if (rc.IsFailure()) return rc;

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
            Assert.SdkRequiresNotNull(Caches);
            Assert.SdkRequiresNotNull(destination);

            // Validate the offset.
            long baseStorageSize = BaseStorageSize;
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
            if (BulkReadEnabled)
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

                if (remainingSize < bulkReadSizeMax)
                {
                    // Try to do a bulk read.
                    Result rc = BulkRead(currentOffset, destination.Slice((int)bufferOffset, (int)remainingSize),
                        headCacheNeeded, tailCacheNeeded);

                    // If the read fails due to insufficient pooled buffer size,
                    // then we want to fall back to the normal read path.
                    if (!ResultFs.AllocationPooledBufferNotEnoughSize.Includes(rc))
                        return rc;
                }
            }

            // Repeatedly read until we're done.
            while (remainingSize > 0)
            {
                // Determine how much to read this iteration.
                int currentSize;

                // If the offset is in the middle of a block. Read the remaining part of that block.
                if (!Alignment.IsAlignedPow2(currentOffset, (uint)BlockSize))
                {
                    long alignedSize = BlockSize - (currentOffset & (BlockSize - 1));
                    currentSize = (int)Math.Min(alignedSize, remainingSize);
                }
                // If we only have a partial block left to read, read that partial block.
                else if (remainingSize < BlockSize)
                {
                    currentSize = (int)remainingSize;
                }
                // We have at least one full block to read. Read all the remaining full blocks at once.
                else
                {
                    currentSize = (int)Alignment.AlignDownPow2(remainingSize, (uint)BlockSize);
                }

                Span<byte> currentDestination = destination.Slice((int)bufferOffset, currentSize);

                // If reading a single block or less, read it using the cache
                if (currentSize <= BlockSize)
                {
                    using var cache = new SharedCache(this);

                    // Get the cache for our current block
                    if (!cache.AcquireNextOverlappedCache(currentOffset, currentSize))
                    {
                        // The block wasn't in the cache. Read the block from the base storage
                        Result rc = PrepareAllocation();
                        if (rc.IsFailure()) return rc;

                        // Loop until we can get exclusive access to the cache block
                        while (true)
                        {
                            if (!cache.AcquireFetchableCache())
                                return ResultFs.OutOfResource.Log();

                            // Try to upgrade out SharedCache to a UniqueCache
                            using var fetchCache = new UniqueCache(this);
                            (Result Result, bool wasUpgradeSuccessful) upgradeResult = fetchCache.Upgrade(in cache);
                            if (upgradeResult.Result.IsFailure())
                                return upgradeResult.Result;

                            // Fetch the data from the base storage into the cache buffer if successful
                            if (upgradeResult.wasUpgradeSuccessful)
                            {
                                rc = fetchCache.Fetch(currentOffset);
                                if (rc.IsFailure()) return rc;

                                break;
                            }
                        }

                        rc = ControlDirtiness();
                        if (rc.IsFailure()) return rc;
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
                            if (rc.IsFailure()) return rc;

                            cache.Invalidate();
                        }
                    }

                    // Read directly from the base storage to the destination buffer
                    Result rcRead = BaseStorage.Read(currentOffset, currentDestination);
                    if (rcRead.IsFailure()) return rcRead;
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
            bool isCacheNeeded = !Alignment.IsAlignedPow2(offset, (uint)BlockSize);

            while (size > 0)
            {
                long currentSize;

                if (!Alignment.IsAlignedPow2(offset, (uint)BlockSize))
                {
                    long alignedSize = Alignment.AlignUpPow2(offset, (uint)BlockSize) - offset;
                    currentSize = Math.Min(alignedSize, size);
                }
                else if (size < BlockSize)
                {
                    currentSize = size;
                }
                else
                {
                    currentSize = BlockSize;
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
            bool isCacheNeeded = !Alignment.IsAlignedPow2(offset + size, (uint)BlockSize);

            while (size > 0)
            {
                long currentOffsetEnd = offset + size;
                long currentSize;

                if (!Alignment.IsAlignedPow2(currentOffsetEnd, (uint)BlockSize))
                {
                    long alignedSize = currentOffsetEnd - Alignment.AlignDownPow2(currentOffsetEnd, (uint)BlockSize);
                    currentSize = Math.Min(alignedSize, size);
                }
                else if (size < BlockSize)
                {
                    currentSize = size;
                }
                else
                {
                    currentSize = BlockSize;
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
            long alignedOffset = Alignment.AlignDownPow2(offset, (uint)BlockSize);
            long alignedOffsetEnd = Math.Min(Alignment.AlignUpPow2(offset + buffer.Length, (uint)BlockSize),
                BaseStorageSize);
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
                    if (rc.IsFailure()) return rc;

                    cache.Invalidate();
                }
            }

            // Read from the base storage.
            rc = BaseStorage.Read(alignedOffset, workBuffer.Slice(0, (int)alignedSize));
            if (rc.IsFailure()) return rc;
            if (workBuffer != buffer)
            {
                workBuffer.Slice((int)(offset - alignedOffset), buffer.Length).CopyTo(buffer);
            }

            bool cached = false;

            // Cache the head block if needed.
            if (isHeadCacheNeeded)
            {
                rc = PrepareAllocation();
                if (rc.IsFailure()) return rc;

                using var cache = new SharedCache(this);
                while (true)
                {
                    if (!cache.AcquireFetchableCache())
                        return ResultFs.OutOfResource.Log();

                    using var fetchCache = new UniqueCache(this);
                    (Result Result, bool wasUpgradeSuccessful) upgradeResult = fetchCache.Upgrade(in cache);
                    if (upgradeResult.Result.IsFailure())
                        return upgradeResult.Result;

                    if (upgradeResult.wasUpgradeSuccessful)
                    {
                        rc = fetchCache.FetchFromBuffer(alignedOffset, workBuffer.Slice(0, (int)alignedSize));
                        if (rc.IsFailure()) return rc;
                        break;
                    }
                }

                cached = true;
            }

            // Cache the tail block if needed.
            if (isTailCacheNeeded && (!isHeadCacheNeeded || alignedSize > BlockSize))
            {
                if (!cached)
                {
                    rc = PrepareAllocation();
                    if (rc.IsFailure()) return rc;
                }

                using var cache = new SharedCache(this);
                while (true)
                {
                    if (!cache.AcquireFetchableCache())
                        return ResultFs.OutOfResource.Log();

                    using var fetchCache = new UniqueCache(this);
                    (Result Result, bool wasUpgradeSuccessful) upgradeResult = fetchCache.Upgrade(in cache);
                    if (upgradeResult.Result.IsFailure())
                        return upgradeResult.Result;

                    if (upgradeResult.wasUpgradeSuccessful)
                    {
                        long tailCacheOffset = Alignment.AlignDownPow2(offset + buffer.Length, (uint)BlockSize);
                        long tailCacheSize = alignedSize - tailCacheOffset + alignedOffset;

                        rc = fetchCache.FetchFromBuffer(tailCacheOffset,
                            workBuffer.Slice((int)(tailCacheOffset - alignedOffset), (int)tailCacheSize));
                        if (rc.IsFailure()) return rc;
                        break;
                    }
                }
            }

            if (cached)
            {
                rc = ControlDirtiness();
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
        }

        private Result WriteCore(long offset, ReadOnlySpan<byte> source)
        {
            Assert.SdkRequiresNotNull(Caches);
            Assert.SdkRequiresNotNull(source);

            // Validate the offset.
            long baseStorageSize = BaseStorageSize;

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

                if (!Alignment.IsAlignedPow2(currentOffset, (uint)BlockSize))
                {
                    int alignedSize = (int)(BlockSize - (currentOffset & (BlockSize - 1)));
                    currentSize = Math.Min(alignedSize, remainingSize);
                }
                else if (remainingSize < BlockSize)
                {
                    currentSize = remainingSize;
                }
                else
                {
                    currentSize = Alignment.AlignDownPow2(remainingSize, (uint)BlockSize);
                }

                Result rc;
                if (currentSize < BlockSize)
                {
                    using var cache = new SharedCache(this);

                    if (!cache.AcquireNextOverlappedCache(currentOffset, currentSize))
                    {
                        rc = PrepareAllocation();
                        if (rc.IsFailure()) return rc;

                        while (true)
                        {
                            if (!cache.AcquireFetchableCache())
                                return ResultFs.OutOfResource.Log();

                            using var fetchCache = new UniqueCache(this);
                            (Result Result, bool wasUpgradeSuccessful) upgradeResult = fetchCache.Upgrade(in cache);
                            if (upgradeResult.Result.IsFailure())
                                return upgradeResult.Result;

                            if (upgradeResult.wasUpgradeSuccessful)
                            {
                                rc = fetchCache.Fetch(currentOffset);
                                if (rc.IsFailure()) return rc;
                                break;
                            }
                        }
                    }
                    cache.Write(currentOffset, currentSource.Slice(0, currentSize));

                    BufferManagerUtility.EnableBlockingBufferManagerAllocation();

                    rc = ControlDirtiness();
                    if (rc.IsFailure()) return rc;
                }
                else
                {
                    using (var cache = new SharedCache(this))
                    {
                        while (cache.AcquireNextOverlappedCache(currentOffset, currentSize))
                        {
                            rc = cache.Flush();
                            if (rc.IsFailure()) return rc;

                            cache.Invalidate();
                        }
                    }

                    rc = BaseStorage.Write(currentOffset, currentSource.Slice(0, currentSize));
                    if (rc.IsFailure()) return rc;

                    BufferManagerUtility.EnableBlockingBufferManagerAllocation();
                }

                remainingSize -= currentSize;
                currentOffset += currentSize;
                bufferOffset += currentSize;
            }

            return Result.Success;
        }
    }
}
