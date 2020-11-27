using System;

using CacheHandle = System.Int64;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs
{
    // ReSharper disable once InconsistentNaming
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

        public void DeallocateBuffer(Buffer buffer) => DoDeallocateBuffer(buffer);

        public CacheHandle RegisterCache(Buffer buffer, BufferAttribute attribute) =>
            DoRegisterCache(buffer, attribute);

        public Buffer AcquireCache(CacheHandle handle) => DoAcquireCache(handle);
        public int GetTotalSize() => DoGetTotalSize();
        public int GetFreeSize() => DoGetFreeSize();
        public int GetTotalAllocatableSize() => DoGetTotalAllocatableSize();
        public int GetFreeSizePeak() => DoGetFreeSizePeak();
        public int GetTotalAllocatableSizePeak() => DoGetTotalAllocatableSizePeak();
        public int GetRetriedCount() => DoGetRetriedCount();
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
