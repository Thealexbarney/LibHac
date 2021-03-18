using LibHac.FsSrv.FsCreator;

namespace LibHac.FsSrv
{
    public class FileSystemProxyConfiguration
    {
        public FileSystemCreatorInterfaces FsCreatorInterfaces { get; set; }
        public BaseStorageServiceImpl BaseStorageService { get; set; }
        public BaseFileSystemServiceImpl BaseFileSystemService { get; set; }
        public NcaFileSystemServiceImpl NcaFileSystemService { get; set; }
        public SaveDataFileSystemServiceImpl SaveDataFileSystemService { get; set; }
        public AccessFailureManagementServiceImpl AccessFailureManagementService { get; set; }
        public TimeServiceImpl TimeService { get; set; }
        public StatusReportServiceImpl StatusReportService { get; set; }
        public ProgramRegistryServiceImpl ProgramRegistryService { get; set; }
        public AccessLogServiceImpl AccessLogService { get; set; }
    }
}
