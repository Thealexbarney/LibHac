using System;
using System.Runtime.CompilerServices;
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
        private bool AccessLogEnabled { get; set; }

        internal MountTable MountTable { get; } = new MountTable();

        public FileSystemClient(ITimeSpanGenerator timer)
        {
            Time = timer;
        }

        public FileSystemClient(FileSystemServer fsServer, ITimeSpanGenerator timer)
        {
            FsSrv = fsServer;
            Time = timer;
        }

        public IFileSystemProxy GetFileSystemProxyServiceObject()
        {
            if (FsProxy != null) return FsProxy;

            lock (_fspInitLocker)
            {
                if (FsProxy != null) return FsProxy;

                if (FsSrv == null)
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

        public void SetAccessLog(bool isEnabled, IAccessLog accessLog = null)
        {
            AccessLogEnabled = isEnabled;

            if (accessLog != null) AccessLog = accessLog;
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

            for (int i = 0; i < maxMountLen; i++)
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

                return ResultFs.InvalidMountName;
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

        internal bool IsEnabledAccessLog()
        {
            return AccessLogEnabled && AccessLog != null && Time != null;
        }

        internal bool IsEnabledFileSystemAccessorAccessLog(string mountName)
        {
            if (MountTable.Find(mountName, out FileSystemAccessor accessor).IsFailure())
            {
                return true;
            }

            return accessor.IsAccessLogEnabled;
        }

        internal bool IsEnabledHandleAccessLog(FileHandle handle)
        {
            return handle.File.Parent.IsAccessLogEnabled;
        }

        internal bool IsEnabledHandleAccessLog(DirectoryHandle handle)
        {
            return handle.Directory.Parent.IsAccessLogEnabled;
        }

        internal void OutputAccessLog(Result result, TimeSpan startTime, TimeSpan endTime, string message, [CallerMemberName] string caller = "")
        {
            AccessLog.Log(result, startTime, endTime, 0, message, caller);
        }

        internal void OutputAccessLog(Result result, TimeSpan startTime, TimeSpan endTime, FileHandle handle, string message, [CallerMemberName] string caller = "")
        {
            AccessLog.Log(result, startTime, endTime, handle.GetId(), message, caller);
        }

        internal void OutputAccessLog(Result result, TimeSpan startTime, TimeSpan endTime, DirectoryHandle handle, string message, [CallerMemberName] string caller = "")
        {
            AccessLog.Log(result, startTime, endTime, handle.GetId(), message, caller);
        }
    }
}
