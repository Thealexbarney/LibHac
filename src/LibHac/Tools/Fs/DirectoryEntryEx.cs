using LibHac.Fs;
using LibHac.FsSystem;

namespace LibHac.Tools.Fs;

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