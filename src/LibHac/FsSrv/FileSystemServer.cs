using System;
using LibHac.Fs;
using LibHac.Fs.Impl;
using LibHac.Fs.Shim;
using LibHac.FsSrv.Creators;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Sm;

namespace LibHac.FsSrv
{
    public class FileSystemServer
    {
        internal const ulong SaveIndexerId = 0x8000000000000000;

        private const ulong SpeedEmulationProgramIdMinimum = 0x100000000000000;
        private const ulong SpeedEmulationProgramIdMaximum = 0x100000000001FFF;

        private FileSystemProxyCoreImpl FsProxyCore { get; }

        /// <summary>The client instance to be used for internal operations like save indexer access.</summary>
        public HorizonClient Hos { get; }

        public bool IsDebugMode { get; }
        private ITimeSpanGenerator Timer { get; }

        /// <summary>
        /// Creates a new <see cref="FileSystemServer"/> and registers its services using the provided HOS client.
        /// </summary>
        /// <param name="horizonClient">The <see cref="HorizonClient"/> that will be used by this server.</param>
        /// <param name="config">The configuration for the created <see cref="FileSystemServer"/>.</param>
        public FileSystemServer(HorizonClient horizonClient, FileSystemServerConfig config)
        {
            if (config.FsCreators == null)
                throw new ArgumentException("FsCreators must not be null");

            if (config.DeviceOperator == null)
                throw new ArgumentException("DeviceOperator must not be null");

            Hos = horizonClient;

            IsDebugMode = false;

            Timer = config.TimeSpanGenerator ?? new StopWatchTimeSpanGenerator();

            FileSystemProxyConfiguration fspConfig = InitializeFileSystemProxyConfiguration(config);

            FsProxyCore = new FileSystemProxyCoreImpl(fspConfig);

            FileSystemProxyImpl fsProxy = GetFileSystemProxyServiceObject();
            ulong processId = Hos.Os.GetCurrentProcessId().Value;
            fsProxy.SetCurrentProcess(processId).IgnoreResult();

            var saveService = new SaveDataFileSystemService(fspConfig.SaveDataFileSystemService, processId);

            saveService.CleanUpTemporaryStorage().IgnoreResult();
            saveService.CleanUpSaveData().IgnoreResult();
            saveService.CompleteSaveDataExtension().IgnoreResult();
            saveService.FixSaveData().IgnoreResult();
            saveService.RecoverMultiCommit().IgnoreResult();

            Hos.Sm.RegisterService(new FileSystemProxyService(this), "fsp-srv").IgnoreResult();
            Hos.Sm.RegisterService(new FileSystemProxyForLoaderService(this), "fsp-ldr").IgnoreResult();
            Hos.Sm.RegisterService(new ProgramRegistryService(this), "fsp-pr").IgnoreResult();

            // NS usually takes care of this
            if (Hos.Fs.IsSdCardInserted())
                Hos.Fs.SetSdCardAccessibility(true);
        }

        /// <summary>
        /// Creates a new <see cref="FileSystemClient"/> using this <see cref="FileSystemServer"/>'s
        /// <see cref="ITimeSpanGenerator"/> for the client's access log.
        /// </summary>
        /// <returns>The created <see cref="FileSystemClient"/>.</returns>
        public FileSystemClient CreateFileSystemClient() => CreateFileSystemClient(Timer);

        /// <summary>
        /// Creates a new <see cref="FileSystemClient"/>.
        /// </summary>
        /// <param name="timer">The <see cref="ITimeSpanGenerator"/> to use for the created
        /// <see cref="FileSystemClient"/>'s access log.</param>
        /// <returns>The created <see cref="FileSystemClient"/>.</returns>
        public FileSystemClient CreateFileSystemClient(ITimeSpanGenerator timer)
        {
            return new FileSystemClient(Hos);
        }

        private FileSystemProxyConfiguration InitializeFileSystemProxyConfiguration(FileSystemServerConfig config)
        {
            var saveDataIndexerManager = new SaveDataIndexerManager(Hos.Fs, SaveIndexerId,
                new ArrayPoolMemoryResource(), new SdHandleManager(), false);

            var programRegistryService = new ProgramRegistryServiceImpl(this);
            var programRegistry = new ProgramRegistryImpl(programRegistryService);

            var baseStorageConfig = new BaseStorageServiceImpl.Configuration();
            baseStorageConfig.BisStorageCreator = config.FsCreators.BuiltInStorageCreator;
            baseStorageConfig.GameCardStorageCreator = config.FsCreators.GameCardStorageCreator;
            baseStorageConfig.ProgramRegistry = programRegistry;
            baseStorageConfig.DeviceOperator = new ReferenceCountedDisposable<IDeviceOperator>(config.DeviceOperator);
            var baseStorageService = new BaseStorageServiceImpl(in baseStorageConfig);

            var timeServiceConfig = new TimeServiceImpl.Configuration();
            timeServiceConfig.HorizonClient = Hos;
            timeServiceConfig.ProgramRegistry = programRegistry;
            var timeService = new TimeServiceImpl(in timeServiceConfig);

            var baseFsServiceConfig = new BaseFileSystemServiceImpl.Configuration();
            baseFsServiceConfig.BisFileSystemCreator = config.FsCreators.BuiltInStorageFileSystemCreator;
            baseFsServiceConfig.GameCardFileSystemCreator = config.FsCreators.GameCardFileSystemCreator;
            baseFsServiceConfig.SdCardFileSystemCreator = config.FsCreators.SdCardFileSystemCreator;
            baseFsServiceConfig.BisWiperCreator = BisWiper.CreateWiper;
            baseFsServiceConfig.ProgramRegistry = programRegistry;
            var baseFsService = new BaseFileSystemServiceImpl(in baseFsServiceConfig);

            var ncaFsServiceConfig = new NcaFileSystemServiceImpl.Configuration();
            ncaFsServiceConfig.BaseFsService = baseFsService;
            ncaFsServiceConfig.HostFsCreator = config.FsCreators.HostFileSystemCreator;
            ncaFsServiceConfig.TargetManagerFsCreator = config.FsCreators.TargetManagerFileSystemCreator;
            ncaFsServiceConfig.PartitionFsCreator = config.FsCreators.PartitionFileSystemCreator;
            ncaFsServiceConfig.RomFsCreator = config.FsCreators.RomFileSystemCreator;
            ncaFsServiceConfig.StorageOnNcaCreator = config.FsCreators.StorageOnNcaCreator;
            ncaFsServiceConfig.SubDirectoryFsCreator = config.FsCreators.SubDirectoryFileSystemCreator;
            ncaFsServiceConfig.EncryptedFsCreator = config.FsCreators.EncryptedFileSystemCreator;
            ncaFsServiceConfig.ProgramRegistryService = programRegistryService;
            ncaFsServiceConfig.HorizonClient = Hos;
            ncaFsServiceConfig.ProgramRegistry = programRegistry;
            ncaFsServiceConfig.SpeedEmulationRange =
                new InternalProgramIdRangeForSpeedEmulation(SpeedEmulationProgramIdMinimum,
                    SpeedEmulationProgramIdMaximum);

            var ncaFsService = new NcaFileSystemServiceImpl(in ncaFsServiceConfig, config.ExternalKeySet);

            var saveFsServiceConfig = new SaveDataFileSystemServiceImpl.Configuration();
            saveFsServiceConfig.BaseFsService = baseFsService;
            saveFsServiceConfig.HostFsCreator = config.FsCreators.HostFileSystemCreator;
            saveFsServiceConfig.TargetManagerFsCreator = config.FsCreators.TargetManagerFileSystemCreator;
            saveFsServiceConfig.SaveFsCreator = config.FsCreators.SaveDataFileSystemCreator;
            saveFsServiceConfig.EncryptedFsCreator = config.FsCreators.EncryptedFileSystemCreator;
            saveFsServiceConfig.ProgramRegistryService = programRegistryService;
            saveFsServiceConfig.ShouldCreateDirectorySaveData = () => true;
            saveFsServiceConfig.SaveIndexerManager = saveDataIndexerManager;
            saveFsServiceConfig.HorizonClient = Hos;
            saveFsServiceConfig.ProgramRegistry = programRegistry;

            var saveFsService = new SaveDataFileSystemServiceImpl(in saveFsServiceConfig);

            var accessLogServiceConfig = new AccessLogServiceImpl.Configuration();
            accessLogServiceConfig.MinimumProgramIdForSdCardLog = 0x0100000000003000;
            accessLogServiceConfig.HorizonClient = Hos;
            accessLogServiceConfig.ProgramRegistry = programRegistry;
            var accessLogService = new AccessLogServiceImpl(in accessLogServiceConfig);

            var fspConfig = new FileSystemProxyConfiguration
            {
                FsCreatorInterfaces = config.FsCreators,
                BaseStorageService = baseStorageService,
                BaseFileSystemService = baseFsService,
                NcaFileSystemService = ncaFsService,
                SaveDataFileSystemService = saveFsService,
                TimeService = timeService,
                ProgramRegistryService = programRegistryService,
                AccessLogService = accessLogService
            };

            return fspConfig;
        }

        private FileSystemProxyImpl GetFileSystemProxyServiceObject()
        {
            return new FileSystemProxyImpl(FsProxyCore);
        }

        private FileSystemProxyImpl GetFileSystemProxyForLoaderServiceObject()
        {
            return new FileSystemProxyImpl(FsProxyCore);
        }

        private ProgramRegistryImpl GetProgramRegistryServiceObject()
        {
            return new ProgramRegistryImpl(FsProxyCore.Config.ProgramRegistryService);
        }

        private class FileSystemProxyService : IServiceObject
        {
            private readonly FileSystemServer _server;

            public FileSystemProxyService(FileSystemServer server)
            {
                _server = server;
            }

            public Result GetServiceObject(out object serviceObject)
            {
                serviceObject = _server.GetFileSystemProxyServiceObject();
                return Result.Success;
            }
        }

        private class FileSystemProxyForLoaderService : IServiceObject
        {
            private readonly FileSystemServer _server;

            public FileSystemProxyForLoaderService(FileSystemServer server)
            {
                _server = server;
            }

            public Result GetServiceObject(out object serviceObject)
            {
                serviceObject = _server.GetFileSystemProxyForLoaderServiceObject();
                return Result.Success;
            }
        }

        private class ProgramRegistryService : IServiceObject
        {
            private readonly FileSystemServer _server;

            public ProgramRegistryService(FileSystemServer server)
            {
                _server = server;
            }

            public Result GetServiceObject(out object serviceObject)
            {
                serviceObject = _server.GetProgramRegistryServiceObject();
                return Result.Success;
            }
        }
    }

    /// <summary>
    /// Contains the configuration for creating a new <see cref="FileSystemServer"/>.
    /// </summary>
    public class FileSystemServerConfig
    {
        /// <summary>
        /// The <see cref="FileSystemCreators"/> used for creating filesystems.
        /// </summary>
        public FileSystemCreators FsCreators { get; set; }

        /// <summary>
        /// An <see cref="IDeviceOperator"/> for managing the gamecard and SD card.
        /// </summary>
        public IDeviceOperator DeviceOperator { get; set; }

        /// <summary>
        /// A keyset containing rights IDs and title keys.
        /// If null, an empty set will be created.
        /// </summary>
        public ExternalKeySet ExternalKeySet { get; set; }

        /// <summary>
        /// Used for generating access log timestamps.
        /// If null, a new <see cref="StopWatchTimeSpanGenerator"/> will be created.
        /// </summary>
        public ITimeSpanGenerator TimeSpanGenerator { get; set; }
    }
}
