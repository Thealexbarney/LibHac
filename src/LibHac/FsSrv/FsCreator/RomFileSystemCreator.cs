using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Tools.FsSystem.RomFs;

namespace LibHac.FsSrv.FsCreator;

public class RomFileSystemCreator : IRomFileSystemCreator
{
    // todo: Implement properly
    public Result Create(ref SharedRef<IFileSystem> outFileSystem, ref readonly SharedRef<IStorage> romFsStorage)
    {
        outFileSystem.Reset(new RomFsFileSystem(in romFsStorage));
        return Result.Success;
    }
}