using System;
using LibHac.Common;
using LibHac.Fs.Impl;
using LibHac.FsSrv;
using LibHac.FsSrv.Sf;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.Fs.Shim
{
    public static class GameCard
    {
        public static Result GetGameCardHandle(this FileSystemClient fs, out GameCardHandle handle)
        {
            handle = default;

            ReferenceCountedDisposable<IDeviceOperator> deviceOperator = null;
            try
            {
                using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

                Result rc = fsProxy.Target.OpenDeviceOperator(out deviceOperator);
                if (rc.IsFailure()) return rc;

                return deviceOperator.Target.GetGameCardHandle(out handle);
            }
            finally
            {
                deviceOperator?.Dispose();
            }
        }

        public static bool IsGameCardInserted(this FileSystemClient fs)
        {
            ReferenceCountedDisposable<IDeviceOperator> deviceOperator = null;
            try
            {
                using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

                Result rc = fsProxy.Target.OpenDeviceOperator(out deviceOperator);
                if (rc.IsFailure()) throw new LibHacException("Abort");

                rc = deviceOperator.Target.IsGameCardInserted(out bool isInserted);
                if (rc.IsFailure()) throw new LibHacException("Abort");

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
            storage = default;

            ReferenceCountedDisposable<IStorageSf> sfStorage = null;
            try
            {
                using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

                Result rc = fsProxy.Target.OpenGameCardStorage(out sfStorage, handle, partitionType);
                if (rc.IsFailure()) return rc;

                storage = new StorageServiceObjectAdapter(sfStorage);
                return Result.Success;
            }
            finally
            {
                sfStorage?.Dispose();
            }
        }

        public static Result MountGameCardPartition(this FileSystemClient fs, U8Span mountName, GameCardHandle handle,
            GameCardPartition partitionId)
        {
            Result rc = MountHelpers.CheckMountNameAcceptingReservedMountName(mountName);
            if (rc.IsFailure()) return rc;

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

            rc = fsProxy.Target.OpenGameCardFileSystem(out ReferenceCountedDisposable<IFileSystemSf> cardFs, handle,
                partitionId);
            if (rc.IsFailure()) return rc;

            using (cardFs)
            {
                var mountNameGenerator = new GameCardCommonMountNameGenerator(handle, partitionId);
                var fileSystemAdapter = new FileSystemServiceObjectAdapter(cardFs);

                return fs.Register(mountName, fileSystemAdapter, mountNameGenerator);
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

            public Result GenerateCommonMountName(Span<byte> nameBuffer)
            {
                char letter = GetGameCardMountNameSuffix(PartitionId);

                string mountName = $"{CommonPaths.GameCardFileSystemMountName}{letter}{Handle.Value:x8}";
                new U8Span(mountName).Value.CopyTo(nameBuffer);

                return Result.Success;
            }

            private static char GetGameCardMountNameSuffix(GameCardPartition partition)
            {
                switch (partition)
                {
                    case GameCardPartition.Update: return 'U';
                    case GameCardPartition.Normal: return 'N';
                    case GameCardPartition.Secure: return 'S';
                    default:
                        throw new ArgumentOutOfRangeException(nameof(partition), partition, null);
                }
            }
        }
    }
}
