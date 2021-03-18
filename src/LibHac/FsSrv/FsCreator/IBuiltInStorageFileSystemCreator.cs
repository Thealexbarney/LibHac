using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator
{
    public interface IBuiltInStorageFileSystemCreator
    {
        // Todo: Remove raw IFileSystem overload
        Result Create(out IFileSystem fileSystem, string rootPath, BisPartitionId partitionId);
        Result Create(out ReferenceCountedDisposable<IFileSystem> fileSystem, U8Span rootPath, BisPartitionId partitionId, bool caseSensitive);
        Result CreateFatFileSystem(out IFileSystem fileSystem, BisPartitionId partitionId);
        Result SetBisRootForHost(BisPartitionId partitionId, string rootPath);
    }
}