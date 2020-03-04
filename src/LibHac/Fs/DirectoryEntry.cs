using System;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.FsSystem;

namespace LibHac.Fs
{
    public class DirectoryEntryEx
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public NxFileAttributes Attributes { get; set; }
        public DirectoryEntryType Type { get; set; }
        public long Size { get; set; }

        public DirectoryEntryEx(string name, string fullPath, DirectoryEntryType type, long size)
        {
            Name = name;
            FullPath = PathTools.Normalize(fullPath);
            Type = type;
            Size = size;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct DirectoryEntry
    {
        [FieldOffset(0)] private byte _name;
        [FieldOffset(0x301)] public NxFileAttributes Attributes;
        [FieldOffset(0x304)] public DirectoryEntryType Type;
        [FieldOffset(0x308)] public long Size;

        public Span<byte> Name => SpanHelpers.CreateSpan(ref _name, PathTools.MaxPathLength + 1);
    }

    public enum DirectoryEntryType : byte
    {
        Directory,
        File
    }

    [Flags]
    public enum NxFileAttributes : byte
    {
        None = 0,
        Directory = 1 << 0,
        Archive = 1 << 1
    }
}
