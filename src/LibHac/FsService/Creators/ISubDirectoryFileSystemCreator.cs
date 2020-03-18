using LibHac.Common;
using LibHac.Fs;

namespace LibHac.FsService.Creators
{
    public interface ISubDirectoryFileSystemCreator
    {
        Result Create(out IFileSystem subDirFileSystem, IFileSystem baseFileSystem, U8Span path);
        Result Create(out IFileSystem subDirFileSystem, IFileSystem baseFileSystem, U8Span path, bool preserveUnc);
    }
}