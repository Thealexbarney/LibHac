using System.Runtime.InteropServices;

namespace LibHac.Kvdb
{
    [StructLayout(LayoutKind.Sequential, Size = 0xC)]
    internal struct ImkvdbHeader
    {
        public const uint ExpectedMagic = 0x564B4D49; // IMKV

        public uint Magic;
        public int Reserved;
        public int EntryCount;
    }

    [StructLayout(LayoutKind.Sequential, Size = 0xC)]
    internal struct ImkvdbEntryHeader
    {
        public const uint ExpectedMagic = 0x4E454D49; // IMEN

        public uint Magic;
        public int KeySize;
        public int ValueSize;
    }
}
