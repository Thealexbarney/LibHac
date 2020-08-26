using System;
using LibHac.Fs;
using LibHac.Fs.Impl;
using LibHac.Fs.Shim;
using LibHac.FsSrv.Creators;
using LibHac.Sm;

namespace LibHac.FsSrv
{
    public class FileSystemServer
    {
        internal const ulong SaveIndexerId = 0x8000000000000000;

        private FileSystemProxyCore FsProxyCore { get; }

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

            ExternalKeySet externalKeySet = config.ExternalKeySet ?? new ExternalKeySet();
            Timer = config.TimeSpanGenerator ?? new StopWatchTimeSpanGenerator();

            var fspConfig = new FileSystemProxyConfiguration
            {
                FsCreatorInterfaces = config.FsCreators,
                ProgramRegistryServiceImpl = new ProgramRegistryServiceImpl(this)
            };

            FsProxyCore = new FileSystemProxyCore(fspConfig, externalKeySet, config.DeviceOperator);

            FsProxyCore.SetSaveDataIndexerManager(new SaveDataIndexerManager(Hos.Fs, SaveIndexerId,
                new ArrayPoolMemoryResource(), new SdHandleManager(), false));

            FileSystemProxy fsProxy = GetFileSystemProxyServiceObject();
            fsProxy.SetCurrentProcess(Hos.Os.GetCurrentProcessId().Value).IgnoreResult();
            fsProxy.CleanUpTemporaryStorage().IgnoreResult();

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

        private FileSystemProxy GetFileSystemProxyServiceObject()
        {
            return new FileSystemProxy(Hos, FsProxyCore);
        }

        private FileSystemProxy GetFileSystemProxyForLoaderServiceObject()
        {
            return new FileSystemProxy(Hos, FsProxyCore);
        }

        private ProgramRegistryImpl GetProgramRegistryServiceObject()
        {
            return new ProgramRegistryImpl(FsProxyCore.Config.ProgramRegistryServiceImpl);
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
