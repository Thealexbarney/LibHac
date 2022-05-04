using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Os;
using LibHac.Util;
using static LibHac.Fs.Impl.AccessLogStrings;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions used for mounting and interacting with the game card.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
[SkipLocalsInit]
public static class GameCard
{
    private static ReadOnlySpan<byte> GetGameCardMountNameSuffix(GameCardPartition partition)
    {
        switch (partition)
        {
            case GameCardPartition.Update: return CommonMountNames.GameCardFileSystemMountNameUpdateSuffix;
            case GameCardPartition.Normal: return CommonMountNames.GameCardFileSystemMountNameNormalSuffix;
            case GameCardPartition.Secure: return CommonMountNames.GameCardFileSystemMountNameSecureSuffix;
            default:
                Abort.UnexpectedDefault();
                return default;
        }
    }

    private class GameCardCommonMountNameGenerator : ICommonMountNameGenerator
    {
        private readonly GameCardHandle _handle;
        private readonly GameCardPartition _partitionId;

        public GameCardCommonMountNameGenerator(GameCardHandle handle, GameCardPartition partitionId)
        {
            _handle = handle;
            _partitionId = partitionId;
        }

        public void Dispose() { }

        public Result GenerateCommonMountName(Span<byte> nameBuffer)
        {
            int handleDigitCount = Unsafe.SizeOf<GameCardHandle>() * 2;

            // Determine how much space we need.
            int requiredNameBufferSize =
                StringUtils.GetLength(CommonMountNames.GameCardFileSystemMountName, PathTool.MountNameLengthMax) +
                StringUtils.GetLength(GetGameCardMountNameSuffix(_partitionId), PathTool.MountNameLengthMax) +
                handleDigitCount + 2;

            Assert.SdkRequiresGreaterEqual(nameBuffer.Length, requiredNameBufferSize);

            // Generate the name.
            var sb = new U8StringBuilder(nameBuffer);
            sb.Append(CommonMountNames.GameCardFileSystemMountName)
                .Append(GetGameCardMountNameSuffix(_partitionId))
                .AppendFormat(_handle, 'x', (byte)handleDigitCount)
                .Append(StringTraits.DriveSeparator);

            Assert.SdkEqual(sb.Length, requiredNameBufferSize - 1);

            return Result.Success;
        }
    }

    public static Result GetGameCardHandle(this FileSystemClient fs, out GameCardHandle outHandle)
    {
        UnsafeHelpers.SkipParamInit(out outHandle);

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc;

        rc = deviceOperator.Get.GetGameCardHandle(out GameCardHandle handle);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        outHandle = handle;
        return Result.Success;
    }

    public static Result MountGameCardPartition(this FileSystemClient fs, U8Span mountName, GameCardHandle handle,
        GameCardPartition partitionId)
    {
        Result rc;
        Span<byte> logBuffer = stackalloc byte[0x60];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            rc = Mount(fs, mountName, handle, partitionId);
            Tick end = fs.Hos.Os.GetSystemTick();

            var idString = new IdString();
            var sb = new U8StringBuilder(logBuffer, true);

            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogGameCardHandle).AppendFormat(handle)
                .Append(LogGameCardPartition).Append(idString.ToString(partitionId));

            fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            rc = Mount(fs, mountName, handle, partitionId);
        }

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc;

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;

        static Result Mount(FileSystemClient fs, U8Span mountName, GameCardHandle handle,
            GameCardPartition partitionId)
        {
            Result rc = fs.Impl.CheckMountNameAcceptingReservedMountName(mountName);
            if (rc.IsFailure()) return rc;

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
            using var fileSystem = new SharedRef<IFileSystemSf>();

            rc = fileSystemProxy.Get.OpenGameCardFileSystem(ref fileSystem.Ref(), handle, partitionId);
            if (rc.IsFailure()) return rc;

            using var fileSystemAdapter =
                new UniqueRef<IFileSystem>(new FileSystemServiceObjectAdapter(ref fileSystem.Ref()));

            if (!fileSystemAdapter.HasValue)
                return ResultFs.AllocationMemoryFailedInGameCardC.Log();

            using var mountNameGenerator =
                new UniqueRef<ICommonMountNameGenerator>(new GameCardCommonMountNameGenerator(handle, partitionId));

            if (!mountNameGenerator.HasValue)
                return ResultFs.AllocationMemoryFailedInGameCardD.Log();

            return fs.Register(mountName, ref fileSystemAdapter.Ref(), ref mountNameGenerator.Ref());
        }
    }

    public static bool IsGameCardInserted(this FileSystemClient fs)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.LogResultErrorMessage(rc);
        Abort.DoAbortUnless(rc.IsSuccess());

        rc = deviceOperator.Get.IsGameCardInserted(out bool isInserted);
        fs.Impl.LogResultErrorMessage(rc);
        Abort.DoAbortUnless(rc.IsSuccess());

        return isInserted;
    }

    public static Result OpenGameCardPartition(this FileSystemClient fs, ref UniqueRef<IStorage> outStorage,
        GameCardHandle handle, GameCardPartitionRaw partitionType)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var storage = new SharedRef<IStorageSf>();

        Result rc = fileSystemProxy.Get.OpenGameCardStorage(ref storage.Ref(), handle, partitionType);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc;

        using var storageAdapter = new UniqueRef<IStorage>(new StorageServiceObjectAdapter(ref storage.Ref()));

        if (!storageAdapter.HasValue)
            return ResultFs.AllocationMemoryFailedInGameCardB.Log();

        outStorage.Set(ref storageAdapter.Ref());
        return Result.Success;
    }
}