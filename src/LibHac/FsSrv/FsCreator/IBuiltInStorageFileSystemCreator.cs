using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator
{
    public interface IBuiltInStorageFileSystemCreator
    {
        Result Create(out ReferenceCountedDisposable<IFileSystem> fileSystem, BisPartitionId partitionId, bool caseSensitive);
    }
}