using LibHac.Fs;
using LibHac.FsService.Creators;

namespace LibHac.FsService
{
    public class DefaultFsServerObjects
    {
        public FileSystemCreators FsCreators { get; set; }
        public IDeviceOperator DeviceOperator { get; set; }
        public EmulatedGameCard GameCard { get; set; }
        public EmulatedSdCard SdCard { get; set; }

        public static DefaultFsServerObjects GetDefaultEmulatedCreators(IFileSystem rootFileSystem, Keyset keyset)
        {
            var creators = new FileSystemCreators();
            var gameCard = new EmulatedGameCard(keyset);
            var sdCard = new EmulatedSdCard();

            var gcStorageCreator = new EmulatedGameCardStorageCreator(gameCard);

            creators.RomFileSystemCreator = new RomFileSystemCreator();
            creators.PartitionFileSystemCreator = new PartitionFileSystemCreator();
            creators.StorageOnNcaCreator = new StorageOnNcaCreator();
            creators.TargetManagerFileSystemCreator = new TargetManagerFileSystemCreator();
            creators.SubDirectoryFileSystemCreator = new SubDirectoryFileSystemCreator();
            creators.SaveDataFileSystemCreator = new SaveDataFileSystemCreator(keyset);
            creators.GameCardStorageCreator = gcStorageCreator;
            creators.GameCardFileSystemCreator = new EmulatedGameCardFsCreator(gcStorageCreator, gameCard);
            creators.EncryptedFileSystemCreator = new EncryptedFileSystemCreator(keyset);
            creators.BuiltInStorageFileSystemCreator = new EmulatedBisFileSystemCreator(rootFileSystem);
            creators.SdFileSystemCreator = new EmulatedSdFileSystemCreator(sdCard, rootFileSystem);

            var deviceOperator = new EmulatedDeviceOperator(gameCard, sdCard);

            return new DefaultFsServerObjects
            {
                FsCreators = creators,
                DeviceOperator = deviceOperator,
                GameCard = gameCard,
                SdCard = sdCard
            };
        }
    }
}
