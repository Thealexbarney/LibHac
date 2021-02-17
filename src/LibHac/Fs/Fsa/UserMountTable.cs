using System;
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
            throw new NotImplementedException();
        }

        public static Result Find(this FileSystemClientImpl fs, out FileSystemAccessor fileSystem, U8Span name)
        {
            throw new NotImplementedException();
        }

        public static void Unregister(this FileSystemClientImpl fs, U8Span name)
        {
            throw new NotImplementedException();
        }
    }
}
