using System.Runtime.InteropServices;

namespace LibHac.Fs
{
    [StructLayout(LayoutKind.Sequential, Size = 0x80)]
    public struct MemoryReportInfo
    {
        public long PooledBufferFreeSizePeak;
        public long PooledBufferRetriedCount;
        public long PooledBufferReduceAllocationCount;
        public long BufferManagerFreeSizePeak;
        public long BufferManagerRetriedCount;
        public long ExpHeapFreeSizePeak;
        public long BufferPoolFreeSizePeak;
        public long PatrolAllocateSuccessCount;
        public long PatrolAllocateFailureCount;
        public long BufferManagerTotalAllocatableSizePeak;
        public long BufferPoolAllocateSizeMax;
    }
}
