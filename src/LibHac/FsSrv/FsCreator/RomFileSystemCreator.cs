using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Tools.FsSystem.RomFs;

namespace LibHac.FsSrv.FsCreator;

public class RomFileSystemCreator : IRomFileSystemCreator
{
    // todo: Implement properly
    public Result Create(ref SharedRef<IFileSystem> outFileSystem, ref SharedRef<IStorage> romFsStorage)
    {
        outFileSystem.Reset(new RomFsFileSystem(ref romFsStorage));
        return Result.Success;
    }
}