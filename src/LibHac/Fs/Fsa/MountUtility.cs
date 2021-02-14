using System;
using LibHac.Common;
using LibHac.Fs.Impl;

namespace LibHac.Fs.Fsa
{
    public static class MountUtility
    {
        internal static Result GetMountNameAndSubPath(out MountName mountName, out U8Span subPath, U8Span path)
        {
            throw new NotImplementedException();
        }

        public static bool IsValidMountName(this FileSystemClientImpl fs, U8Span name)
        {
            throw new NotImplementedException();
        }

        public static bool IsUsedReservedMountName(this FileSystemClientImpl fs, U8Span name)
        {
            throw new NotImplementedException();
        }

        internal static Result FindFileSystem(this FileSystemClientImpl fs, out FileSystemAccessor fileSystem,
            out U8Span subPath, U8Span path)
        {
            throw new NotImplementedException();
        }

        public static Result CheckMountName(this FileSystemClientImpl fs, U8Span name)
        {
            throw new NotImplementedException();
        }

        public static Result CheckMountNameAcceptingReservedMountName(this FileSystemClientImpl fs, U8Span name)
        {
            throw new NotImplementedException();
        }

        public static Result Unmount(this FileSystemClientImpl fs, U8Span mountName)
        {
            throw new NotImplementedException();
        }

        public static Result IsMounted(this FileSystemClientImpl fs, out bool isMounted, U8Span mountName)
        {
            throw new NotImplementedException();
        }

        public static Result Unmount(this FileSystemClient fs, U8Span mountName)
        {
            throw new NotImplementedException();
        }

        public static Result IsMounted(this FileSystemClient fs, out bool isMounted, U8Span mountName)
        {
            throw new NotImplementedException();
        }

        public static Result ConvertToFsCommonPath(this FileSystemClient fs, U8SpanMutable commonPathBuffer,
            U8Span path)
        {
            throw new NotImplementedException();
        }
    }
}
