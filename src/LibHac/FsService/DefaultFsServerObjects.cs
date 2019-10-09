using LibHac.Fs;
using LibHac.FsService.Creators;

namespace LibHac.FsService
{
    public class DefaultFsServerObjects
    {
        public FileSystemCreators FsCreators { get; set; }
        public IDeviceOperator DeviceOperator { get; set; }
        public EmulatedGameCard GameCard { get; set; }

        public static DefaultFsServerObjects GetDefaultEmulatedCreators(IFileSystem rootFileSystem, Keyset keyset)
        {
            var creators = new FileSystemCreators();
            var gameCard = new EmulatedGameCard(keyset);

            var gcStorageCreator = new EmulatedGameCardStorageCreator(gameCard);

            creators.SubDirectoryFileSystemCreator = new SubDirectoryFileSystemCreator();
            creators.SaveDataFileSystemCreator = new SaveDataFileSystemCreator(keyset);
            creators.GameCardStorageCreator = gcStorageCreator;
            creators.GameCardFileSystemCreator = new EmulatedGameCardFsCreator(gcStorageCreator, gameCard);
            creators.EncryptedFileSystemCreator = new EncryptedFileSystemCreator(keyset);
            creators.BuiltInStorageFileSystemCreator = new EmulatedBisFileSystemCreator(rootFileSystem);
            creators.SdFileSystemCreator = new EmulatedSdFileSystemCreator(rootFileSystem);

            var deviceOperator = new EmulatedDeviceOperator(gameCard);

            return new DefaultFsServerObjects
            {
                FsCreators = creators,
                DeviceOperator = deviceOperator,
                GameCard = gameCard
            };
        }
    }
}
