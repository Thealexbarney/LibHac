using System;
using System.Collections.Generic;
using System.Text;

namespace libhac
{
    public class Romfs
    {
        public static readonly int IvfcMaxLevel = 6;
    }

    public class RomfsHeader
    {
        public ulong HeaderSize;
        public ulong DirHashTableOffset;
        public ulong DirHashTableSize;
        public ulong DirMetaTableOffset;
        public ulong DirMetaTableSize;
        public ulong FileHashTableOffset;
        public ulong FileHashTableSize;
        public ulong FileMetaTableOffset;
        public ulong FileMetaTableSize;
        public ulong DataOffset;
    }

    public class IvfcLevel
    {
        public ulong DataOffset { get; set; }
        public ulong DataSize { get; set; }
        public ulong HashOffset { get; set; }
        public ulong HashBlockSize { get; set; }
        public ulong HashBlockCount { get; set; }
    }
}
