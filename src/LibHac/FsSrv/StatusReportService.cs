using LibHac.Diag;
using LibHac.Fs;
using LibHac.Os;

namespace LibHac.FsSrv
{
    public readonly struct StatusReportService
    {
        private readonly StatusReportServiceImpl _serviceImpl;

        public StatusReportService(StatusReportServiceImpl serviceImpl)
        {
            _serviceImpl = serviceImpl;
        }

        public Result GetAndClearFileSystemProxyErrorInfo(out FileSystemProxyErrorInfo errorInfo)
        {
            return _serviceImpl.GetAndClearFileSystemProxyErrorInfo(out errorInfo);
        }

        public Result GetAndClearMemoryReportInfo(out MemoryReportInfo reportInfo)
        {
            return _serviceImpl.GetAndClearMemoryReportInfo(out reportInfo);
        }

        public Result GetFsStackUsage(out uint stackUsage, FsStackUsageThreadType threadType)
        {
            stackUsage = _serviceImpl.ReportStackUsage(threadType);
            return Result.Success;
        }
    }

    public class StatusReportServiceImpl
    {
        private Configuration _config;
        private SdkMutexType _mutex;

        public StatusReportServiceImpl(in Configuration configuration)
        {
            _config = configuration;
            _mutex.Initialize();
        }

        public struct Configuration
        {
            public NcaFileSystemServiceImpl NcaFsServiceImpl;
            public SaveDataFileSystemServiceImpl SaveFsServiceImpl;
            // Missing: FatFileSystemCreator (Not an IFatFileSystemCreator)
            public MemoryReport BufferManagerMemoryReport;
            public MemoryReport ExpHeapMemoryReport;
            public MemoryReport BufferPoolMemoryReport;
            public PatrolAllocateCountGetter GetPatrolAllocateCounts;
            public IStackUsageReporter MainThreadStackUsageReporter;
            public IStackUsageReporter IpcWorkerThreadStackUsageReporter;
            public IStackUsageReporter PipeLineWorkerThreadStackUsageReporter;

            // LibHac additions
            public FileSystemServer FsServer;
        }

        public Result GetAndClearFileSystemProxyErrorInfo(out FileSystemProxyErrorInfo errorInfo)
        {
            errorInfo = new FileSystemProxyErrorInfo();

            _config.NcaFsServiceImpl.GetAndClearRomFsErrorInfo(out errorInfo.RomFsRemountForDataCorruptionCount,
                out errorInfo.RomFsUnrecoverableDataCorruptionByRemountCount,
                out errorInfo.RomFsRecoveredByInvalidateCacheCount);

            // Missing: GetFatInfo

            Result rc = _config.SaveFsServiceImpl.GetSaveDataIndexCount(out int count);
            if (rc.IsFailure()) return rc;

            errorInfo.SaveDataIndexCount = count;
            return Result.Success;
        }

        public Result GetAndClearMemoryReportInfo(out MemoryReportInfo reportInfo)
        {
            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            reportInfo = new MemoryReportInfo();

            // Missing: Get and clear pooled buffer stats
            reportInfo.PooledBufferFreeSizePeak = 0;
            reportInfo.PooledBufferRetriedCount = 0;
            reportInfo.PooledBufferReduceAllocationCount = 0;

            MemoryReport report = _config.BufferManagerMemoryReport;
            if (report != null)
            {
                reportInfo.BufferManagerFreeSizePeak = report.GetFreeSizePeak();
                reportInfo.BufferManagerTotalAllocatableSizePeak = report.GetTotalAllocatableSizePeak();
                reportInfo.BufferManagerRetriedCount = report.GetRetriedCount();
                report.Clear();
            }

            report = _config.ExpHeapMemoryReport;
            if (report != null)
            {
                reportInfo.ExpHeapFreeSizePeak = report.GetFreeSizePeak();
                report.Clear();
            }

            report = _config.BufferPoolMemoryReport;
            if (report != null)
            {
                reportInfo.BufferPoolFreeSizePeak = report.GetFreeSizePeak();
                reportInfo.BufferPoolAllocateSizeMax = report.GetAllocateSizeMax();
                report.Clear();
            }

            if (_config.GetPatrolAllocateCounts != null)
            {
                _config.GetPatrolAllocateCounts(out reportInfo.PatrolAllocateSuccessCount,
                    out reportInfo.PatrolAllocateFailureCount);
            }

            return Result.Success;
        }

        public uint ReportStackUsage(FsStackUsageThreadType threadType)
        {
            switch (threadType)
            {
                case FsStackUsageThreadType.MainThread:
                    Assert.SdkRequiresNotNull(_config.MainThreadStackUsageReporter);
                    return _config.MainThreadStackUsageReporter.GetStackUsage();

                case FsStackUsageThreadType.IpcWorker:
                    Assert.SdkRequiresNotNull(_config.IpcWorkerThreadStackUsageReporter);
                    return _config.IpcWorkerThreadStackUsageReporter.GetStackUsage();

                case FsStackUsageThreadType.PipelineWorker:
                    Assert.SdkRequiresNotNull(_config.PipeLineWorkerThreadStackUsageReporter);
                    return _config.PipeLineWorkerThreadStackUsageReporter.GetStackUsage();

                default:
                    return 0;
            }
        }
    }
}
