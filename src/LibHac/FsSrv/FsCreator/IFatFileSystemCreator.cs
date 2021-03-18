using LibHac.Fat;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator
{
    public interface IFatFileSystemCreator
    {
        Result Create(out ReferenceCountedDisposable<IFileSystem> fileSystem,
            ReferenceCountedDisposable<IStorage> baseStorage, FatAttribute attribute, int driveId,
            Result invalidFatFormatResult, Result usableSpaceNotEnoughResult);

        Result Format(ReferenceCountedDisposable<IStorage> partitionStorage, FatAttribute attribute,
            FatFormatParam formatParam, int driveId, Result invalidFatFormatResult, Result usableSpaceNotEnoughResult);
    }
}