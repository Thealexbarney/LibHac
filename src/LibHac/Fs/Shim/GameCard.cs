using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv;
using LibHac.FsSrv.Sf;
using LibHac.Os;
using LibHac.Util;
using static LibHac.Fs.Impl.AccessLogStrings;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.Fs.Shim
{
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
            private GameCardHandle Handle { get; }
            private GameCardPartition PartitionId { get; }

            public GameCardCommonMountNameGenerator(GameCardHandle handle, GameCardPartition partitionId)
            {
                Handle = handle;
                PartitionId = partitionId;
            }

            public void Dispose() { }

            public Result GenerateCommonMountName(Span<byte> nameBuffer)
            {
                int handleDigitCount = Unsafe.SizeOf<GameCardHandle>() * 2;

                // Determine how much space we need.
                int neededSize =
                    StringUtils.GetLength(CommonMountNames.GameCardFileSystemMountName, PathTool.MountNameLengthMax) +
                    StringUtils.GetLength(GetGameCardMountNameSuffix(PartitionId), PathTool.MountNameLengthMax) +
                    handleDigitCount + 2;

                Assert.SdkRequiresGreaterEqual(nameBuffer.Length, neededSize);

                // Generate the name.
                var sb = new U8StringBuilder(nameBuffer);
                sb.Append(CommonMountNames.GameCardFileSystemMountName)
                    .Append(GetGameCardMountNameSuffix(PartitionId))
                    .AppendFormat(Handle.Value, 'x', (byte)handleDigitCount)
                    .Append(StringTraits.DriveSeparator);

                Assert.SdkEqual(sb.Length, neededSize - 1);

                return Result.Success;
            }
        }

        public static Result GetGameCardHandle(this FileSystemClient fs, out GameCardHandle handle)
        {
            UnsafeHelpers.SkipParamInit(out handle);

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            ReferenceCountedDisposable<IDeviceOperator> deviceOperator = null;
            try
            {
                Result rc = fsProxy.Target.OpenDeviceOperator(out deviceOperator);
                fs.Impl.AbortIfNeeded(rc);
                if (rc.IsFailure()) return rc;

                rc = deviceOperator.Target.GetGameCardHandle(out handle);
                fs.Impl.AbortIfNeeded(rc);
                return rc;
            }
            finally
            {
                deviceOperator?.Dispose();
            }
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
                    .Append(LogGameCardHandle).AppendFormat(handle.Value)
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

                using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

                ReferenceCountedDisposable<IFileSystemSf> fileSystem = null;
                try
                {
                    rc = fsProxy.Target.OpenGameCardFileSystem(out fileSystem, handle, partitionId);
                    if (rc.IsFailure()) return rc;

                    var fileSystemAdapter = new FileSystemServiceObjectAdapter(fileSystem);
                    var mountNameGenerator = new GameCardCommonMountNameGenerator(handle, partitionId);
                    return fs.Register(mountName, fileSystemAdapter, mountNameGenerator);
                }
                finally
                {
                    fileSystem?.Dispose();
                }
            }
        }

        public static bool IsGameCardInserted(this FileSystemClient fs)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            ReferenceCountedDisposable<IDeviceOperator> deviceOperator = null;
            try
            {
                Result rc = fsProxy.Target.OpenDeviceOperator(out deviceOperator);
                fs.Impl.LogErrorMessage(rc);
                Abort.DoAbortUnless(rc.IsSuccess());

                rc = deviceOperator.Target.IsGameCardInserted(out bool isInserted);
                fs.Impl.LogErrorMessage(rc);
                Abort.DoAbortUnless(rc.IsSuccess());

                return isInserted;
            }
            finally
            {
                deviceOperator?.Dispose();
            }
        }

        public static Result OpenGameCardPartition(this FileSystemClient fs, out IStorage storage,
            GameCardHandle handle, GameCardPartitionRaw partitionType)
        {
            UnsafeHelpers.SkipParamInit(out storage);

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            ReferenceCountedDisposable<IStorageSf> sfStorage = null;
            try
            {
                Result rc = fsProxy.Target.OpenGameCardStorage(out sfStorage, handle, partitionType);
                fs.Impl.AbortIfNeeded(rc);
                if (rc.IsFailure()) return rc;

                storage = new StorageServiceObjectAdapter(sfStorage);
                return Result.Success;
            }
            finally
            {
                sfStorage?.Dispose();
            }
        }
    }
}
