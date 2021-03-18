using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem.RomFs;

namespace LibHac.FsSrv.FsCreator
{
    public class RomFileSystemCreator : IRomFileSystemCreator
    {
        // todo: Implement properly
        public Result Create(out ReferenceCountedDisposable<IFileSystem> fileSystem, ReferenceCountedDisposable<IStorage> romFsStorage)
        {
            // Todo: Properly use shared references
            fileSystem = new ReferenceCountedDisposable<IFileSystem>(new RomFsFileSystem(romFsStorage.AddReference().Target));
            return Result.Success;
        }
    }
}
