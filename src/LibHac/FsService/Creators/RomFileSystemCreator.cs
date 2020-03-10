using LibHac.Fs;
using LibHac.FsSystem.RomFs;

namespace LibHac.FsService.Creators
{
    public class RomFileSystemCreator : IRomFileSystemCreator
    {
        // todo: Implement properly
        public Result Create(out IFileSystem fileSystem, IStorage romFsStorage)
        {
            fileSystem = new RomFsFileSystem(romFsStorage);
            return Result.Success;
        }
    }
}
