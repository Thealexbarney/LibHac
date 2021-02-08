namespace LibHac.FsSrv
{
    public abstract class MemoryReport
    {
        public abstract long GetFreeSizePeak();
        public abstract long GetTotalAllocatableSizePeak();
        public abstract long GetRetriedCount();
        public abstract long GetAllocateSizeMax();
        public abstract void Clear();
    }
}
