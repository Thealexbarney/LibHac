using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.FsSystem;
using LibHac.Os;
using LibHac.Util;
using static LibHac.Fs.Impl.CommonMountNames;
using static LibHac.Fs.Impl.AccessLogStrings;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.Fs.Shim
{
    [SkipLocalsInit]
    public static class Bis
    {
        private class BisCommonMountNameGenerator : ICommonMountNameGenerator
        {
            private BisPartitionId PartitionId { get; }

            public BisCommonMountNameGenerator(BisPartitionId partitionId)
            {
                PartitionId = partitionId;
            }

            public void Dispose() { }

            public Result GenerateCommonMountName(Span<byte> nameBuffer)
            {
                ReadOnlySpan<byte> mountName = GetBisMountName(PartitionId);

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

        private static Result MountBis(this FileSystemClientImpl fs, U8Span mountName, BisPartitionId partitionId,
            U8Span rootPath)
        {
            Result rc;

            if (fs.IsEnabledAccessLog(AccessLogTarget.System))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = Mount(fs, mountName, partitionId);
                Tick end = fs.Hos.Os.GetSystemTick();

                Span<byte> logBuffer = stackalloc byte[0x300];
                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogName).Append(mountName).Append(LogQuote)
                    .Append(LogBisPartitionId).Append(idString.ToString(partitionId))
                    .Append(LogPath).Append(rootPath).Append(LogQuote);

                fs.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = Mount(fs, mountName, partitionId);
            }

            fs.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            if (fs.IsEnabledAccessLog(AccessLogTarget.System))
                fs.EnableFileSystemAccessorAccessLog(mountName);

            return Result.Success;

            static Result Mount(FileSystemClientImpl fs, U8Span mountName, BisPartitionId partitionId)
            {
                Result rc = fs.CheckMountNameAcceptingReservedMountName(mountName);
                if (rc.IsFailure()) return rc;

                using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

                // Nintendo doesn't use the provided rootPath
                FspPath.CreateEmpty(out FspPath sfPath);

                ReferenceCountedDisposable<IFileSystemSf> fileSystem = null;
                try
                {
                    rc = fsProxy.Target.OpenBisFileSystem(out fileSystem, in sfPath, partitionId);
                    if (rc.IsFailure()) return rc;

                    var nameGenerator = new BisCommonMountNameGenerator(partitionId);
                    var fileSystemAdapter = new FileSystemServiceObjectAdapter(fileSystem);

                    return fs.Fs.Register(mountName, fileSystemAdapter, nameGenerator);
                }
                finally
                {
                    fileSystem?.Dispose();
                }
            }
        }

        public static Result MountBis(this FileSystemClient fs, U8Span mountName, BisPartitionId partitionId)
        {
            return MountBis(fs.Impl, mountName, partitionId, default);
        }

        public static Result MountBis(this FileSystemClient fs, BisPartitionId partitionId, U8Span rootPath)
        {
            return MountBis(fs.Impl, new U8Span(GetBisMountName(partitionId)), partitionId, rootPath);
        }

        public static ReadOnlySpan<byte> GetBisMountName(BisPartitionId partitionId)
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
                    Abort.DoAbort("The partition specified is not mountable.");
                    break;

                case BisPartitionId.CalibrationFile:
                    return BisCalibrationFilePartitionMountName;
                case BisPartitionId.SafeMode:
                    return BisSafeModePartitionMountName;
                case BisPartitionId.User:
                    return BisUserPartitionMountName;
                case BisPartitionId.System:
                    return BisSystemPartitionMountName;

                default:
                    Abort.UnexpectedDefault();
                    break;
            }

            return ReadOnlySpan<byte>.Empty;
        }

        public static Result SetBisRootForHost(this FileSystemClient fs, BisPartitionId partitionId, U8Span rootPath)
        {
            Unsafe.SkipInit(out FsPath path);
            Result rc;

            int pathLen = StringUtils.GetLength(rootPath, PathTools.MaxPathLength + 1);
            if (pathLen > PathTools.MaxPathLength)
            {
                fs.Impl.LogErrorMessage(ResultFs.TooLongPath.Value);
                return ResultFs.TooLongPath.Log();
            }

            if (pathLen > 0)
            {
                byte endingSeparator = rootPath[pathLen - 1] == StringTraits.DirectorySeparator
                    ? StringTraits.NullTerminator
                    : StringTraits.DirectorySeparator;

                var sb = new U8StringBuilder(path.Str);
                rc = sb.Append(rootPath).Append(endingSeparator).ToSfPath();
                if (rc.IsFailure()) return rc;
            }
            else
            {
                path.Str[0] = StringTraits.NullTerminator;
            }

            FspPath.FromSpan(out FspPath sfPath, path.Str);

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            rc = fsProxy.Target.SetBisRootForHost(partitionId, in sfPath);
            fs.Impl.LogErrorMessage(rc);
            return rc;
        }

        public static Result OpenBisPartition(this FileSystemClient fs, out IStorage partitionStorage,
            BisPartitionId partitionId)
        {
            UnsafeHelpers.SkipParamInit(out partitionStorage);

            ReferenceCountedDisposable<IStorageSf> storage = null;
            try
            {
                using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();
                Result rc = fsProxy.Target.OpenBisStorage(out storage, partitionId);
                fs.Impl.AbortIfNeeded(rc);
                if (rc.IsFailure()) return rc;

                var storageAdapter = new StorageServiceObjectAdapter(storage);

                partitionStorage = storageAdapter;
                return Result.Success;
            }
            finally
            {
                storage?.Dispose();
            }
        }

        public static Result InvalidateBisCache(this FileSystemClient fs)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();
            Result rc = fsProxy.Target.InvalidateBisCache();
            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }
    }
}
