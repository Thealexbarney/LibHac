using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator
{
    public interface IPartitionFileSystemCreator
    {
        // Todo: Remove non-shared overload
        Result Create(out IFileSystem fileSystem, IStorage pFsStorage);
        Result Create(out ReferenceCountedDisposable<IFileSystem> fileSystem, ReferenceCountedDisposable<IStorage> pFsStorage);
    }
}
