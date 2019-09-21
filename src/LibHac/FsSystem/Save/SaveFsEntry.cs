using System;
using System.Runtime.InteropServices;

namespace LibHac.FsSystem.Save
{
    internal ref struct SaveEntryKey
    {
        public ReadOnlySpan<byte> Name;
        public int Parent;

        public SaveEntryKey(ReadOnlySpan<byte> name, int parent)
        {
            Name = name;
            Parent = parent;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 0x14)]
    public struct SaveFileInfo
    {
        public int StartBlock;
        public long Length;
        public long Reserved;
    }

    /// <summary>
    /// Represents the current position when enumerating a directory's contents.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 0x14)]
    public struct SaveFindPosition
    {
        /// <summary>The ID of the next directory to be enumerated.</summary>
        public int NextDirectory;
        /// <summary>The ID of the next file to be enumerated.</summary>
        public int NextFile;
    }
}
