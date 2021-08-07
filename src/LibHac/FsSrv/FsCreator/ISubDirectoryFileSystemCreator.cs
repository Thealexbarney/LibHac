using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator
{
    public interface ISubDirectoryFileSystemCreator
    {
        Result Create(out ReferenceCountedDisposable<IFileSystem> subDirFileSystem, ref ReferenceCountedDisposable<IFileSystem> baseFileSystem, in Path path);
    }
}