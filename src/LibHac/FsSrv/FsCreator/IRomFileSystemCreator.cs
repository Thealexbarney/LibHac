using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator
{
    public interface IRomFileSystemCreator
    {
        Result Create(out ReferenceCountedDisposable<IFileSystem> fileSystem, ReferenceCountedDisposable<IStorage> romFsStorage);
    }
}
