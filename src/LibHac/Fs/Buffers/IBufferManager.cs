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

        public (UIntPtr address, nuint size) AllocateBuffer(nuint size, BufferAttribute attribute) =>
            DoAllocateBuffer(size, attribute);

        public void DeallocateBuffer(UIntPtr address, nuint size) => DoDeallocateBuffer(address, size);

        public CacheHandle RegisterCache(UIntPtr address, nuint size, BufferAttribute attribute) =>
            DoRegisterCache(address, size, attribute);

        public (UIntPtr address, nuint size) AcquireCache(CacheHandle handle) => DoAcquireCache(handle);
        public nuint GetTotalSize() => DoGetTotalSize();
        public nuint GetFreeSize() => DoGetFreeSize();
        public nuint GetTotalAllocatableSize() => DoGetTotalAllocatableSize();
        public nuint GetFreeSizePeak() => DoGetFreeSizePeak();
        public nuint GetTotalAllocatableSizePeak() => DoGetTotalAllocatableSizePeak();
        public nuint GetRetriedCount() => DoGetRetriedCount();
        public void ClearPeak() => DoClearPeak();

        protected abstract (UIntPtr address, nuint size) DoAllocateBuffer(nuint size, BufferAttribute attribute);
        protected abstract void DoDeallocateBuffer(UIntPtr address, nuint size);
        protected abstract CacheHandle DoRegisterCache(UIntPtr address, nuint size, BufferAttribute attribute);
        protected abstract (UIntPtr address, nuint size) DoAcquireCache(CacheHandle handle);
        protected abstract nuint DoGetTotalSize();
        protected abstract nuint DoGetFreeSize();
        protected abstract nuint DoGetTotalAllocatableSize();
        protected abstract nuint DoGetFreeSizePeak();
        protected abstract nuint DoGetTotalAllocatableSizePeak();
        protected abstract nuint DoGetRetriedCount();
        protected abstract void DoClearPeak();

        protected virtual void Dispose(bool disposing) { }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
