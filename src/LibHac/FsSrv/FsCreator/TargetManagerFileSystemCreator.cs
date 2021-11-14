using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator;

public class TargetManagerFileSystemCreator : ITargetManagerFileSystemCreator
{
    public Result Create(ref SharedRef<IFileSystem> outFileSystem, in Path rootPath, bool openCaseSensitive,
        bool ensureRootPathExists, Result pathNotFoundResult)
    {
        throw new NotImplementedException();
    }

    public Result NormalizeCaseOfPath(out bool isSupported, ref Path path)
    {
        throw new NotImplementedException();
    }
}
