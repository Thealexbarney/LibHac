using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.FsSystem;
using LibHac.Util;
using static LibHac.Fs.CommonPaths;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

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

                var sb = new U8StringBuilder(nameBuffer);
                sb.Append(mountName).Append(StringTraits.DriveSeparator);

                Debug.Assert(sb.Length == requiredNameBufferSize - 1);

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
                System.TimeSpan startTime = fs.Time.GetCurrent();
                rc = MountBisImpl(fs, mountName, partitionId, rootPath);
                System.TimeSpan endTime = fs.Time.GetCurrent();

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
        private static Result MountBisImpl(FileSystemClient fs, U8Span mountName, BisPartitionId partitionId,
            U8Span rootPath)
        {
            Result rc = MountHelpers.CheckMountNameAcceptingReservedMountName(mountName);
            if (rc.IsFailure()) return rc;

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

            // Nintendo doesn't use the provided rootPath
            FspPath.CreateEmpty(out FspPath sfPath);

            rc = fsProxy.Target.OpenBisFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, in sfPath,
                partitionId);
            if (rc.IsFailure()) return rc;

            using (fileSystem)
            {
                var nameGenerator = new BisCommonMountNameGenerator(partitionId);
                var fileSystemAdapter = new FileSystemServiceObjectAdapter(fileSystem);

                return fs.Register(mountName, fileSystemAdapter, nameGenerator);
            }
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
            Unsafe.SkipInit(out FsPath path);

            int pathLen = StringUtils.GetLength(rootPath, PathTools.MaxPathLength + 1);
            if (pathLen > PathTools.MaxPathLength)
                return ResultFs.TooLongPath.Log();

            if (pathLen > 0)
            {
                byte endingSeparator = PathTool.IsSeparator(rootPath[pathLen - 1])
                    ? StringTraits.NullTerminator
                    : StringTraits.DirectorySeparator;

                var sb = new U8StringBuilder(path.Str);
                Result rc = sb.Append(rootPath).Append(endingSeparator).ToSfPath();
                if (rc.IsFailure()) return rc;
            }
            else
            {
                path.Str[0] = StringTraits.NullTerminator;
            }

            FspPath.FromSpan(out FspPath sfPath, path.Str);

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

            return fsProxy.Target.SetBisRootForHost(partitionId, in sfPath);
        }

        public static Result OpenBisPartition(this FileSystemClient fs, out IStorage partitionStorage,
            BisPartitionId partitionId)
        {
            partitionStorage = default;

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();
            Result rc = fsProxy.Target.OpenBisStorage(out ReferenceCountedDisposable<IStorageSf> storage, partitionId);
            if (rc.IsFailure()) return rc;

            using (storage)
            {
                var storageAdapter = new StorageServiceObjectAdapter(storage);

                partitionStorage = storageAdapter;
                return Result.Success;
            }
        }

        public static Result InvalidateBisCache(this FileSystemClient fs)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();
            return fsProxy.Target.InvalidateBisCache();
        }
    }
}
