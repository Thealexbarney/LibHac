using System;
using LibHac.Fs;
using LibHac.FsService.Creators;

namespace LibHac.FsService
{
    public class FileSystemServer
    {
        internal const ulong SaveIndexerId = 0x8000000000000000;

        private FileSystemProxyCore FsProxyCore { get; }

        /// <summary>The client instance to be used for internal operations like save indexer access.</summary>
        public FileSystemClient FsClient { get; }
        private ITimeSpanGenerator Timer { get; }

        internal SaveDataIndexerManager SaveDataIndexerManager { get; }

        /// <summary>
        /// Creates a new <see cref="FileSystemServer"/>.
        /// </summary>
        /// <param name="config">The configuration for the created <see cref="FileSystemServer"/>.</param>
        public FileSystemServer(FileSystemServerConfig config)
        {
            if (config.FsCreators == null)
                throw new ArgumentException("FsCreators must not be null");

            if (config.DeviceOperator == null)
                throw new ArgumentException("DeviceOperator must not be null");

            ExternalKeySet externalKeySet = config.ExternalKeySet ?? new ExternalKeySet();
            Timer = config.TimeSpanGenerator ?? new StopWatchTimeSpanGenerator();

            FsProxyCore = new FileSystemProxyCore(config.FsCreators, externalKeySet, config.DeviceOperator);
            var fsProxy = new FileSystemProxy(FsProxyCore, this);
            FsClient = new FileSystemClient(this, fsProxy, Timer);

            SaveDataIndexerManager = new SaveDataIndexerManager(FsClient, SaveIndexerId);

            fsProxy.CleanUpTemporaryStorage();
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
            return new FileSystemClient(this, timer);
        }

        public IFileSystemProxy CreateFileSystemProxyService()
        {
            return new FileSystemProxy(FsProxyCore, this);
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
