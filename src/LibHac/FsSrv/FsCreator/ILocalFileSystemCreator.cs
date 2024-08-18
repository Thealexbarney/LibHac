using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator;

public interface ILocalFileSystemCreator
{
    Result Create(ref SharedRef<IFileSystem> outFileSystem, ref readonly Path rootPath, bool openCaseSensitive, bool ensureRootPathExists, Result pathNotFoundResult);

    public Result Create(ref SharedRef<IFileSystem> outFileSystem, ref readonly Path rootPath, bool openCaseSensitive)
    {
        return Create(ref outFileSystem, in rootPath, openCaseSensitive, false, Result.Success).Ret();
    }
}