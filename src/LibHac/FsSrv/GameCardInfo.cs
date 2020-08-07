using LibHac.Fs;

namespace LibHac.FsSrv
{
    internal class GameCardInfo
    {
        public byte[] RootPartitionHeaderHash { get; } = new byte[0x20];
        public ulong PackageId { get; set; }
        public long Size { get; set; }
        public long RootPartitionOffset { get; set; }
        public long RootPartitionHeaderSize { get; set; }
        public long SecureAreaOffset { get; set; }
        public long SecureAreaSize { get; set; }
        public int UpdateVersion { get; set; }
        public ulong UpdateTitleId { get; set; }
        public GameCardAttribute Attribute { get; set; }
    }
}
