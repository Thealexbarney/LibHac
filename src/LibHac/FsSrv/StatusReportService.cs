using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSrv.FsCreator;
using LibHac.FsSystem;
using LibHac.Os;

namespace LibHac.FsSrv;

/// <summary>
/// Handles status-report-related calls for <see cref="FileSystemProxyImpl"/>.
/// </summary>
/// <remarks><para>This struct handles forwarding calls to the <see cref="StatusReportServiceImpl"/> object.
/// No permissions are needed to call any of this struct's functions.</para>
/// <para>Based on nnSdk 14.3.0 (FS 14.1.0)</para></remarks>
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

/// <summary>
/// Manages getting and resetting various status reports and statistics about parts of the FS service.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
public class StatusReportServiceImpl
{
    private Configuration _config;
    private SdkMutexType _mutex;

    public StatusReportServiceImpl(in Configuration configuration)
    {
        _config = configuration;
        _mutex = new SdkMutexType();
    }

    public struct Configuration
    {
        public NcaFileSystemServiceImpl NcaFileSystemServiceImpl;
        public SaveDataFileSystemServiceImpl SaveDataFileSystemServiceImpl;
        public FatFileSystemCreator FatFileSystemCreator;
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
        errorInfo = default;

        Assert.SdkRequiresNotNull(_config.NcaFileSystemServiceImpl);

        _config.NcaFileSystemServiceImpl.GetAndClearRomFsErrorInfo(out errorInfo.RemountForDataCorruptionCount,
            out errorInfo.UnrecoverableDataCorruptionByRemountCount,
            out errorInfo.RecoveredByInvalidateCacheCount);

        if (_config.FatFileSystemCreator is not null)
        {
            _config.FatFileSystemCreator.GetAndClearFatFsError(out errorInfo.FatFsError);
            _config.FatFileSystemCreator.GetAndClearFatReportInfo(out errorInfo.BisSystemFatReportInfo,
                out errorInfo.BisUserFatReport, out errorInfo.SdCardFatReport);
        }

        Assert.SdkRequiresNotNull(_config.SaveDataFileSystemServiceImpl);

        Result rc = _config.SaveDataFileSystemServiceImpl.GetSaveDataIndexCount(out int count);
        if (rc.IsFailure()) return rc;

        errorInfo.SaveDataIndexCount = count;
        return Result.Success;
    }

    public Result GetAndClearMemoryReportInfo(out MemoryReportInfo reportInfo)
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        reportInfo = default;

        reportInfo.PooledBufferFreeSizePeak = _config.FsServer.GetPooledBufferFreeSizePeak();
        reportInfo.PooledBufferRetriedCount = _config.FsServer.GetPooledBufferRetriedCount();
        reportInfo.PooledBufferReduceAllocationCount = _config.FsServer.GetPooledBufferReduceAllocationCount();
        reportInfo.PooledBufferFailedIdealAllocationCountOnAsyncAccess =
            _config.FsServer.GetPooledBufferFailedIdealAllocationCountOnAsyncAccess();

        _config.FsServer.ClearPooledBufferPeak();

        if (_config.BufferManagerMemoryReport is not null)
        {
            reportInfo.BufferManagerFreeSizePeak = _config.BufferManagerMemoryReport.GetFreeSizePeak();
            reportInfo.BufferManagerTotalAllocatableSizePeak = _config.BufferManagerMemoryReport.GetTotalAllocatableSizePeak();
            reportInfo.BufferManagerRetriedCount = _config.BufferManagerMemoryReport.GetRetriedCount();
            _config.BufferManagerMemoryReport.Clear();
        }

        if (_config.ExpHeapMemoryReport is not null)
        {
            reportInfo.ExpHeapFreeSizePeak = _config.ExpHeapMemoryReport.GetFreeSizePeak();
            _config.ExpHeapMemoryReport.Clear();
        }

        if (_config.BufferPoolMemoryReport is not null)
        {
            reportInfo.BufferPoolFreeSizePeak = _config.BufferPoolMemoryReport.GetFreeSizePeak();
            reportInfo.BufferPoolAllocateSizeMax = _config.BufferPoolMemoryReport.GetAllocateSizeMax();
            _config.BufferPoolMemoryReport.Clear();
        }

        if (_config.GetPatrolAllocateCounts is not null)
        {
            _config.GetPatrolAllocateCounts(out reportInfo.PatrolReadAllocateBufferSuccessCount,
                out reportInfo.PatrolReadAllocateBufferFailureCount);
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