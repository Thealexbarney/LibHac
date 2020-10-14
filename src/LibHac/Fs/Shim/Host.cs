using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.FsSrv;
using LibHac.FsSystem;
using LibHac.Util;
using static LibHac.Fs.CommonMountNames;

namespace LibHac.Fs.Shim
{
    /// <summary>
    /// Contains functions for mounting file systems from a host computer.
    /// </summary>
    /// <remarks>Based on nnSdk 9.3.0</remarks>
    public static class Host
    {
        private static ReadOnlySpan<byte> HostRootFileSystemPath => new[]
            {(byte) '@', (byte) 'H', (byte) 'o', (byte) 's', (byte) 't', (byte) ':', (byte) '/'};

        private const int HostRootFileSystemPathLength = 8;

        private class HostCommonMountNameGenerator : ICommonMountNameGenerator
        {
            private FsPath _path;

            public HostCommonMountNameGenerator(U8Span path)
            {
                StringUtils.Copy(_path.Str, path);

                int pathLength = StringUtils.GetLength(_path.Str);
                if (pathLength != 0 && _path.Str[pathLength - 1] == StringTraits.DirectorySeparator)
                {
                    _path.Str[pathLength - 1] = StringTraits.NullTerminator;
                }
            }

            public Result GenerateCommonMountName(Span<byte> nameBuffer)
            {
                int requiredNameBufferSize = StringUtils.GetLength(_path.Str, FsPath.MaxLength) + HostRootFileSystemPathLength;

                if (nameBuffer.Length < requiredNameBufferSize)
                    return ResultFs.TooLongPath.Log();

                var sb = new U8StringBuilder(nameBuffer);
                sb.Append(HostRootFileSystemPath).Append(_path.Str);

                Debug.Assert(sb.Length == requiredNameBufferSize - 1);

                return Result.Success;
            }
        }

        private class HostRootCommonMountNameGenerator : ICommonMountNameGenerator
        {
            public Result GenerateCommonMountName(Span<byte> nameBuffer)
            {
                const int requiredNameBufferSize = HostRootFileSystemPathLength;

                Debug.Assert(nameBuffer.Length >= requiredNameBufferSize);

                // ReSharper disable once RedundantAssignment
                int size = StringUtils.Copy(nameBuffer, HostRootFileSystemPath);
                Debug.Assert(size == requiredNameBufferSize - 1);

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
            IFileSystem hostFileSystem = default;
            var path = new FsPath();
            path.Str[0] = 0;

            static string LogMessageGenerator() => $", name: \"{HostRootFileSystemMountName.ToString()}\"";

            Result OpenHostFs() => OpenHostFileSystemImpl(fs, out hostFileSystem, ref path, MountHostOption.None);

            Result MountHostFs() => fs.Register(HostRootFileSystemMountName, hostFileSystem,
                new HostRootCommonMountNameGenerator());

            // Open the host file system
            Result result =
                fs.RunOperationWithAccessLogOnFailure(AccessLogTarget.Application, OpenHostFs, LogMessageGenerator);
            if (result.IsFailure()) return result;

            // Mount the host file system
            result = fs.RunOperationWithAccessLog(AccessLogTarget.Application, MountHostFs, LogMessageGenerator);
            if (result.IsFailure()) return result;

            if (fs.IsEnabledAccessLog(AccessLogTarget.Application))
                fs.EnableFileSystemAccessorAccessLog(HostRootFileSystemMountName);

            return Result.Success;
        }

        /// <summary>
        /// Mounts the C:\ drive of a host Windows computer at @Host:/
        /// </summary>
        /// <param name="fs">The <see cref="FileSystemClient"/> to use.</param>
        /// <param name="option">Options for mounting the host file system.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        public static Result MountHostRoot(this FileSystemClient fs, MountHostOption option)
        {
            IFileSystem hostFileSystem = default;
            var path = new FsPath();
            path.Str[0] = 0;

            string LogMessageGenerator() =>
                $", name: \"{HostRootFileSystemMountName.ToString()}, mount_host_option: {option}\"";

            Result OpenHostFs() => OpenHostFileSystemImpl(fs, out hostFileSystem, ref path, option);

            Result MountHostFs() => fs.Register(HostRootFileSystemMountName, hostFileSystem,
                new HostRootCommonMountNameGenerator());

            // Open the host file system
            Result result =
                fs.RunOperationWithAccessLogOnFailure(AccessLogTarget.Application, OpenHostFs, LogMessageGenerator);
            if (result.IsFailure()) return result;

            // Mount the host file system
            result = fs.RunOperationWithAccessLog(AccessLogTarget.Application, MountHostFs, LogMessageGenerator);
            if (result.IsFailure()) return result;

            if (fs.IsEnabledAccessLog(AccessLogTarget.Application))
                fs.EnableFileSystemAccessorAccessLog(HostRootFileSystemMountName);

            return Result.Success;
        }

        /// <summary>
        /// Unmounts the file system at @Host:/
        /// </summary>
        /// <param name="fs">The <see cref="FileSystemClient"/> to use.</param>
        public static void UnmountHostRoot(this FileSystemClient fs)
        {
            fs.Unmount(HostRootFileSystemMountName);
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
            return MountHostImpl(fs, mountName, path, null);
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
            return MountHostImpl(fs, mountName, path, option);
        }

        /// <summary>
        /// Mounts a directory on a host Windows computer at the specified mount point.
        /// </summary>
        /// <param name="fs">The <see cref="FileSystemClient"/> to use.</param>
        /// <param name="mountName">The mount name at which the file system will be mounted.</param>
        /// <param name="path">The path on the host computer to mount. e.g. C:\Windows\System32</param>
        /// <param name="optionalOption">Options for mounting the host file system. Specifying this parameter is optional.</param>
        /// <param name="caller">The caller of this function.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        private static Result MountHostImpl(this FileSystemClient fs, U8Span mountName, U8Span path,
            MountHostOption? optionalOption, [CallerMemberName] string caller = "")
        {
            Result rc;
            ICommonMountNameGenerator nameGenerator;

            string logMessage = null;
            var option = MountHostOption.None;

            // Set the mount option if it was specified
            if (optionalOption.HasValue)
            {
                option = optionalOption.Value;
            }

            if (fs.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                if (optionalOption.HasValue)
                {
                    logMessage = $", name: \"{mountName.ToString()}\", mount_host_option: {option}";
                }
                else
                {
                    logMessage = $", name: \"{mountName.ToString()}\"";
                }

                TimeSpan startTime = fs.Time.GetCurrent();
                rc = PreMountHost(out nameGenerator, mountName, path);
                TimeSpan endTime = fs.Time.GetCurrent();

                fs.OutputAccessLogUnlessResultSuccess(rc, startTime, endTime, logMessage, caller);
            }
            else
            {
                rc = PreMountHost(out nameGenerator, mountName, path);
            }

            if (rc.IsFailure()) return rc;

            IFileSystem hostFileSystem;

            if (fs.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                TimeSpan startTime = fs.Time.GetCurrent();
                rc = OpenHostFileSystem(fs, out hostFileSystem, mountName, path, option);
                TimeSpan endTime = fs.Time.GetCurrent();

                fs.OutputAccessLogUnlessResultSuccess(rc, startTime, endTime, logMessage, caller);
            }
            else
            {
                rc = OpenHostFileSystem(fs, out hostFileSystem, mountName, path, option);
            }

            if (rc.IsFailure()) return rc;

            if (fs.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                TimeSpan startTime = fs.Time.GetCurrent();
                rc = fs.Register(mountName, hostFileSystem, nameGenerator);
                TimeSpan endTime = fs.Time.GetCurrent();

                fs.OutputAccessLog(rc, startTime, endTime, logMessage, caller);
            }
            else
            {
                rc = fs.Register(mountName, hostFileSystem, nameGenerator);
            }

            if (rc.IsFailure()) return rc;

            if (fs.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                fs.EnableFileSystemAccessorAccessLog(mountName);
            }

            return Result.Success;
        }

        /// <summary>
        /// Creates an <see cref="ICommonMountNameGenerator"/> based on the <paramref name="mountName"/> and
        /// <paramref name="path"/>, and verifies the <paramref name="mountName"/>.
        /// </summary>
        /// <param name="nameGenerator">If successful, the created <see cref="ICommonMountNameGenerator"/>.</param>
        /// <param name="mountName">The mount name at which the file system will be mounted.</param>
        /// <param name="path">The path that will be opened on the host computer. e.g. C:\Windows\System32</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        private static Result PreMountHost(out ICommonMountNameGenerator nameGenerator, U8Span mountName, U8Span path)
        {
            nameGenerator = default;

            Result rc = MountHelpers.CheckMountName(mountName);
            if (rc.IsFailure()) return rc;

            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            nameGenerator = new HostCommonMountNameGenerator(path);
            return Result.Success;
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
            fileSystem = default;

            if (mountName.IsNull())
                return ResultFs.NullptrArgument.Log();

            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            if (PathUtility.IsWindowsDrive(mountName))
                return ResultFs.InvalidMountName.Log();

            if (MountHelpers.IsReservedMountName(mountName))
                return ResultFs.InvalidMountName.Log();

            bool needsTrailingSeparator = false;
            int pathLength = StringUtils.GetLength(path, PathTools.MaxPathLength + 1);

            if (pathLength != 0 && PathTool.IsSeparator(path[pathLength - 1]))
            {
                needsTrailingSeparator = true;
                pathLength++;
            }

            if (pathLength + 1 > PathTools.MaxPathLength)
                return ResultFs.TooLongPath.Log();

            FsPath fullPath;
            unsafe { _ = &fullPath; } // workaround for CS0165

            var sb = new U8StringBuilder(fullPath.Str);
            sb.Append(StringTraits.DirectorySeparator).Append(path);

            if (needsTrailingSeparator)
            {
                sb.Append(StringTraits.DirectorySeparator);
            }

            if (sb.Overflowed)
                return ResultFs.TooLongPath.Log();

            // If the input path begins with "//", change any leading '/' characters to '\'
            if (PathTool.IsSeparator(fullPath.Str[1]) && PathTool.IsSeparator(fullPath.Str[2]))
            {
                for (int i = 1; PathTool.IsSeparator(fullPath.Str[i]); i++)
                {
                    fullPath.Str[i] = StringTraits.AltDirectorySeparator;
                }
            }

            return OpenHostFileSystemImpl(fs, out fileSystem, ref fullPath, option);
        }

        /// <summary>
        /// Opens a host file system via <see cref="IFileSystemProxy"/>.
        /// </summary>
        /// <param name="fs">The <see cref="FileSystemClient"/> to use.</param>
        /// <param name="fileSystem">If successful, the opened host file system.</param>
        /// <param name="path">The path on the host computer to open. e.g. /C:\Windows\System32/</param>
        /// <param name="option">Options for opening the host file system.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        private static Result OpenHostFileSystemImpl(FileSystemClient fs, out IFileSystem fileSystem, ref FsPath path, MountHostOption option)
        {
            fileSystem = default;

            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();
            IFileSystem hostFs;

            if (option == MountHostOption.None)
            {
                Result rc = fsProxy.OpenHostFileSystem(out hostFs, ref path);
                if (rc.IsFailure()) return rc;
            }
            else
            {
                Result rc = fsProxy.OpenHostFileSystemWithOption(out hostFs, ref path, option);
                if (rc.IsFailure()) return rc;
            }

            fileSystem = hostFs;
            return Result.Success;
        }
    }
}
