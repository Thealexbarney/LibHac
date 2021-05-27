using System;
using LibHac.Fs.Impl;
using LibHac.Fs.Shim;
using LibHac.FsSrv.FsCreator;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.FsSrv.Storage;
using LibHac.FsSystem;
using LibHac.Sm;

namespace LibHac.FsSrv
{
    public static class FileSystemServerInitializer
    {
        private const ulong SpeedEmulationProgramIdMinimum = 0x100000000000000;
        private const ulong SpeedEmulationProgramIdMaximum = 0x100000000001FFF;

        private const int BufferManagerHeapSize = 1024 * 1024 * 14;
        private const int BufferManagerCacheSize = 1024;
        private const int BufferManagerBlockSize = 0x4000;

        /// <summary>
        /// Initializes a <see cref="FileSystemServer"/> with the provided <see cref="FileSystemServerConfig"/>.
        /// </summary>
        /// <param name="client">The <see cref="HorizonClient"/> that <paramref name="server"/> was created with.</param>
        /// <param name="server">The <see cref="FileSystemServer"/> to initialize.</param>
        /// <param name="config">The config for initializing <paramref name="server"/>.</param>
        public static void InitializeWithConfig(HorizonClient client, FileSystemServer server, FileSystemServerConfig config)
        {
            if (config.FsCreators == null)
                throw new ArgumentException("FsCreators must not be null");

            if (config.DeviceOperator == null)
                throw new ArgumentException("DeviceOperator must not be null");

            server.SetDebugFlagEnabled(false);
            server.Storage.InitializeStorageDeviceManagerFactory(null);

            FileSystemProxyConfiguration fspConfig = InitializeFileSystemProxy(server, config);

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = server.Impl.GetFileSystemProxyServiceObject();
            ulong processId = client.Os.GetCurrentProcessId().Value;
            fsProxy.Target.SetCurrentProcess(processId).IgnoreResult();

            client.Fs.Impl.InitializeDfcFileSystemProxyServiceObject(fsProxy);

            InitializeFileSystemProxyServer(client, server);

            var saveService = new SaveDataFileSystemService(fspConfig.SaveDataFileSystemService, processId);

            saveService.CleanUpTemporaryStorage().IgnoreResult();
            saveService.CleanUpSaveData().IgnoreResult();
            saveService.CompleteSaveDataExtension().IgnoreResult();
            saveService.FixSaveData().IgnoreResult();
            saveService.RecoverMultiCommit().IgnoreResult();

            // NS usually takes care of this
            if (client.Fs.IsSdCardInserted())
                client.Fs.SetSdCardAccessibility(true);
        }

        private static FileSystemProxyConfiguration InitializeFileSystemProxy(FileSystemServer server,
            FileSystemServerConfig config)
        {
            var random = new Random();
            RandomDataGenerator randomGenerator = buffer =>
            {
                random.NextBytes(buffer);
                return Result.Success;
            };

            var bufferManager = new FileSystemBufferManager();
            Memory<byte> heapBuffer = GC.AllocateArray<byte>(BufferManagerHeapSize, true);
            bufferManager.Initialize(BufferManagerCacheSize, heapBuffer, BufferManagerBlockSize);

            var saveDataIndexerManager = new SaveDataIndexerManager(server.Hos.Fs, Fs.SaveData.SaveIndexerId,
                new ArrayPoolMemoryResource(), new SdHandleManager(), false);

            var programRegistryService = new ProgramRegistryServiceImpl(server);

            server.InitializeProgramRegistryImpl(programRegistryService);

            var baseStorageConfig = new BaseStorageServiceImpl.Configuration();
            baseStorageConfig.BisStorageCreator = config.FsCreators.BuiltInStorageCreator;
            baseStorageConfig.GameCardStorageCreator = config.FsCreators.GameCardStorageCreator;
            baseStorageConfig.FsServer = server;
            baseStorageConfig.DeviceOperator = new ReferenceCountedDisposable<IDeviceOperator>(config.DeviceOperator);
            var baseStorageService = new BaseStorageServiceImpl(in baseStorageConfig);

            var timeService = new TimeServiceImpl(server);

            var baseFsServiceConfig = new BaseFileSystemServiceImpl.Configuration();
            baseFsServiceConfig.BisFileSystemCreator = config.FsCreators.BuiltInStorageFileSystemCreator;
            baseFsServiceConfig.GameCardFileSystemCreator = config.FsCreators.GameCardFileSystemCreator;
            baseFsServiceConfig.SdCardFileSystemCreator = config.FsCreators.SdCardFileSystemCreator;
            baseFsServiceConfig.BisWiperCreator = BisWiper.CreateWiper;
            baseFsServiceConfig.FsServer = server;
            var baseFsService = new BaseFileSystemServiceImpl(in baseFsServiceConfig);

            var accessFailureManagementServiceConfig = new AccessFailureManagementServiceImpl.Configuration();
            accessFailureManagementServiceConfig.FsServer = server;

            var accessFailureManagementService =
                new AccessFailureManagementServiceImpl(in accessFailureManagementServiceConfig);

            var speedEmulationRange =
                new InternalProgramIdRangeForSpeedEmulation(SpeedEmulationProgramIdMinimum,
                    SpeedEmulationProgramIdMaximum);

            var ncaFsServiceConfig = new NcaFileSystemServiceImpl.Configuration();
            ncaFsServiceConfig.BaseFsService = baseFsService;
            ncaFsServiceConfig.LocalFsCreator = config.FsCreators.LocalFileSystemCreator;
            ncaFsServiceConfig.TargetManagerFsCreator = config.FsCreators.TargetManagerFileSystemCreator;
            ncaFsServiceConfig.PartitionFsCreator = config.FsCreators.PartitionFileSystemCreator;
            ncaFsServiceConfig.RomFsCreator = config.FsCreators.RomFileSystemCreator;
            ncaFsServiceConfig.StorageOnNcaCreator = config.FsCreators.StorageOnNcaCreator;
            ncaFsServiceConfig.SubDirectoryFsCreator = config.FsCreators.SubDirectoryFileSystemCreator;
            ncaFsServiceConfig.EncryptedFsCreator = config.FsCreators.EncryptedFileSystemCreator;
            ncaFsServiceConfig.ProgramRegistryService = programRegistryService;
            ncaFsServiceConfig.AccessFailureManagementService = accessFailureManagementService;
            ncaFsServiceConfig.SpeedEmulationRange = speedEmulationRange;
            ncaFsServiceConfig.FsServer = server;

            var ncaFsService = new NcaFileSystemServiceImpl(in ncaFsServiceConfig, config.ExternalKeySet);

            var saveFsServiceConfig = new SaveDataFileSystemServiceImpl.Configuration();
            saveFsServiceConfig.BaseFsService = baseFsService;
            saveFsServiceConfig.TimeService = timeService;
            saveFsServiceConfig.LocalFsCreator = config.FsCreators.LocalFileSystemCreator;
            saveFsServiceConfig.TargetManagerFsCreator = config.FsCreators.TargetManagerFileSystemCreator;
            saveFsServiceConfig.SaveFsCreator = config.FsCreators.SaveDataFileSystemCreator;
            saveFsServiceConfig.EncryptedFsCreator = config.FsCreators.EncryptedFileSystemCreator;
            saveFsServiceConfig.ProgramRegistryService = programRegistryService;
            saveFsServiceConfig.BufferManager = bufferManager;
            saveFsServiceConfig.GenerateRandomData = randomGenerator;
            saveFsServiceConfig.IsPseudoSaveData = () => true;
            saveFsServiceConfig.MaxSaveFsCacheCount = 1;
            saveFsServiceConfig.SaveIndexerManager = saveDataIndexerManager;
            saveFsServiceConfig.FsServer = server;

            var saveFsService = new SaveDataFileSystemServiceImpl(in saveFsServiceConfig);

            var statusReportServiceConfig = new StatusReportServiceImpl.Configuration();
            statusReportServiceConfig.NcaFsServiceImpl = ncaFsService;
            statusReportServiceConfig.SaveFsServiceImpl = saveFsService;
            statusReportServiceConfig.BufferManagerMemoryReport = null;
            statusReportServiceConfig.ExpHeapMemoryReport = null;
            statusReportServiceConfig.BufferPoolMemoryReport = null;
            statusReportServiceConfig.GetPatrolAllocateCounts = null;
            statusReportServiceConfig.MainThreadStackUsageReporter = new DummyStackUsageReporter();
            statusReportServiceConfig.IpcWorkerThreadStackUsageReporter = new DummyStackUsageReporter();
            statusReportServiceConfig.PipeLineWorkerThreadStackUsageReporter = new DummyStackUsageReporter();
            statusReportServiceConfig.FsServer = server;

            var statusReportService = new StatusReportServiceImpl(in statusReportServiceConfig);

            var accessLogServiceConfig = new AccessLogServiceImpl.Configuration();
            accessLogServiceConfig.MinimumProgramIdForSdCardLog = 0x0100000000003000;
            accessLogServiceConfig.FsServer = server;

            var accessLogService = new AccessLogServiceImpl(in accessLogServiceConfig);

            var fspConfig = new FileSystemProxyConfiguration
            {
                FsCreatorInterfaces = config.FsCreators,
                BaseStorageService = baseStorageService,
                BaseFileSystemService = baseFsService,
                NcaFileSystemService = ncaFsService,
                SaveDataFileSystemService = saveFsService,
                AccessFailureManagementService = accessFailureManagementService,
                TimeService = timeService,
                StatusReportService = statusReportService,
                ProgramRegistryService = programRegistryService,
                AccessLogService = accessLogService
            };

            server.InitializeFileSystemProxy(fspConfig);
            return fspConfig;
        }

        private static void InitializeFileSystemProxyServer(HorizonClient client, FileSystemServer server)
        {
            client.Sm.RegisterService(new FileSystemProxyService(server), "fsp-srv").IgnoreResult();
            client.Sm.RegisterService(new FileSystemProxyForLoaderService(server), "fsp-ldr").IgnoreResult();
            client.Sm.RegisterService(new ProgramRegistryService(server), "fsp-pr").IgnoreResult();
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
                serviceObject = _server.Impl.GetFileSystemProxyServiceObject();
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
                serviceObject = _server.Impl.GetFileSystemProxyForLoaderServiceObject();
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
                serviceObject = _server.Impl.GetProgramRegistryServiceObject();
                return Result.Success;
            }
        }

        private class DummyStackUsageReporter : IStackUsageReporter
        {
            public uint GetStackUsage() => 0;
        }
    }

    /// <summary>
    /// Contains the configuration for creating a new <see cref="FileSystemServer"/>.
    /// </summary>
    public class FileSystemServerConfig
    {
        /// <summary>
        /// The <see cref="FileSystemCreatorInterfaces"/> used for creating filesystems.
        /// </summary>
        public FileSystemCreatorInterfaces FsCreators { get; set; }

        /// <summary>
        /// An <see cref="IDeviceOperator"/> for managing the gamecard and SD card.
        /// </summary>
        public IDeviceOperator DeviceOperator { get; set; }

        /// <summary>
        /// A keyset containing rights IDs and title keys.
        /// If null, an empty set will be created.
        /// </summary>
        public ExternalKeySet ExternalKeySet { get; set; }
    }
}
