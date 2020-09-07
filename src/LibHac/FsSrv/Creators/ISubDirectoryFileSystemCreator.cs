using LibHac.Common;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.Creators
{
    public interface ISubDirectoryFileSystemCreator
    {
        // Todo: Remove the raw IFileSystem overloads
        Result Create(out IFileSystem subDirFileSystem, IFileSystem baseFileSystem, U8Span path);
        Result Create(out IFileSystem subDirFileSystem, IFileSystem baseFileSystem, U8Span path, bool preserveUnc);

        Result Create(out ReferenceCountedDisposable<IFileSystem> subDirFileSystem, ReferenceCountedDisposable<IFileSystem> baseFileSystem, U8Span path);
        Result Create(out ReferenceCountedDisposable<IFileSystem> subDirFileSystem, ReferenceCountedDisposable<IFileSystem> baseFileSystem, U8Span path, bool preserveUnc);
    }
}