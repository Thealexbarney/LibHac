using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv;
using LibHac.FsSrv.Impl;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Tools.Fs;

namespace LibHac.Tests.Fs.FileSystemClientTests;

public class HorizonServerSet
{
    public Horizon Server { get; set; }
    public HorizonClient FsProcessClient { get; set; }
    public HorizonClient InitialProcessClient { get; set; }
    public HorizonClient Client { get; set; }
    public FileSystemServer FsServer { get; set; }
    public IFileSystem RootFileSystem { get; set; }
}

public static class FileSystemServerFactory
{
    private static HorizonServerSet CreateHorizonImpl(bool sdCardInserted = true,
        AccessControlBits.Bits fsAcBits = AccessControlBits.Bits.Debug, ProgramLocation programLocation = default)
    {
        var hos = new HorizonServerSet();
        hos.RootFileSystem = new InMemoryFileSystem();
        var keySet = new KeySet();

        hos.Server = new Horizon(new HorizonConfiguration());

        hos.FsProcessClient = hos.Server.CreatePrivilegedHorizonClient();
        hos.FsServer = new FileSystemServer(hos.FsProcessClient);

        hos.InitialProcessClient = hos.Server.CreatePrivilegedHorizonClient();

        var random = new Random(12345);
        RandomDataGenerator randomGenerator = buffer => random.NextBytes(buffer);

        var defaultObjects =
            DefaultFsServerObjects.GetDefaultEmulatedCreators(hos.RootFileSystem, keySet, hos.FsServer,
                randomGenerator);

        defaultObjects.Sdmmc.SetSdCardInserted(sdCardInserted);

        var config = new FileSystemServerConfig();
        config.FsCreators = defaultObjects.FsCreators;
        config.StorageDeviceManagerFactory = defaultObjects.StorageDeviceManagerFactory;
        config.ExternalKeySet = new ExternalKeySet();
        config.RandomGenerator = randomGenerator;

        FileSystemServerInitializer.InitializeWithConfig(hos.FsProcessClient, hos.FsServer, config);
        hos.FsServer.SetDebugFlagEnabled(true);

        if (programLocation.ProgramId == ProgramId.InvalidId)
        {
            hos.Client = hos.Server.CreateHorizonClient();
        }
        else
        {
            hos.Client = hos.Server.CreateHorizonClient(programLocation, fsAcBits);
        }

        return hos;
    }

    public static FileSystemClient CreateClient(bool sdCardInserted)
    {
        HorizonServerSet hos = CreateHorizonImpl(sdCardInserted: sdCardInserted);

        return hos.InitialProcessClient.Fs;
    }

    public static FileSystemClient CreateClient(out IFileSystem rootFs)
    {
        HorizonServerSet hos = CreateHorizonImpl(sdCardInserted: true);
        rootFs = hos.RootFileSystem;

        return hos.InitialProcessClient.Fs;
    }

    public static Horizon CreateHorizonServer()
    {
        return CreateHorizonImpl(sdCardInserted: true).Server;
    }

    public static HorizonServerSet CreateHorizon(ProgramId programId = default, bool sdCardInserted = true,
        AccessControlBits.Bits fsAcBits = AccessControlBits.Bits.Debug)
    {
        var programLocation = new ProgramLocation(programId, StorageId.BuiltInUser);
        return CreateHorizonImpl(sdCardInserted, fsAcBits, programLocation);
    }
}