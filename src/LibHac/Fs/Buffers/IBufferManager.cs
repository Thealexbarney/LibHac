using System;

using CacheHandle = System.Int64;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs
{
    // ReSharper disable once InconsistentNaming
    /// <summary>
    /// Handles buffer allocation, deallocation, and caching.<br/>
    /// An allocated buffer may be placed in the cache using <see cref="RegisterCache"/>.
    /// Caching a buffer saves the buffer for later retrieval, but tells the buffer manager that it can deallocate the
    /// buffer if the memory is needed elsewhere. Any cached buffer may be evicted from the cache if there is no free
    /// space for a requested allocation or if the cache is full when caching a new buffer.
    /// A cached buffer can be retrieved using <see cref="AcquireCache"/>.
    /// </summary>
    public abstract class IBufferManager : IDisposable
    {
        public readonly struct BufferAttribute
        {
            public int Level { get; }

            public BufferAttribute(int level)
            {
                Level = level;
            }
        }

        public const int BufferLevelMin = 0;

        public Buffer AllocateBuffer(int size, BufferAttribute attribute) =>
            DoAllocateBuffer(size, attribute);

        /// <summary>
        /// Allocates a new buffer with an attribute of level 0.
        /// </summary>
        /// <param name="size">The minimum size of the buffer to allocate</param>
        /// <returns>The allocated <see cref="Buffer"/> if successful. Otherwise a null <see cref="Buffer"/>.</returns>
        public Buffer AllocateBuffer(int size) => DoAllocateBuffer(size, new BufferAttribute());

        /// <summary>
        /// Deallocates the provided <see cref="Buffer"/>.
        /// </summary>
        /// <param name="buffer">The Buffer to deallocate.</param>
        public void DeallocateBuffer(Buffer buffer) => DoDeallocateBuffer(buffer);

        /// <summary>
        /// Adds a <see cref="Buffer"/> to the cache.
        /// The buffer must have been allocated from this <see cref="IBufferManager"/>.<br/>
        /// The buffer must not be used after adding it to the cache.
        /// </summary>
        /// <param name="buffer">The buffer to cache.</param>
        /// <param name="attribute">The buffer attribute.</param>
        /// <returns>A handle that can be used to retrieve the buffer at a later time.</returns>
        public CacheHandle RegisterCache(Buffer buffer, BufferAttribute attribute) =>
            DoRegisterCache(buffer, attribute);

        /// <summary>
        /// Attempts to acquire a cached <see cref="Buffer"/>.
        /// If the buffer was evicted from the cache, a null buffer is returned.
        /// </summary>
        /// <param name="handle">The handle received when registering the buffer.</param>
        /// <returns>The requested <see cref="Buffer"/> if it's still in the cache;
        /// otherwise a null <see cref="Buffer"/></returns>
        public Buffer AcquireCache(CacheHandle handle) => DoAcquireCache(handle);

        /// <summary>
        /// Gets the total size of the <see cref="IBufferManager"/>'s heap.
        /// </summary>
        /// <returns>The total size of the heap.</returns>
        public int GetTotalSize() => DoGetTotalSize();

        /// <summary>
        /// Gets the amount of free space in the heap that is not currently allocated or cached.
        /// </summary>
        /// <returns>The amount of free space.</returns>
        public int GetFreeSize() => DoGetFreeSize();

        /// <summary>
        /// Gets the amount of space that can be used for new allocations.
        /// This includes free space and space used by cached buffers.
        /// </summary>
        /// <returns>The amount of allocatable space.</returns>
        public int GetTotalAllocatableSize() => DoGetTotalAllocatableSize();

        /// <summary>
        /// Gets the largest amount of free space there's been at one time since the peak was last cleared.
        /// </summary>
        /// <returns>The peak amount of free space.</returns>
        public int GetFreeSizePeak() => DoGetFreeSizePeak();

        /// <summary>
        /// Gets the largest amount of allocatable space there's been at one time since the peak was last cleared.
        /// </summary>
        /// <returns>The peak amount of allocatable space.</returns>
        public int GetTotalAllocatableSizePeak() => DoGetTotalAllocatableSizePeak();

        /// <summary>
        /// Gets the number of times an allocation or cache registration needed to be retried after deallocating
        /// a cache entry because of insufficient heap space or cache space.
        /// </summary>
        /// <returns>The number of retries.</returns>
        public int GetRetriedCount() => DoGetRetriedCount();

        /// <summary>
        /// Resets the free and allocatable peak sizes, setting the peak sizes to the actual current sizes.
        /// </summary>
        public void ClearPeak() => DoClearPeak();

        protected abstract Buffer DoAllocateBuffer(int size, BufferAttribute attribute);
        protected abstract void DoDeallocateBuffer(Buffer buffer);
        protected abstract CacheHandle DoRegisterCache(Buffer buffer, BufferAttribute attribute);
        protected abstract Buffer DoAcquireCache(CacheHandle handle);
        protected abstract int DoGetTotalSize();
        protected abstract int DoGetFreeSize();
        protected abstract int DoGetTotalAllocatableSize();
        protected abstract int DoGetFreeSizePeak();
        protected abstract int DoGetTotalAllocatableSizePeak();
        protected abstract int DoGetRetriedCount();
        protected abstract void DoClearPeak();

        protected virtual void Dispose(bool disposing) { }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
