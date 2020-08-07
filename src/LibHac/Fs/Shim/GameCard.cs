using System;
using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.FsSrv;

namespace LibHac.Fs.Shim
{
    public static class GameCard
    {
        public static Result GetGameCardHandle(this FileSystemClient fs, out GameCardHandle handle)
        {
            handle = default;

            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.OpenDeviceOperator(out IDeviceOperator deviceOperator);
            if (rc.IsFailure()) return rc;

            return deviceOperator.GetGameCardHandle(out handle);
        }

        public static bool IsGameCardInserted(this FileSystemClient fs)
        {
            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.OpenDeviceOperator(out IDeviceOperator deviceOperator);
            if (rc.IsFailure()) throw new LibHacException("Abort");

            rc = deviceOperator.IsGameCardInserted(out bool isInserted);
            if (rc.IsFailure()) throw new LibHacException("Abort");

            return isInserted;
        }

        public static Result OpenGameCardPartition(this FileSystemClient fs, out IStorage storage,
            GameCardHandle handle, GameCardPartitionRaw partitionType)
        {
            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            return fsProxy.OpenGameCardStorage(out storage, handle, partitionType);
        }

        public static Result MountGameCardPartition(this FileSystemClient fs, U8Span mountName, GameCardHandle handle,
            GameCardPartition partitionId)
        {
            Result rc = MountHelpers.CheckMountNameAcceptingReservedMountName(mountName);
            if (rc.IsFailure()) return rc;

            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            rc = fsProxy.OpenGameCardFileSystem(out IFileSystem cardFs, handle, partitionId);
            if (rc.IsFailure()) return rc;

            var mountNameGenerator = new GameCardCommonMountNameGenerator(handle, partitionId);

            return fs.Register(mountName, cardFs, mountNameGenerator);
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

                string mountName = $"{CommonMountNames.GameCardFileSystemMountName}{letter}{Handle.Value:x8}";
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
