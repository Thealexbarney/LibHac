using LibHac.Common;
using LibHac.Fs.Impl;

namespace LibHac.Fs.Fsa;

internal struct UserMountTableGlobals
{
    public MountTable MountTable;

    public void Initialize(FileSystemClient fsClient)
    {
        MountTable = new MountTable(fsClient);
    }
}

/// <summary>
/// Contains functions for adding, removing and retrieving <see cref="FileSystemAccessor"/>s from the mount table.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
internal static class UserMountTable
{
    public static Result Register(this FileSystemClientImpl fs, ref UniqueRef<FileSystemAccessor> fileSystem)
    {
        return fs.Globals.UserMountTable.MountTable.Mount(ref fileSystem);
    }

    public static Result Find(this FileSystemClientImpl fs, out FileSystemAccessor fileSystem, U8Span name)
    {
        return fs.Globals.UserMountTable.MountTable.Find(out fileSystem, name);
    }

    public static void Unregister(this FileSystemClientImpl fs, U8Span name)
    {
        fs.Globals.UserMountTable.MountTable.Unmount(name);
    }
}