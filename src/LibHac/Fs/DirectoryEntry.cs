using System;
using System.Runtime.InteropServices;
using LibHac.Common;

namespace LibHac.Fs;

[StructLayout(LayoutKind.Explicit)]
public struct DirectoryEntry
{
    [FieldOffset(0)] private byte _name;
    [FieldOffset(0x301)] public NxFileAttributes Attributes;
    [FieldOffset(0x304)] public DirectoryEntryType Type;
    [FieldOffset(0x308)] public long Size;

    public Span<byte> Name => SpanHelpers.CreateSpan(ref _name, PathTool.EntryNameLengthMax + 1);
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