using LibHac.Common.Keys;
using LibHac.FsSrv.FsCreator;
using LibHac.FsSrv.Sf;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;

namespace LibHac.FsSrv
{
    public class DefaultFsServerObjects
    {
        public FileSystemCreatorInterfaces FsCreators { get; set; }
        public IDeviceOperator DeviceOperator { get; set; }
        public EmulatedGameCard GameCard { get; set; }
        public EmulatedSdCard SdCard { get; set; }

        public static DefaultFsServerObjects GetDefaultEmulatedCreators(IFileSystem rootFileSystem, KeySet keySet,
            FileSystemServer fsServer)
        {
            var creators = new FileSystemCreatorInterfaces();
            var gameCard = new EmulatedGameCard(keySet);
            var sdCard = new EmulatedSdCard();

            var gcStorageCreator = new EmulatedGameCardStorageCreator(gameCard);

            creators.RomFileSystemCreator = new RomFileSystemCreator();
            creators.PartitionFileSystemCreator = new PartitionFileSystemCreator();
            creators.StorageOnNcaCreator = new StorageOnNcaCreator(keySet);
            creators.TargetManagerFileSystemCreator = new TargetManagerFileSystemCreator();
            creators.SubDirectoryFileSystemCreator = new SubDirectoryFileSystemCreator();
            creators.SaveDataFileSystemCreator = new SaveDataFileSystemCreator(fsServer, keySet, null, null);
            creators.GameCardStorageCreator = gcStorageCreator;
            creators.GameCardFileSystemCreator = new EmulatedGameCardFsCreator(gcStorageCreator, gameCard);
            creators.EncryptedFileSystemCreator = new EncryptedFileSystemCreator(keySet);
            creators.BuiltInStorageFileSystemCreator = new EmulatedBisFileSystemCreator(rootFileSystem);
            creators.SdCardFileSystemCreator = new EmulatedSdCardFileSystemCreator(sdCard, rootFileSystem);

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
