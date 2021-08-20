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

        private const int HostRootFileSystemPathLength = 7;

        /// <summary>
        /// Opens a host file system via <see cref="IFileSystemProxy"/>.
        /// </summary>
        /// <param name="fs">The <see cref="FileSystemClient"/> to use.</param>
        /// <param name="outFileSystem">If successful, the opened host file system.</param>
        /// <param name="path">The path on the host computer to open. e.g. /C:\Windows\System32/</param>
        /// <param name="option">Options for opening the host file system.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        private static Result OpenHostFileSystemImpl(FileSystemClient fs, ref UniqueRef<IFileSystem> outFileSystem,
            in FspPath path, MountHostOption option)
        {
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
            using var fileSystem = new SharedRef<IFileSystemSf>();

            if (option.Flags != MountHostOptionFlag.None)
            {
                Result rc = fileSystemProxy.Get.OpenHostFileSystemWithOption(ref fileSystem.Ref(), in path, option);
                if (rc.IsFailure()) return rc;
            }
            else
            {
                Result rc = fileSystemProxy.Get.OpenHostFileSystem(ref fileSystem.Ref(), in path);
                if (rc.IsFailure()) return rc;
            }

            using var fileSystemAdapter =
                new UniqueRef<IFileSystem>(new FileSystemServiceObjectAdapter(ref fileSystem.Ref()));

            if (!fileSystemAdapter.HasValue)
                return ResultFs.AllocationMemoryFailedInHostA.Log();

            outFileSystem.Set(ref fileSystemAdapter.Ref());
            return Result.Success;
        }

        private class HostCommonMountNameGenerator : ICommonMountNameGenerator
        {
            private FsPath _path;

            public HostCommonMountNameGenerator(U8Span path)
            {
                StringUtils.Strlcpy(_path.Str, path, FsPath.MaxLength + 1);
            }

            public void Dispose() { }

            public Result GenerateCommonMountName(Span<byte> nameBuffer)
            {
                int requiredNameBufferSize =
                    StringUtils.GetLength(_path.Str, FsPath.MaxLength + 1) + HostRootFileSystemPathLength;

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
        /// <param name="outFileSystem">If successful, the opened host file system.</param>
        /// <param name="mountName">The mount name to be verified.</param>
        /// <param name="path">The path on the host computer to open. e.g. C:\Windows\System32</param>
        /// <param name="option">Options for opening the host file system.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        private static Result OpenHostFileSystem(FileSystemClient fs, ref UniqueRef<IFileSystem> outFileSystem,
            U8Span mountName, U8Span path, MountHostOption option)
        {
            if (mountName.IsNull())
                return ResultFs.NullptrArgument.Log();

            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            if (WindowsPath.IsWindowsDrive(mountName))
                return ResultFs.InvalidMountName.Log();

            if (fs.Impl.IsUsedReservedMountName(mountName))
                return ResultFs.InvalidMountName.Log();

            Result rc = PathUtility.ConvertToFspPath(out FspPath sfPath, path);
            if (rc.IsFailure()) return rc.Miss();

            if (sfPath.Str[0] == NullTerminator)
            {
                SpanHelpers.AsByteSpan(ref sfPath)[0] = Dot;
                SpanHelpers.AsByteSpan(ref sfPath)[1] = NullTerminator;
            }

            using var fileSystem = new UniqueRef<IFileSystem>();

            rc = OpenHostFileSystemImpl(fs, ref fileSystem.Ref(), in sfPath, option);
            if (rc.IsFailure()) return rc.Miss();

            outFileSystem.Set(ref fileSystem.Ref());
            return Result.Success;
        }

        /// <summary>
        /// Creates a <see cref="HostCommonMountNameGenerator"/> based on the <paramref name="mountName"/> and
        /// <paramref name="path"/>, and verifies the <paramref name="mountName"/>.
        /// </summary>
        /// <param name="fs">The <see cref="FileSystemClient"/> to use.</param>
        /// <param name="outMountNameGenerator">If successful, the created <see cref="ICommonMountNameGenerator"/>.</param>
        /// <param name="mountName">The mount name at which the file system will be mounted.</param>
        /// <param name="path">The path that will be opened on the host computer. e.g. C:\Windows\System32</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        private static Result PreMountHost(FileSystemClient fs,
            ref UniqueRef<HostCommonMountNameGenerator> outMountNameGenerator, U8Span mountName, U8Span path)
        {
            Result rc = fs.Impl.CheckMountName(mountName);
            if (rc.IsFailure()) return rc;

            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            outMountNameGenerator.Reset(new HostCommonMountNameGenerator(path));

            if (!outMountNameGenerator.HasValue)
                return ResultFs.AllocationMemoryFailedInHostB.Log();

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

            using var mountNameGenerator = new UniqueRef<HostCommonMountNameGenerator>();

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = PreMountHost(fs, ref mountNameGenerator.Ref(), mountName, path);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogName).Append(mountName).Append(LogQuote)
                    .Append(LogRootPath).Append(path).Append(LogQuote);

                logBuffer = sb.Buffer;

                fs.Impl.OutputAccessLogUnlessResultSuccess(rc, start, end, null, new U8Span(logBuffer));
            }
            else
            {
                rc = PreMountHost(fs, ref mountNameGenerator.Ref(), mountName, path);
            }

            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            using var fileSystem = new UniqueRef<IFileSystem>();

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = OpenHostFileSystem(fs, ref fileSystem.Ref(), mountName, path, MountHostOption.None);
                Tick end = fs.Hos.Os.GetSystemTick();

                fs.Impl.OutputAccessLogUnlessResultSuccess(rc, start, end, null, new U8Span(logBuffer));
            }
            else
            {
                rc = OpenHostFileSystem(fs, ref fileSystem.Ref(), mountName, path, MountHostOption.None);
            }

            // No AbortIfNeeded here
            if (rc.IsFailure()) return rc;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = PostMount(fs, mountName, ref fileSystem.Ref(), ref mountNameGenerator.Ref());
                Tick end = fs.Hos.Os.GetSystemTick();

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(logBuffer));
            }
            else
            {
                rc = PostMount(fs, mountName, ref fileSystem.Ref(), ref mountNameGenerator.Ref());
            }

            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
                fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

            return Result.Success;

            static Result PostMount(FileSystemClient fs, U8Span mountName, ref UniqueRef<IFileSystem> fileSystem,
                ref UniqueRef<HostCommonMountNameGenerator> mountNameGenerator)
            {
                using UniqueRef<ICommonMountNameGenerator> baseMountNameGenerator =
                    UniqueRef<ICommonMountNameGenerator>.Create(ref mountNameGenerator);

                Result rc = fs.Register(mountName, ref fileSystem, ref baseMountNameGenerator.Ref());
                if (rc.IsFailure()) return rc.Miss();

                return Result.Success;
            }
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

            using var mountNameGenerator = new UniqueRef<HostCommonMountNameGenerator>();

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = PreMountHost(fs, ref mountNameGenerator.Ref(), mountName, path);
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
                rc = PreMountHost(fs, ref mountNameGenerator.Ref(), mountName, path);
            }

            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            using var fileSystem = new UniqueRef<IFileSystem>();

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = OpenHostFileSystem(fs, ref fileSystem.Ref(), mountName, path, option);
                Tick end = fs.Hos.Os.GetSystemTick();

                fs.Impl.OutputAccessLogUnlessResultSuccess(rc, start, end, null, new U8Span(logBuffer));
            }
            else
            {
                rc = OpenHostFileSystem(fs, ref fileSystem.Ref(), mountName, path, option);
            }

            // No AbortIfNeeded here
            if (rc.IsFailure()) return rc;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = PostMount(fs, mountName, ref fileSystem.Ref(), ref mountNameGenerator.Ref());
                Tick end = fs.Hos.Os.GetSystemTick();

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(logBuffer));
            }
            else
            {
                rc = PostMount(fs, mountName, ref fileSystem.Ref(), ref mountNameGenerator.Ref());
            }

            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
                fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

            return Result.Success;

            static Result PostMount(FileSystemClient fs, U8Span mountName, ref UniqueRef<IFileSystem> fileSystem,
                ref UniqueRef<HostCommonMountNameGenerator> mountNameGenerator)
            {
                using UniqueRef<ICommonMountNameGenerator> baseMountNameGenerator =
                    UniqueRef<ICommonMountNameGenerator>.Create(ref mountNameGenerator);

                Result rc = fs.Register(mountName, ref fileSystem, ref baseMountNameGenerator.Ref());
                if (rc.IsFailure()) return rc.Miss();

                return Result.Success;
            }
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

            using var fileSystem = new UniqueRef<IFileSystem>();
            FspPath.CreateEmpty(out FspPath sfPath);

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = OpenHostFileSystemImpl(fs, ref fileSystem.Ref(), in sfPath, MountHostOption.None);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogName).Append(HostRootFileSystemMountName).Append(LogQuote);
                logBuffer = sb.Buffer;

                fs.Impl.OutputAccessLogUnlessResultSuccess(rc, start, end, null, new U8Span(logBuffer));
            }
            else
            {
                rc = OpenHostFileSystemImpl(fs, ref fileSystem.Ref(), in sfPath, MountHostOption.None);
            }

            // No AbortIfNeeded here
            if (rc.IsFailure()) return rc;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = PostMount(fs, ref fileSystem.Ref());
                Tick end = fs.Hos.Os.GetSystemTick();

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(logBuffer));
            }
            else
            {
                rc = PostMount(fs, ref fileSystem.Ref());
            }

            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
                fs.Impl.EnableFileSystemAccessorAccessLog(new U8Span(HostRootFileSystemMountName));

            return Result.Success;

            static Result PostMount(FileSystemClient fs, ref UniqueRef<IFileSystem> fileSystem)
            {
                using var mountNameGenerator =
                    new UniqueRef<ICommonMountNameGenerator>(new HostRootCommonMountNameGenerator());

                if (!mountNameGenerator.HasValue)
                    return ResultFs.AllocationMemoryFailedInHostC.Log();

                Result rc = fs.Register(new U8Span(HostRootFileSystemMountName), ref fileSystem,
                    ref mountNameGenerator.Ref());
                if (rc.IsFailure()) return rc.Miss();

                return Result.Success;
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

            using var fileSystem = new UniqueRef<IFileSystem>();
            FspPath.CreateEmpty(out FspPath sfPath);

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = OpenHostFileSystemImpl(fs, ref fileSystem.Ref(), in sfPath, option);
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
                rc = OpenHostFileSystemImpl(fs, ref fileSystem.Ref(), in sfPath, option);
            }

            // No AbortIfNeeded here
            if (rc.IsFailure()) return rc;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = PostMount(fs, ref fileSystem.Ref());
                Tick end = fs.Hos.Os.GetSystemTick();

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(logBuffer));
            }
            else
            {
                rc = PostMount(fs, ref fileSystem.Ref());
            }

            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
                fs.Impl.EnableFileSystemAccessorAccessLog(new U8Span(HostRootFileSystemMountName));

            return Result.Success;

            static Result PostMount(FileSystemClient fs, ref UniqueRef<IFileSystem> fileSystem)
            {
                using var mountNameGenerator =
                    new UniqueRef<ICommonMountNameGenerator>(new HostRootCommonMountNameGenerator());

                if (!mountNameGenerator.HasValue)
                    return ResultFs.AllocationMemoryFailedInHostC.Log();

                Result rc = fs.Register(new U8Span(HostRootFileSystemMountName), ref fileSystem,
                    ref mountNameGenerator.Ref());
                if (rc.IsFailure()) return rc.Miss();

                return Result.Success;
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

            fs.Impl.LogResultErrorMessage(rc);
            Abort.DoAbortUnless(rc.IsSuccess());
        }
    }
}
