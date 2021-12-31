using System;
using LibHac.Common.FixedArrays;

namespace LibHac.Fs;
public struct DirectoryEntry
{
    public Array769<byte> Name;
    public NxFileAttributes Attributes;
    public Array2<byte> Reserved302;
    public DirectoryEntryType Type;
    public Array3<byte> Reserved305;
    public long Size;
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