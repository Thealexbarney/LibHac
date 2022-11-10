using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator;

public interface ISubDirectoryFileSystemCreator
{
    Result Create(ref SharedRef<IFileSystem> outSubDirFileSystem, ref SharedRef<IFileSystem> baseFileSystem, in Path path);
}