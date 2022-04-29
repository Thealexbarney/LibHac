using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator;

public interface ILocalFileSystemCreator
{
    Result Create(ref SharedRef<IFileSystem> outFileSystem, in Path rootPath, bool openCaseSensitive, bool ensureRootPathExists, Result pathNotFoundResult);
}