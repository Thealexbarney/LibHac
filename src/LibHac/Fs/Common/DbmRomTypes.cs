using System.Runtime.InteropServices;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs;

[StructLayout(LayoutKind.Sequential)]
public struct RomFileSystemInformation
{
    public long HeaderSize;
    public long DirectoryBucketOffset;
    public long DirectoryBucketSize;
    public long DirectoryEntryOffset;
    public long DirectoryEntrySize;
    public long FileBucketOffset;
    public long FileBucketSize;
    public long FileEntryOffset;
    public long FileEntrySize;
    public long DataOffset;
}

[StructLayout(LayoutKind.Sequential)]
public struct RomFileInfo
{
    public Int64 Offset;
    public Int64 Size;
}