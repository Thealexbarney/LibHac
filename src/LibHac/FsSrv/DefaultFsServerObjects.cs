using System;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.FsSrv.FsCreator;
using LibHac.FsSrv.Storage;
using LibHac.FsSystem;
using LibHac.Gc;
using LibHac.Sdmmc;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;

namespace LibHac.FsSrv;

public class DefaultFsServerObjects
{
    public FileSystemCreatorInterfaces FsCreators { get; set; }
    public EmulatedGameCard GameCard { get; set; }
    public SdmmcApi Sdmmc { get; set; }
    public GameCardEmulated GameCardNew { get; set; }
    public EmulatedStorageDeviceManagerFactory StorageDeviceManagerFactory { get; set; }

    public static DefaultFsServerObjects GetDefaultEmulatedCreators(IFileSystem rootFileSystem, KeySet keySet,
        FileSystemServer fsServer, RandomDataGenerator randomGenerator)
    {
        var creators = new FileSystemCreatorInterfaces();
        var gameCard = new EmulatedGameCard(keySet);

        var gameCardNew = new GameCardEmulated();
        var sdmmcNew = new SdmmcApi(fsServer);

        var gcStorageCreator = new GameCardStorageCreator(fsServer);

        using var sharedRootFileSystem = new SharedRef<IFileSystem>(rootFileSystem);

        var memoryResource = new ArrayPoolMemoryResource();
        IBufferManager bufferManager = null;
        IHash256GeneratorFactorySelector ncaHashGeneratorFactorySelector = null;

        creators.RomFileSystemCreator = new RomFileSystemCreator();
        creators.PartitionFileSystemCreator = new PartitionFileSystemCreator();
        creators.StorageOnNcaCreator = new StorageOnNcaCreator(memoryResource, bufferManager, InitializeNcaReader, new NcaCompressionConfiguration(), ncaHashGeneratorFactorySelector);
        creators.TargetManagerFileSystemCreator = new TargetManagerFileSystemCreator();
        creators.SubDirectoryFileSystemCreator = new SubDirectoryFileSystemCreator();
        creators.SaveDataFileSystemCreator = new SaveDataFileSystemCreator(fsServer, null, randomGenerator);
        creators.GameCardStorageCreator = gcStorageCreator;
        creators.GameCardFileSystemCreator = new GameCardFileSystemCreator(memoryResource, gcStorageCreator, fsServer);
        creators.EncryptedFileSystemCreator = new EncryptedFileSystemCreator(keySet);
        creators.BuiltInStorageFileSystemCreator = new EmulatedBisFileSystemCreator(in sharedRootFileSystem);
        creators.SdCardFileSystemCreator = new EmulatedSdCardFileSystemCreator(sdmmcNew, in sharedRootFileSystem);

        var storageDeviceManagerFactory = new EmulatedStorageDeviceManagerFactory(fsServer, sdmmcNew, gameCardNew, hasGameCard: true);

        return new DefaultFsServerObjects
        {
            FsCreators = creators,
            GameCard = gameCard,
            Sdmmc = sdmmcNew,
            GameCardNew = gameCardNew,
            StorageDeviceManagerFactory = storageDeviceManagerFactory
        };
    }

    public static Result InitializeNcaReader(ref SharedRef<NcaReader> outReader,
        ref readonly SharedRef<IStorage> baseStorage, in NcaCompressionConfiguration compressionConfig,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector, ContentAttributes contentAttributes)
    {
        throw new NotImplementedException();
    }
}