using LibHac.Common;
using LibHac.Fs.Impl;

namespace LibHac.Fs.Fsa
{
    internal struct UserMountTableGlobals
    {
        public MountTable MountTable;

        public void Initialize(FileSystemClient fsClient)
        {
            MountTable = new MountTable(fsClient);
        }
    }

    internal static class UserMountTable
    {
        public static Result Register(this FileSystemClientImpl fs, FileSystemAccessor fileSystem)
        {
            return fs.Globals.UserMountTable.MountTable.Mount(fileSystem);
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
}
