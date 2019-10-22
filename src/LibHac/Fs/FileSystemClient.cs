using System;
using LibHac.Common;
using LibHac.Fs.Accessors;
using LibHac.FsService;
using LibHac.FsSystem;

namespace LibHac.Fs
{
    public partial class FileSystemClient
    {
        private FileSystemServer FsSrv { get; }
        private IFileSystemProxy FsProxy { get; set; }

        private readonly object _fspInitLocker = new object();

        internal ITimeSpanGenerator Time { get; }
        private IAccessLog AccessLog { get; set; }

        internal MountTable MountTable { get; } = new MountTable();

        public FileSystemClient(ITimeSpanGenerator timer)
        {
            Time = timer ?? new StopWatchTimeSpanGenerator();
        }

        public FileSystemClient(FileSystemServer fsServer, ITimeSpanGenerator timer)
        {
            FsSrv = fsServer;
            Time = timer ?? new StopWatchTimeSpanGenerator();
        }

        public bool HasFileSystemServer()
        {
            return FsSrv != null;
        }

        public IFileSystemProxy GetFileSystemProxyServiceObject()
        {
            if (FsProxy != null) return FsProxy;

            lock (_fspInitLocker)
            {
                if (FsProxy != null) return FsProxy;

                if (!HasFileSystemServer())
                {
                    throw new InvalidOperationException("Client was not initialized with a server object.");
                }

                FsProxy = FsSrv.CreateFileSystemProxyService();

                return FsProxy;
            }
        }

        public Result Register(U8Span mountName, IFileSystem fileSystem)
        {
            return Register(mountName, fileSystem, null);
        }

        public Result Register(U8Span mountName, IFileSystem fileSystem, ICommonMountNameGenerator nameGenerator)
        {
            var accessor = new FileSystemAccessor(mountName.ToString(), fileSystem, this, nameGenerator);

            Result rc = MountTable.Mount(accessor);
            if (rc.IsFailure()) return rc;

            accessor.IsAccessLogEnabled = IsEnabledAccessLog();
            return Result.Success;
        }

        public void Unmount(string mountName)
        {
            Result rc;

            if (IsEnabledAccessLog() && IsEnabledFileSystemAccessorAccessLog(mountName))
            {
                TimeSpan startTime = Time.GetCurrent();

                rc = MountTable.Unmount(mountName);

                TimeSpan endTime = Time.GetCurrent();
                OutputAccessLog(rc, startTime, endTime, $", name: \"{mountName}\"");
            }
            else
            {
                rc = MountTable.Unmount(mountName);
            }

            rc.ThrowIfFailure();
        }

        internal Result FindFileSystem(ReadOnlySpan<char> path, out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
        {
            fileSystem = default;

            Result rc = GetMountName(path, out ReadOnlySpan<char> mountName, out subPath);
            if (rc.IsFailure()) return rc;

            rc = MountTable.Find(mountName.ToString(), out fileSystem);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        internal static Result GetMountName(ReadOnlySpan<char> path, out ReadOnlySpan<char> mountName, out ReadOnlySpan<char> subPath)
        {
            int mountLen = 0;
            int maxMountLen = Math.Min(path.Length, PathTools.MountNameLength);

            for (int i = 0; i <= maxMountLen; i++)
            {
                if (path[i] == PathTools.MountSeparator)
                {
                    mountLen = i;
                    break;
                }
            }

            if (mountLen == 0)
            {
                mountName = default;
                subPath = default;

                return ResultFs.InvalidMountName.Log();
            }

            mountName = path.Slice(0, mountLen);

            if (mountLen + 1 < path.Length)
            {
                subPath = path.Slice(mountLen + 1);
            }
            else
            {
                subPath = default;
            }

            return Result.Success;
        }
    }
}
