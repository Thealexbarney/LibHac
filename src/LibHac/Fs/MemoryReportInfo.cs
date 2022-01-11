using LibHac.Common.FixedArrays;

namespace LibHac.Fs;

public struct MemoryReportInfo
{
    public long PooledBufferFreeSizePeak;
    public long PooledBufferRetriedCount;
    public long PooledBufferReduceAllocationCount;
    public long BufferManagerFreeSizePeak;
    public long BufferManagerRetriedCount;
    public long ExpHeapFreeSizePeak;
    public long BufferPoolFreeSizePeak;
    public long PatrolReadAllocateBufferSuccessCount;
    public long PatrolReadAllocateBufferFailureCount;
    public long BufferManagerTotalAllocatableSizePeak;
    public long BufferPoolAllocateSizeMax;
    public long PooledBufferFailedIdealAllocationCountOnAsyncAccess;
    public Array32<byte> Reserved;
}