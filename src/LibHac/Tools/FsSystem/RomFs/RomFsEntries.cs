using System;
using System.Runtime.InteropServices;

namespace LibHac.Tools.FsSystem.RomFs;

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
        return GetRomHashCode(Parent, Name);
    }

    public static uint GetRomHashCode(int parent, ReadOnlySpan<byte> name)
    {
        uint hash = 123456789 ^ (uint)parent;

        foreach (byte c in name)
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

[StructLayout(LayoutKind.Sequential)]
public struct RomFileInfo
{
    public long Offset;
    public long Length;
}

/// <summary>
/// Represents the current position when enumerating a directory's contents.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct FindPosition
{
    /// <summary>The ID of the next directory to be enumerated.</summary>
    public int NextDirectory;
    /// <summary>The ID of the next file to be enumerated.</summary>
    public int NextFile;
}