using LibHac.Fs;

namespace LibHac.FsService.Creators
{
    public interface IBuiltInStorageFileSystemCreator
    {
        Result Create(out IFileSystem fileSystem, string rootPath, BisPartitionId partitionId);
        Result CreateFatFileSystem(out IFileSystem fileSystem, BisPartitionId partitionId);
        Result SetBisRootForHost(BisPartitionId partitionId, string rootPath);
    }
}