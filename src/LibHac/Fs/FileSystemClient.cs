using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Accessors;
using LibHac.Fs.Fsa;
using LibHac.FsSrv.Sf;
using LibHac.FsSystem;
using LibHac.Util;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;

namespace LibHac.Fs
{
    public partial class FileSystemClient
    {
        internal HorizonClient Hos { get; }

        private IFileSystemProxy FsProxy { get; set; }
        private readonly object _fspInitLocker = new object();

        private IFileSystemProxyForLoader FsProxyForLoader { get; set; }
        private readonly object _fsplInitLocker = new object();

        private IProgramRegistry ProgramRegistry { get; set; }
        private readonly object _progRegInitLocker = new object();

        internal ITimeSpanGenerator Time { get; }
        private IAccessLog AccessLog { get; set; }

        internal MountTable MountTable { get; } = new MountTable();

        public FileSystemClient(ITimeSpanGenerator timer)
        {
            Time = timer ?? new StopWatchTimeSpanGenerator();
        }

        public FileSystemClient(HorizonClient horizonClient)
        {
            Hos = horizonClient;
            Time = horizonClient.Time;

            Assert.NotNull(Time);
        }

        public bool HasFileSystemServer()
        {
            return Hos != null;
        }

        public IFileSystemProxy GetFileSystemProxyServiceObject()
        {
            if (FsProxy != null) return FsProxy;

            lock (_fsplInitLocker)
            {
                if (FsProxy != null) return FsProxy;

                if (!HasFileSystemServer())
                {
                    throw new InvalidOperationException("Client was not initialized with a server object.");
                }

                Result rc = Hos.Sm.GetService(out IFileSystemProxy fsProxy, "fsp-srv");

                if (rc.IsFailure())
                {
                    throw new HorizonResultException(rc, "Failed to get file system proxy service object.");
                }

                fsProxy.SetCurrentProcess(Hos.Os.GetCurrentProcessId().Value).IgnoreResult();

                FsProxy = fsProxy;
                return FsProxy;
            }
        }

        public IFileSystemProxyForLoader GetFileSystemProxyForLoaderServiceObject()
        {
            if (FsProxyForLoader != null) return FsProxyForLoader;

            lock (_fspInitLocker)
            {
                if (FsProxyForLoader != null) return FsProxyForLoader;

                if (!HasFileSystemServer())
                {
                    throw new InvalidOperationException("Client was not initialized with a server object.");
                }

                Result rc = Hos.Sm.GetService(out IFileSystemProxyForLoader fsProxy, "fsp-ldr");

                if (rc.IsFailure())
                {
                    throw new HorizonResultException(rc, "Failed to get file system proxy service object.");
                }

                fsProxy.SetCurrentProcess(Hos.Os.GetCurrentProcessId().Value).IgnoreResult();

                FsProxyForLoader = fsProxy;
                return FsProxyForLoader;
            }
        }

        public IProgramRegistry GetProgramRegistryServiceObject()
        {
            if (ProgramRegistry != null) return ProgramRegistry;

            lock (_progRegInitLocker)
            {
                if (ProgramRegistry != null) return ProgramRegistry;

                if (!HasFileSystemServer())
                {
                    throw new InvalidOperationException("Client was not initialized with a server object.");
                }

                Result rc = Hos.Sm.GetService(out IProgramRegistry registry, "fsp-pr");

                if (rc.IsFailure())
                {
                    throw new HorizonResultException(rc, "Failed to get registry service object.");
                }

                ProgramRegistry = registry;
                return ProgramRegistry;
            }
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
