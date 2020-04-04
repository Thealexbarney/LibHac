using System;
using System.Diagnostics;
using LibHac.Common;
using LibHac.FsService;
using LibHac.FsSystem;
using static LibHac.Fs.CommonMountNames;

namespace LibHac.Fs.Shim
{
    public static class Bis
    {
        private class BisCommonMountNameGenerator : ICommonMountNameGenerator
        {
            private BisPartitionId PartitionId { get; }

            public BisCommonMountNameGenerator(BisPartitionId partitionId)
            {
                PartitionId = partitionId;
            }

            public Result GenerateCommonMountName(Span<byte> nameBuffer)
            {
                U8Span mountName = GetBisMountName(PartitionId);

                // Add 2 for the mount name separator and null terminator
                // ReSharper disable once RedundantAssignment
                int requiredNameBufferSize = StringUtils.GetLength(mountName, PathTools.MountNameLengthMax) + 2;

                Debug.Assert(nameBuffer.Length >= requiredNameBufferSize);

                // ReSharper disable once RedundantAssignment
                int size = new U8StringBuilder(nameBuffer).Append(mountName).Append(StringTraits.DriveSeparator).Length;
                Debug.Assert(size == requiredNameBufferSize - 1);

                return Result.Success;
            }
        }

        public static Result MountBis(this FileSystemClient fs, U8Span mountName, BisPartitionId partitionId)
        {
            return MountBis(fs, mountName, partitionId, default);
        }

        public static Result MountBis(this FileSystemClient fs, BisPartitionId partitionId, U8Span rootPath)
        {
            return MountBis(fs, GetBisMountName(partitionId), partitionId, rootPath);
        }

        // nn::fs::detail::MountBis
        private static Result MountBis(FileSystemClient fs, U8Span mountName, BisPartitionId partitionId, U8Span rootPath)
        {
            Result rc;

            if (fs.IsEnabledAccessLog(AccessLogTarget.System))
            {
                TimeSpan startTime = fs.Time.GetCurrent();
                rc = MountBisImpl(fs, mountName, partitionId, rootPath);
                TimeSpan endTime = fs.Time.GetCurrent();

                string logMessage = $", name: \"{mountName.ToString()}\", bispartitionid: {partitionId}, path: \"{rootPath.ToString()}\"";

                fs.OutputAccessLog(rc, startTime, endTime, logMessage);
            }
            else
            {
                rc = MountBisImpl(fs, mountName, partitionId, rootPath);
            }

            if (rc.IsFailure()) return rc;

            if (fs.IsEnabledAccessLog(AccessLogTarget.System))
            {
                fs.EnableFileSystemAccessorAccessLog(mountName);
            }

            return Result.Success;
        }

        // ReSharper disable once UnusedParameter.Local
        private static Result MountBisImpl(FileSystemClient fs, U8Span mountName, BisPartitionId partitionId, U8Span rootPath)
        {
            Result rc = MountHelpers.CheckMountNameAcceptingReservedMountName(mountName);
            if (rc.IsFailure()) return rc;

            FsPath sfPath;
            unsafe { _ = &sfPath; } // workaround for CS0165

            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            // Nintendo doesn't use the provided rootPath
            sfPath.Str[0] = 0;

            rc = fsProxy.OpenBisFileSystem(out IFileSystem fileSystem, ref sfPath, partitionId);
            if (rc.IsFailure()) return rc;

            var nameGenerator = new BisCommonMountNameGenerator(partitionId);

            return fs.Register(mountName, fileSystem, nameGenerator);
        }

        public static U8Span GetBisMountName(BisPartitionId partitionId)
        {
            switch (partitionId)
            {
                case BisPartitionId.BootPartition1Root:
                case BisPartitionId.BootPartition2Root:
                case BisPartitionId.UserDataRoot:
                case BisPartitionId.BootConfigAndPackage2Part1:
                case BisPartitionId.BootConfigAndPackage2Part2:
                case BisPartitionId.BootConfigAndPackage2Part3:
                case BisPartitionId.BootConfigAndPackage2Part4:
                case BisPartitionId.BootConfigAndPackage2Part5:
                case BisPartitionId.BootConfigAndPackage2Part6:
                case BisPartitionId.CalibrationBinary:
                    throw new HorizonResultException(default, "The partition specified is not mountable.");

                case BisPartitionId.CalibrationFile:
                    return BisCalibrationFilePartitionMountName;
                case BisPartitionId.SafeMode:
                    return BisSafeModePartitionMountName;
                case BisPartitionId.User:
                    return BisUserPartitionMountName;
                case BisPartitionId.System:
                    return BisSystemPartitionMountName;

                default:
                    throw new ArgumentOutOfRangeException(nameof(partitionId), partitionId, null);
            }
        }

        // todo: Decide how to handle SetBisRootForHost since it allows mounting any directory on the user's computer
        public static Result SetBisRootForHost(this FileSystemClient fs, BisPartitionId partitionId, U8Span rootPath)
        {
            FsPath sfPath;
            unsafe { _ = &sfPath; } // workaround for CS0165

            int pathLen = StringUtils.GetLength(rootPath, PathTools.MaxPathLength + 1);
            if (pathLen > PathTools.MaxPathLength)
                return ResultFs.TooLongPath.Log();

            if (pathLen > 0)
            {
                byte endingSeparator = PathTool.IsSeparator(rootPath[pathLen - 1])
                    ? StringTraits.NullTerminator
                    : StringTraits.DirectorySeparator;

                Result rc = new U8StringBuilder(sfPath.Str).Append(rootPath).Append(endingSeparator).ToSfPath();
                if (rc.IsFailure()) return rc;
            }
            else
            {
                sfPath.Str[0] = StringTraits.NullTerminator;
            }

            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            return fsProxy.SetBisRootForHost(partitionId, ref sfPath);
        }

        public static Result OpenBisPartition(this FileSystemClient fs, out IStorage partitionStorage, BisPartitionId partitionId)
        {
            partitionStorage = default;

            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();
            Result rc = fsProxy.OpenBisStorage(out IStorage storage, partitionId);
            if (rc.IsFailure()) return rc;

            partitionStorage = storage;
            return Result.Success;
        }

        public static Result InvalidateBisCache(this FileSystemClient fs)
        {
            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();
            return fsProxy.InvalidateBisCache();
        }
    }
}
