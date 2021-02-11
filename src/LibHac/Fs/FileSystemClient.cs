using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Accessors;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using LibHac.FsSystem;
using LibHac.Util;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;

namespace LibHac.Fs
{
    // Functions in the nn::fssrv::detail namespace use this struct.
    public readonly struct FileSystemClientImpl
    {
        internal readonly FileSystemClient Fs;
        internal HorizonClient Hos => Fs.Hos;
        internal ref FileSystemClientGlobals Globals => ref Fs.Globals;

        internal FileSystemClientImpl(FileSystemClient parentClient) => Fs = parentClient;
    }

    internal struct FileSystemClientGlobals
    {
        public HorizonClient Hos;
        public object InitMutex;
        public FileSystemProxyServiceObjectGlobals FileSystemProxyServiceObject;
    }

    public partial class FileSystemClient
    {
        internal FileSystemClientGlobals Globals;

        internal HorizonClient Hos => Globals.Hos;

        internal ITimeSpanGenerator Time { get; }
        private IAccessLog AccessLog { get; set; }

        internal MountTable MountTable { get; } = new MountTable();

        public FileSystemClient(ITimeSpanGenerator timer)
        {
            Time = timer ?? new StopWatchTimeSpanGenerator();
        }

        public FileSystemClient(HorizonClient horizonClient)
        {
            Time = horizonClient.Time;

            Globals.Hos = horizonClient;
            Globals.InitMutex = new object();

            Assert.NotNull(Time);
        }

        public bool HasFileSystemServer()
        {
            return Hos != null;
        }

        public Result Register(U8Span mountName, IFileSystem fileSystem)
        {
            return Register(mountName, fileSystem, null);
        }

        public Result Register(U8Span mountName, IFileSystem fileSystem, ICommonMountNameGenerator nameGenerator)
        {
            return Register(mountName, null, fileSystem, nameGenerator);
        }

        public Result Register(U8Span mountName, IMultiCommitTarget multiCommitTarget, IFileSystem fileSystem,
            ICommonMountNameGenerator nameGenerator)
        {
            var accessor = new FileSystemAccessor(mountName, multiCommitTarget, fileSystem, this, nameGenerator);

            Result rc = MountTable.Mount(accessor);
            if (rc.IsFailure()) return rc;

            accessor.IsAccessLogEnabled = IsEnabledAccessLog();
            return Result.Success;
        }

        public void Unmount(U8Span mountName)
        {
            Result rc;
            string mountNameStr = mountName.ToString();

            if (IsEnabledAccessLog() && IsEnabledFileSystemAccessorAccessLog(mountName))
            {
                System.TimeSpan startTime = Time.GetCurrent();

                rc = MountTable.Unmount(mountNameStr);

                System.TimeSpan endTime = Time.GetCurrent();
                OutputAccessLog(rc, startTime, endTime, $", name: \"{mountNameStr}\"");
            }
            else
            {
                rc = MountTable.Unmount(mountNameStr);
            }

            rc.ThrowIfFailure();
        }

        internal Result FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, U8Span path)
        {
            fileSystem = default;
            subPath = default;

            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            int hostMountNameLen = StringUtils.GetLength(CommonPaths.HostRootFileSystemMountName);
            if (StringUtils.Compare(path, CommonPaths.HostRootFileSystemMountName, hostMountNameLen) == 0)
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
                StringUtils.Copy(mountName.Name, CommonPaths.HostRootFileSystemMountName);
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
