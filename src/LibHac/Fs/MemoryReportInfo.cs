using System.Runtime.InteropServices;

namespace LibHac.Fs
{
    [StructLayout(LayoutKind.Sequential, Size = 0x80)]
    public struct MemoryReportInfo
    {
        long PooledBufferFreeSizePeak;
        long PooledBufferRetriedCount;
        long PooledBufferReduceAllocationCount;
        long BufferManagerFreeSizePeak;
        long BufferManagerRetiredCount;
        long ExpHeapFreeSizePeak;
        long BufferPoolFreeSizePeak;
        long PatrolAllocateSuccessCount;
        long PatrolAllocateFailureCount;
        long BufferManagerTotalAllocatableSizePeak;
        long BufferPoolAllocateSizeMax;
    };

}
