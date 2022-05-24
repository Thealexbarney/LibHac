using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.FsSrv.FsCreator;
using LibHac.FsSrv.Storage;
using LibHac.FsSystem;
using LibHac.Gc;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;

namespace LibHac.FsSrv;

public class DefaultFsServerObjects
{
    public FileSystemCreatorInterfaces FsCreators { get; set; }
    public EmulatedGameCard GameCard { get; set; }
    public EmulatedSdCard SdCard { get; set; }
    public GameCardDummy GameCardNew { get; set; }
    public EmulatedStorageDeviceManagerFactory StorageDeviceManagerFactory { get; set; }

    public static DefaultFsServerObjects GetDefaultEmulatedCreators(IFileSystem rootFileSystem, KeySet keySet,
        FileSystemServer fsServer, RandomDataGenerator randomGenerator)
    {
        var creators = new FileSystemCreatorInterfaces();
        var gameCard = new EmulatedGameCard(keySet);
        var sdCard = new EmulatedSdCard();

        var gameCardNew = new GameCardDummy();

        var gcStorageCreator = new EmulatedGameCardStorageCreator(gameCard);

        using var sharedRootFileSystem = new SharedRef<IFileSystem>(rootFileSystem);
        using SharedRef<IFileSystem> sharedRootFileSystemCopy =
            SharedRef<IFileSystem>.CreateCopy(in sharedRootFileSystem);

        creators.RomFileSystemCreator = new RomFileSystemCreator();
        creators.PartitionFileSystemCreator = new PartitionFileSystemCreator();
        creators.StorageOnNcaCreator = new StorageOnNcaCreator(keySet);
        creators.TargetManagerFileSystemCreator = new TargetManagerFileSystemCreator();
        creators.SubDirectoryFileSystemCreator = new SubDirectoryFileSystemCreator();
        creators.SaveDataFileSystemCreator = new SaveDataFileSystemCreator(fsServer, keySet, null, randomGenerator);
        creators.GameCardStorageCreator = gcStorageCreator;
        creators.GameCardFileSystemCreator = new EmulatedGameCardFsCreator(gcStorageCreator, gameCard);
        creators.EncryptedFileSystemCreator = new EncryptedFileSystemCreator(keySet);
        creators.BuiltInStorageFileSystemCreator = new EmulatedBisFileSystemCreator(ref sharedRootFileSystem.Ref);
        creators.SdCardFileSystemCreator = new EmulatedSdCardFileSystemCreator(sdCard, ref sharedRootFileSystemCopy.Ref);

        var storageDeviceManagerFactory = new EmulatedStorageDeviceManagerFactory(fsServer, gameCardNew, hasGameCard: true);

        return new DefaultFsServerObjects
        {
            FsCreators = creators,
            GameCard = gameCard,
            SdCard = sdCard,
            GameCardNew = gameCardNew,
            StorageDeviceManagerFactory = storageDeviceManagerFactory
        };
    }
}