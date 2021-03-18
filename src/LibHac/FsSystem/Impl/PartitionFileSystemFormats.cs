using System.Runtime.InteropServices;
using LibHac.Common;

namespace LibHac.FsSystem.Impl
{
    public interface IPartitionFileSystemEntry
    {
        long Offset { get; }
        long Size { get; }
        int NameOffset { get; }
    }

    [StructLayout(LayoutKind.Sequential, Size = 0x18)]
    public struct StandardEntry : IPartitionFileSystemEntry
    {
        public long Offset;
        public long Size;
        public int NameOffset;

        long IPartitionFileSystemEntry.Offset => Offset;
        long IPartitionFileSystemEntry.Size => Size;
        int IPartitionFileSystemEntry.NameOffset => NameOffset;
    }

    [StructLayout(LayoutKind.Sequential, Size = 0x40)]
    public struct HashedEntry : IPartitionFileSystemEntry
    {
        public long Offset;
        public long Size;
        public int NameOffset;
        public int HashSize;
        public long HashOffset;
        public Buffer32 Hash;

        long IPartitionFileSystemEntry.Offset => Offset;
        long IPartitionFileSystemEntry.Size => Size;
        int IPartitionFileSystemEntry.NameOffset => NameOffset;
    }
}
