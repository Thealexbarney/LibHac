using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator;

public interface ISubDirectoryFileSystemCreator
{
    Result Create(ref SharedRef<IFileSystem> outSubDirFileSystem, ref readonly SharedRef<IFileSystem> baseFileSystem, ref readonly Path path);
}