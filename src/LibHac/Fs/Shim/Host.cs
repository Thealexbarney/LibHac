using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.FsSystem;
using LibHac.Os;
using LibHac.Util;
using static LibHac.Fs.StringTraits;
using static LibHac.Fs.Impl.AccessLogStrings;
using static LibHac.Fs.Impl.CommonMountNames;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim
{
    /// <summary>
    /// Contains functions for mounting file systems from a host computer.
    /// </summary>
    /// <remarks>Based on nnSdk 11.4.0</remarks>
    [SkipLocalsInit]
    public static class Host
    {
        private static ReadOnlySpan<byte> HostRootFileSystemPath => // "@Host:/"
            new[] { (byte)'@', (byte)'H', (byte)'o', (byte)'s', (byte)'t', (byte)':', (byte)'/' };

        private const int HostRootFileSystemPathLength = 8;

        /// <summary>
        /// Opens a host file system via <see cref="IFileSystemProxy"/>.
        /// </summary>
        /// <param name="fs">The <see cref="FileSystemClient"/> to use.</param>
        /// <param name="fileSystem">If successful, the opened host file system.</param>
        /// <param name="path">The path on the host computer to open. e.g. /C:\Windows\System32/</param>
        /// <param name="option">Options for opening the host file system.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        private static Result OpenHostFileSystemImpl(FileSystemClient fs, out IFileSystem fileSystem, in FspPath path,
            MountHostOption option)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            ReferenceCountedDisposable<IFileSystemSf> hostFs = null;
            try
            {
                if (option.Flags != MountHostOptionFlag.None)
                {
                    Result rc = fsProxy.Target.OpenHostFileSystemWithOption(out hostFs, in path, option);
                    if (rc.IsFailure()) return rc;
                }
                else
                {
                    Result rc = fsProxy.Target.OpenHostFileSystem(out hostFs, in path);
                    if (rc.IsFailure()) return rc;
                }

                fileSystem = new FileSystemServiceObjectAdapter(hostFs);
                return Result.Success;
            }
            finally
            {
                hostFs?.Dispose();
            }
        }

        private class HostCommonMountNameGenerator : ICommonMountNameGenerator
        {
            private FsPath _path;

            public HostCommonMountNameGenerator(U8Span path)
            {
                StringUtils.Copy(_path.Str, path);

                int pathLength = StringUtils.GetLength(_path.Str);
                if (pathLength != 0 && _path.Str[pathLength - 1] == DirectorySeparator)
                {
                    _path.Str[pathLength - 1] = NullTerminator;
                }
            }

            public void Dispose() { }

            public Result GenerateCommonMountName(Span<byte> nameBuffer)
            {
                int requiredNameBufferSize =
                    StringUtils.GetLength(_path.Str, FsPath.MaxLength) + HostRootFileSystemPathLength;

                if (nameBuffer.Length < requiredNameBufferSize)
                    return ResultFs.TooLongPath.Log();

                var sb = new U8StringBuilder(nameBuffer);
                sb.Append(HostRootFileSystemPath).Append(_path.Str);

                Assert.SdkEqual(sb.Length, requiredNameBufferSize - 1);

                return Result.Success;
            }
        }

        private class HostRootCommonMountNameGenerator : ICommonMountNameGenerator
        {
            public void Dispose() { }

            public Result GenerateCommonMountName(Span<byte> nameBuffer)
            {
                const int requiredNameBufferSize = HostRootFileSystemPathLength;

                Assert.SdkRequiresGreaterEqual(nameBuffer.Length, requiredNameBufferSize);

                var sb = new U8StringBuilder(nameBuffer);
                sb.Append(HostRootFileSystemPath);

                Assert.SdkEqual(sb.Length, requiredNameBufferSize - 1);

                return Result.Success;
            }
        }

        /// <summary>
        /// Verifies parameters and opens a host file system.
        /// </summary>
        /// <param name="fs">The <see cref="FileSystemClient"/> to use.</param>
        /// <param name="fileSystem">If successful, the opened host file system.</param>
        /// <param name="mountName">The mount name to be verified.</param>
        /// <param name="path">The path on the host computer to open. e.g. C:\Windows\System32</param>
        /// <param name="option">Options for opening the host file system.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        private static Result OpenHostFileSystem(FileSystemClient fs, out IFileSystem fileSystem, U8Span mountName,
            U8Span path, MountHostOption option)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            if (mountName.IsNull())
                return ResultFs.NullptrArgument.Log();

            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            if (WindowsPath.IsWindowsDrive(mountName))
                return ResultFs.InvalidMountName.Log();

            if (fs.Impl.IsUsedReservedMountName(mountName))
                return ResultFs.InvalidMountName.Log();

            bool needsTrailingSeparator = false;
            int pathLength = StringUtils.GetLength(path, PathTools.MaxPathLength + 1);

            if (pathLength != 0 && path[pathLength - 1] == DirectorySeparator)
            {
                needsTrailingSeparator = true;
                pathLength++;
            }

            if (pathLength + 1 > PathTools.MaxPathLength)
                return ResultFs.TooLongPath.Log();

            Unsafe.SkipInit(out FsPath fullPath);

            var sb = new U8StringBuilder(fullPath.Str);
            sb.Append(DirectorySeparator).Append(path);

            if (needsTrailingSeparator)
            {
                sb.Append(DirectorySeparator);
            }

            if (sb.Overflowed)
                return ResultFs.TooLongPath.Log();

            // If the input path begins with "//", change any leading '/' characters to '\'
            if (fullPath.Str[1] == DirectorySeparator && fullPath.Str[2] == DirectorySeparator)
            {
                for (int i = 1; fullPath.Str[i] == DirectorySeparator; i++)
                {
                    fullPath.Str[i] = AltDirectorySeparator;
                }
            }

            Result rc = FspPath.FromSpan(out FspPath sfPath, fullPath.Str);
            if (rc.IsFailure()) return rc;

            return OpenHostFileSystemImpl(fs, out fileSystem, in sfPath, option);
        }

        /// <summary>
        /// Creates a <see cref="HostCommonMountNameGenerator"/> based on the <paramref name="mountName"/> and
        /// <paramref name="path"/>, and verifies the <paramref name="mountName"/>.
        /// </summary>
        /// <param name="fs">The <see cref="FileSystemClient"/> to use.</param>
        /// <param name="nameGenerator">If successful, the created <see cref="ICommonMountNameGenerator"/>.</param>
        /// <param name="mountName">The mount name at which the file system will be mounted.</param>
        /// <param name="path">The path that will be opened on the host computer. e.g. C:\Windows\System32</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        private static Result PreMountHost(FileSystemClient fs, out HostCommonMountNameGenerator nameGenerator,
            U8Span mountName, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out nameGenerator);

            Result rc = fs.Impl.CheckMountName(mountName);
            if (rc.IsFailure()) return rc;

            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            nameGenerator = new HostCommonMountNameGenerator(path);
            return Result.Success;
        }

        /// <summary>
        /// Mounts a directory on a host Windows computer at the specified mount point.
        /// </summary>
        /// <param name="fs">The <see cref="FileSystemClient"/> to use.</param>
        /// <param name="mountName">The mount name at which the file system will be mounted.</param>
        /// <param name="path">The path on the host computer to mount. e.g. C:\Windows\System32</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        public static Result MountHost(this FileSystemClient fs, U8Span mountName, U8Span path)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x300];

            HostCommonMountNameGenerator mountNameGenerator;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = PreMountHost(fs, out mountNameGenerator, mountName, path);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogName).Append(mountName).Append(LogQuote)
                    .Append(LogRootPath).Append(path).Append(LogQuote);

                logBuffer = sb.Buffer;

                fs.Impl.OutputAccessLogUnlessResultSuccess(rc, start, end, null, new U8Span(logBuffer));
            }
            else
            {
                rc = PreMountHost(fs, out mountNameGenerator, mountName, path);
            }
            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            IFileSystem fileSystem;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = OpenHostFileSystem(fs, out fileSystem, mountName, path, MountHostOption.None);
                Tick end = fs.Hos.Os.GetSystemTick();

                fs.Impl.OutputAccessLogUnlessResultSuccess(rc, start, end, null, new U8Span(logBuffer));
            }
            else
            {
                rc = OpenHostFileSystem(fs, out fileSystem, mountName, path, MountHostOption.None);
            }
            // No AbortIfNeeded here
            if (rc.IsFailure()) return rc;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = fs.Register(mountName, fileSystem, mountNameGenerator);
                Tick end = fs.Hos.Os.GetSystemTick();

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(logBuffer));
            }
            else
            {
                rc = fs.Register(mountName, fileSystem, mountNameGenerator);
            }
            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
                fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

            return Result.Success;
        }

        /// <summary>
        /// Mounts a directory on a host Windows computer at the specified mount point.
        /// </summary>
        /// <param name="fs">The <see cref="FileSystemClient"/> to use.</param>
        /// <param name="mountName">The mount name at which the file system will be mounted.</param>
        /// <param name="path">The path on the host computer to mount. e.g. C:\Windows\System32</param>
        /// <param name="option">Options for mounting the host file system.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        public static Result MountHost(this FileSystemClient fs, U8Span mountName, U8Span path, MountHostOption option)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x300];

            HostCommonMountNameGenerator mountNameGenerator;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = PreMountHost(fs, out mountNameGenerator, mountName, path);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogName).Append(mountName).Append(LogQuote)
                    .Append(LogRootPath).Append(path).Append(LogQuote)
                    .Append(LogMountHostOption).Append(idString.ToString(option));

                logBuffer = sb.Buffer;

                fs.Impl.OutputAccessLogUnlessResultSuccess(rc, start, end, null, new U8Span(logBuffer));
            }
            else
            {
                rc = PreMountHost(fs, out mountNameGenerator, mountName, path);
            }
            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            IFileSystem fileSystem;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = OpenHostFileSystem(fs, out fileSystem, mountName, path, option);
                Tick end = fs.Hos.Os.GetSystemTick();

                fs.Impl.OutputAccessLogUnlessResultSuccess(rc, start, end, null, new U8Span(logBuffer));
            }
            else
            {
                rc = OpenHostFileSystem(fs, out fileSystem, mountName, path, MountHostOption.None);
            }
            // No AbortIfNeeded here
            if (rc.IsFailure()) return rc;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = fs.Register(mountName, fileSystem, mountNameGenerator);
                Tick end = fs.Hos.Os.GetSystemTick();

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(logBuffer));
            }
            else
            {
                rc = fs.Register(mountName, fileSystem, mountNameGenerator);
            }
            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
                fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

            return Result.Success;
        }

        /// <summary>
        /// Mounts the C:\ drive of a host Windows computer at @Host:/
        /// </summary>
        /// <param name="fs">The <see cref="FileSystemClient"/> to use.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        public static Result MountHostRoot(this FileSystemClient fs)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x30];

            IFileSystem fileSystem;
            FspPath.CreateEmpty(out FspPath sfPath);

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = OpenHostFileSystemImpl(fs, out fileSystem, in sfPath, MountHostOption.None);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogName).Append(HostRootFileSystemMountName).Append(LogQuote);
                logBuffer = sb.Buffer;

                fs.Impl.OutputAccessLogUnlessResultSuccess(rc, start, end, null, new U8Span(logBuffer));
            }
            else
            {
                rc = OpenHostFileSystemImpl(fs, out fileSystem, in sfPath, MountHostOption.None);
            }
            // No AbortIfNeeded here
            if (rc.IsFailure()) return rc;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = MountHostFs(fs, fileSystem);
                Tick end = fs.Hos.Os.GetSystemTick();

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(logBuffer));
            }
            else
            {
                rc = MountHostFs(fs, fileSystem);
            }
            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
                fs.Impl.EnableFileSystemAccessorAccessLog(new U8Span(HostRootFileSystemMountName));

            return Result.Success;

            static Result MountHostFs(FileSystemClient fs, IFileSystem fileSystem)
            {
                return fs.Register(new U8Span(HostRootFileSystemMountName), fileSystem,
                    new HostRootCommonMountNameGenerator());
            }
        }

        /// <summary>
        /// Mounts the C:\ drive of a host Windows computer at @Host:/
        /// </summary>
        /// <param name="fs">The <see cref="FileSystemClient"/> to use.</param>
        /// <param name="option">Options for mounting the host file system.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        public static Result MountHostRoot(this FileSystemClient fs, MountHostOption option)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x60];

            IFileSystem fileSystem;
            FspPath.CreateEmpty(out FspPath sfPath);

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = OpenHostFileSystemImpl(fs, out fileSystem, in sfPath, option);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogName).Append(HostRootFileSystemMountName).Append(LogQuote)
                    .Append(LogMountHostOption).Append(idString.ToString(option));

                logBuffer = sb.Buffer;

                fs.Impl.OutputAccessLogUnlessResultSuccess(rc, start, end, null, new U8Span(logBuffer));
            }
            else
            {
                rc = OpenHostFileSystemImpl(fs, out fileSystem, in sfPath, option);
            }
            // No AbortIfNeeded here
            if (rc.IsFailure()) return rc;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = MountHostFs(fs, fileSystem);
                Tick end = fs.Hos.Os.GetSystemTick();

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(logBuffer));
            }
            else
            {
                rc = MountHostFs(fs, fileSystem);
            }
            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
                fs.Impl.EnableFileSystemAccessorAccessLog(new U8Span(HostRootFileSystemMountName));

            return Result.Success;

            static Result MountHostFs(FileSystemClient fs, IFileSystem fileSystem)
            {
                return fs.Register(new U8Span(HostRootFileSystemMountName), fileSystem,
                    new HostRootCommonMountNameGenerator());
            }
        }

        /// <summary>
        /// Unmounts the file system at @Host:/
        /// </summary>
        /// <param name="fs">The <see cref="FileSystemClient"/> to use.</param>
        public static void UnmountHostRoot(this FileSystemClient fs)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x30];

            var mountName = new U8Span(HostRootFileSystemMountName);

            if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledFileSystemAccessorAccessLog(mountName))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = fs.Impl.Unmount(mountName);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogName).Append(mountName).Append((byte)'"');

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = fs.Impl.Unmount(mountName);
            }
            fs.Impl.LogErrorMessage(rc);
            Abort.DoAbortUnless(rc.IsSuccess());
        }
    }
}
