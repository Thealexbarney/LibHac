using System;
using System.Runtime.InteropServices;

namespace LibHac.IO.RomFs
{
    internal ref struct RomEntryKey
    {
        public ReadOnlySpan<byte> Name;
        public int Parent;

        public RomEntryKey(ReadOnlySpan<byte> name, int parent)
        {
            Name = name;
            Parent = parent;
        }

        public uint GetRomHashCode()
        {
            uint hash = 123456789 ^ (uint)Parent;

            foreach (byte c in Name)
            {
                hash = c ^ ((hash << 27) | (hash >> 5));
            }

            return hash;
        }
    }

    internal ref struct RomKeyValuePair<T> where T : unmanaged
    {
        public RomEntryKey Key;
        public int Offset;
        public T Value;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct RomFsEntry<T> where T : unmanaged
    {
        public int Parent;
        public T Value;
        public int Next;
        public int KeyLength;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct FileRomEntry
    {
        public int NextSibling;
        public RomFileInfo Info;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RomFileInfo
    {
        public long Offset;
        public long Length;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct DirectoryRomEntry
    {
        public int NextSibling;
        public FindPosition Pos;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FindPosition
    {
        public int NextDirectory;
        public int NextFile;
    }
}
