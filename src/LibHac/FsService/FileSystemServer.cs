using LibHac.FsClient;
using LibHac.FsService.Creators;

namespace LibHac.FsService
{
    public class FileSystemServer
    {
        private FileSystemProxyCore FsProxyCore { get; }

        /// <summary>The client instance to be used for internal operations like save indexer access.</summary>
        private FileSystemClient FsClient { get; }
        private ITimeSpanGenerator Timer { get; }

        /// <summary>
        /// Creates a new <see cref="FileSystemServer"/> with a new default <see cref="ITimeSpanGenerator"/>.
        /// </summary>
        /// <param name="fsCreators">The <see cref="FileSystemCreators"/> used for creating filesystems.</param>
        public FileSystemServer(FileSystemCreators fsCreators) : this(fsCreators, new StopWatchTimeSpanGenerator()) { }

        /// <summary>
        /// Creates a new <see cref="FileSystemServer"/>.
        /// </summary>
        /// <param name="fsCreators">The <see cref="FileSystemCreators"/> used for creating filesystems.</param>
        /// <param name="timer">The <see cref="ITimeSpanGenerator"/> to use for access log timestamps.</param>
        public FileSystemServer(FileSystemCreators fsCreators, ITimeSpanGenerator timer)
        {
            FsProxyCore = new FileSystemProxyCore(fsCreators);
            FsClient = new FileSystemClient(this, timer);
            Timer = timer;
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

        public FileSystemProxy CreateFileSystemProxyService()
        {
            return new FileSystemProxy(FsProxyCore, FsClient);
        }
    }
}
