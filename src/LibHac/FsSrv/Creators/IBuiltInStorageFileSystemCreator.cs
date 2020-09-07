using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.Creators
{
    public interface IBuiltInStorageFileSystemCreator
    {
        // Todo: Remove raw IFileSystem overload
        Result Create(out IFileSystem fileSystem, string rootPath, BisPartitionId partitionId);
        Result Create(out ReferenceCountedDisposable<IFileSystem> fileSystem, U8Span rootPath, BisPartitionId partitionId);
        Result CreateFatFileSystem(out IFileSystem fileSystem, BisPartitionId partitionId);
        Result SetBisRootForHost(BisPartitionId partitionId, string rootPath);
    }
}