using System;
using LibHac.FsService;

namespace LibHac.Fs
{
    public static class GameCard
    {
        public static Result OpenGameCardPartition(this FileSystemClient fs, out IStorage storage,
            GameCardHandle handle, GameCardPartitionRaw partitionType)
        {
            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            return fsProxy.OpenGameCardStorage(out storage, handle, partitionType);
        }

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

        public static long GetGameCardSizeBytes(GameCardSize size)
        {
            switch (size)
            {
                case GameCardSize.Size1Gb: return 0x3B800000;
                case GameCardSize.Size2Gb: return 0x77000000;
                case GameCardSize.Size4Gb: return 0xEE000000;
                case GameCardSize.Size8Gb: return 0x1DC000000;
                case GameCardSize.Size16Gb: return 0x3B8000000;
                case GameCardSize.Size32Gb: return 0x770000000;
                default:
                    throw new ArgumentOutOfRangeException(nameof(size), size, null);
            }
        }

        public static long CardPageToOffset(int page)
        {
            return (long)page << 9;
        }
    }

    public enum GameCardSize
    {
        Size1Gb = 0xFA,
        Size2Gb = 0xF8,
        Size4Gb = 0xF0,
        Size8Gb = 0xE0,
        Size16Gb = 0xE1,
        Size32Gb = 0xE2
    }

    [Flags]
    public enum GameCardAttribute : byte
    {
        AutoBoot = 1 << 0,
        HistoryErase = 1 << 1,
        RepairTool = 1 << 2
    }
}
