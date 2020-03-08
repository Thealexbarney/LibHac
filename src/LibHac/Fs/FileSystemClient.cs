using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
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

        internal FileSystemClient(FileSystemServer fsServer, IFileSystemProxy fsProxy, ITimeSpanGenerator timer)
        {
            FsSrv = fsServer;
            FsProxy = fsProxy;
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

        internal Result FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, U8Span path)
        {
            fileSystem = default;
            subPath = default;

            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            int hostMountNameLen = StringUtils.GetLength(CommonMountNames.HostRootFileSystemMountName);
            if (StringUtils.Compare(path, CommonMountNames.HostRootFileSystemMountName, hostMountNameLen) == 0)
            {
                return ResultFs.NotMounted.Log();
            }

            Result rc = GetMountNameAndSubPath(out MountName mountName, out subPath, path);
            if (rc.IsFailure()) return rc;

            rc = MountTable.Find(StringUtils.Utf8ZToString(mountName.Name), out fileSystem);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        internal static Result GetMountNameAndSubPath(out MountName mountName, out U8Span subPath, U8Span path)
        {
            int mountLen = 0;
            int maxMountLen = Math.Min(path.Length, PathTools.MountNameLengthMax);

            if (PathUtility.IsWindowsDrive(path) || PathUtility.IsUnc(path))
            {
                StringUtils.Copy(mountName.Name, CommonMountNames.HostRootFileSystemMountName);
                mountName.Name[PathTools.MountNameLengthMax] = StringTraits.NullTerminator;

                subPath = path;
                return Result.Success;
            }

            for (int i = 0; i <= maxMountLen; i++)
            {
                if (path[i] == PathTools.MountSeparator)
                {
                    mountLen = i;
                    break;
                }
            }

            if (mountLen == 0 || mountLen > maxMountLen)
            {
                mountName = default;
                subPath = default;

                return ResultFs.InvalidMountName.Log();
            }

            U8Span subPathTemp = path.Slice(mountLen + 1);

            if (subPathTemp.Length == 0 || !PathTool.IsAnySeparator(subPathTemp[0]))
            {
                mountName = default;
                subPath = default;

                return ResultFs.InvalidPathFormat.Log();
            }

            path.Value.Slice(0, mountLen).CopyTo(mountName.Name);
            mountName.Name[mountLen] = StringTraits.NullTerminator;
            subPath = subPathTemp;

            return Result.Success;
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    [DebuggerDisplay("{ToString()}")]
    internal struct MountName
    {
        public Span<byte> Name => SpanHelpers.AsByteSpan(ref this);

        public override string ToString() => new U8Span(Name).ToString();
    }
}
