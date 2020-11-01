using LibHac.FsSrv.Creators;

namespace LibHac.FsSrv
{
    public class FileSystemProxyConfiguration
    {
        public FileSystemCreators FsCreatorInterfaces { get; set; }
        public BaseFileSystemServiceImpl BaseFileSystemService { get; set; }
        public NcaFileSystemServiceImpl NcaFileSystemService { get; set; }
        public SaveDataFileSystemServiceImpl SaveDataFileSystemService { get; set; }
        public ProgramRegistryServiceImpl ProgramRegistryService { get; set; }
    }
}
