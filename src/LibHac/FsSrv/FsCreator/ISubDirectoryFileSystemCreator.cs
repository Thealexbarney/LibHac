using LibHac.Common;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator
{
    public interface ISubDirectoryFileSystemCreator
    {
        Result Create(out ReferenceCountedDisposable<IFileSystem> subDirFileSystem, ref ReferenceCountedDisposable<IFileSystem> baseFileSystem, U8Span path);
        Result Create(out ReferenceCountedDisposable<IFileSystem> subDirFileSystem, ref ReferenceCountedDisposable<IFileSystem> baseFileSystem, U8Span path, bool preserveUnc);
    }
}