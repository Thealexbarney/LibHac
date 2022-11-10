using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Os;
using LibHac.Util;
using static LibHac.Fs.Impl.CommonMountNames;
using static LibHac.Fs.Impl.AccessLogStrings;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions for mounting built-in-storage partition file systems
/// and opening the raw partitions as <see cref="IStorage"/>s.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
[SkipLocalsInit]
public static class Bis
{
    private class BisCommonMountNameGenerator : ICommonMountNameGenerator
    {
        private BisPartitionId _partitionId;

        public BisCommonMountNameGenerator(BisPartitionId partitionId)
        {
            _partitionId = partitionId;
        }

        public void Dispose() { }

        public Result GenerateCommonMountName(Span<byte> nameBuffer)
        {
            ReadOnlySpan<byte> mountName = GetBisMountName(_partitionId);

            // Add 2 for the mount name separator and null terminator
            int requiredNameBufferSize = StringUtils.GetLength(mountName, PathTool.MountNameLengthMax) + 2;

            Assert.SdkRequiresGreaterEqual(nameBuffer.Length, requiredNameBufferSize);

            var sb = new U8StringBuilder(nameBuffer);
            sb.Append(mountName).Append(StringTraits.DriveSeparator);

            Assert.SdkEqual(sb.Length, requiredNameBufferSize - 1);

            return Result.Success;
        }
    }

    private static Result MountBis(this FileSystemClientImpl fs, U8Span mountName, BisPartitionId partitionId,
        U8Span rootPath)
    {
        Result res;

        if (fs.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = Mount(fs, mountName, partitionId);
            Tick end = fs.Hos.Os.GetSystemTick();

            Span<byte> logBuffer = stackalloc byte[0x300];
            var idString = new IdString();
            var sb = new U8StringBuilder(logBuffer, true);

            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogBisPartitionId).Append(idString.ToString(partitionId))
                .Append(LogPath).Append(rootPath).Append(LogQuote);

            fs.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            res = Mount(fs, mountName, partitionId);
        }

        fs.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        if (fs.IsEnabledAccessLog(AccessLogTarget.System))
            fs.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;

        static Result Mount(FileSystemClientImpl fs, U8Span mountName, BisPartitionId partitionId)
        {
            Result res = fs.CheckMountNameAcceptingReservedMountName(mountName);
            if (res.IsFailure()) return res.Miss();

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();

            // Nintendo doesn't use the provided rootPath
            FspPath.CreateEmpty(out FspPath sfPath);

            using var fileSystem = new SharedRef<IFileSystemSf>();

            res = fileSystemProxy.Get.OpenBisFileSystem(ref fileSystem.Ref(), in sfPath, partitionId);
            if (res.IsFailure()) return res.Miss();

            using var mountNameGenerator =
                new UniqueRef<ICommonMountNameGenerator>(new BisCommonMountNameGenerator(partitionId));

            if (!mountNameGenerator.HasValue)
                return ResultFs.AllocationMemoryFailedInBisA.Log();

            using var fileSystemAdapter =
                new UniqueRef<IFileSystem>(new FileSystemServiceObjectAdapter(ref fileSystem.Ref()));

            if (!fileSystemAdapter.HasValue)
                return ResultFs.AllocationMemoryFailedInBisB.Log();

            res = fs.Fs.Register(mountName, ref fileSystemAdapter.Ref(), ref mountNameGenerator.Ref());
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }
    }

    public static Result MountBis(this FileSystemClient fs, U8Span mountName, BisPartitionId partitionId)
    {
        return MountBis(fs.Impl, mountName, partitionId, U8Span.Empty);
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

    public static Result OpenBisPartition(this FileSystemClient fs, ref UniqueRef<IStorage> outPartitionStorage,
        BisPartitionId partitionId)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var storage = new SharedRef<IStorageSf>();

        Result res = fileSystemProxy.Get.OpenBisStorage(ref storage.Ref(), partitionId);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        using var storageAdapter = new UniqueRef<IStorage>(new StorageServiceObjectAdapter(ref storage.Ref()));

        if (!storageAdapter.HasValue)
        {
            res = ResultFs.AllocationMemoryFailedInBisC.Value;
            fs.Impl.AbortIfNeeded(res);
            if (res.IsFailure()) return res.Log();
        }

        outPartitionStorage.Set(ref storageAdapter.Ref());
        return Result.Success;
    }

    public static Result InvalidateBisCache(this FileSystemClient fs)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result res = fileSystemProxy.Get.InvalidateBisCache();
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }
}