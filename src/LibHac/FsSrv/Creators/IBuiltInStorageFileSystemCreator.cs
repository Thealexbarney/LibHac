using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.Creators
{
    public interface IBuiltInStorageFileSystemCreator
    {
        Result Create(out IFileSystem fileSystem, string rootPath, BisPartitionId partitionId);
        Result CreateFatFileSystem(out IFileSystem fileSystem, BisPartitionId partitionId);
        Result SetBisRootForHost(BisPartitionId partitionId, string rootPath);
    }
}