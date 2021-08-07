using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;

namespace LibHac.Tests.Fs
{
    public static class FsaExtensions
    {
        private static Result SetUpPath(out Path path, string value)
        {
            path = new Path();

            if (value is null)
                return ResultFs.NullptrArgument.Log();

            Result rc = path.Initialize(StringUtils.StringToUtf8(value));
            if (rc.IsFailure()) return rc;

            rc = path.Normalize(new PathFlags());
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        public static Result CreateFile(this IFileSystem fs, string path, long size, CreateFileOptions option)
        {
            Result rc = SetUpPath(out Path pathNormalized, path);
            if (rc.IsFailure()) return rc;

            rc = fs.CreateFile(in pathNormalized, size, option);

            pathNormalized.Dispose();
            return rc;
        }

        public static Result CreateFile(this IFileSystem fs, string path, long size)
        {
            return CreateFile(fs, path, size, CreateFileOptions.None);
        }

        public static Result DeleteFile(this IFileSystem fs, string path)
        {
            Result rc = SetUpPath(out Path pathNormalized, path);
            if (rc.IsFailure()) return rc;

            rc = fs.DeleteFile(in pathNormalized);

            pathNormalized.Dispose();
            return rc;
        }

        public static Result CreateDirectory(this IFileSystem fs, string path)
        {
            Result rc = SetUpPath(out Path pathNormalized, path);
            if (rc.IsFailure()) return rc;

            rc = fs.CreateDirectory(in pathNormalized);

            pathNormalized.Dispose();
            return rc;
        }

        public static Result DeleteDirectory(this IFileSystem fs, string path)
        {
            Result rc = SetUpPath(out Path pathNormalized, path);
            if (rc.IsFailure()) return rc;

            rc = fs.DeleteDirectory(in pathNormalized);

            pathNormalized.Dispose();
            return rc;
        }

        public static Result DeleteDirectoryRecursively(this IFileSystem fs, string path)
        {
            Result rc = SetUpPath(out Path pathNormalized, path);
            if (rc.IsFailure()) return rc;

            rc = fs.DeleteDirectoryRecursively(in pathNormalized);

            pathNormalized.Dispose();
            return rc;
        }

        public static Result CleanDirectoryRecursively(this IFileSystem fs, string path)
        {
            Result rc = SetUpPath(out Path pathNormalized, path);
            if (rc.IsFailure()) return rc;

            rc = fs.CleanDirectoryRecursively(in pathNormalized);

            pathNormalized.Dispose();
            return rc;
        }

        public static Result RenameFile(this IFileSystem fs, string currentPath, string newPath)
        {
            Result rc = SetUpPath(out Path currentPathNormalized, currentPath);
            if (rc.IsFailure()) return rc;

            rc = SetUpPath(out Path newPathNormalized, newPath);
            if (rc.IsFailure()) return rc;

            rc = fs.RenameFile(in currentPathNormalized, in newPathNormalized);

            currentPathNormalized.Dispose();
            newPathNormalized.Dispose();
            return rc;
        }

        public static Result RenameDirectory(this IFileSystem fs, string currentPath, string newPath)
        {
            Result rc = SetUpPath(out Path currentPathNormalized, currentPath);
            if (rc.IsFailure()) return rc;

            rc = SetUpPath(out Path newPathNormalized, newPath);
            if (rc.IsFailure()) return rc;

            rc = fs.RenameDirectory(in currentPathNormalized, in newPathNormalized);

            currentPathNormalized.Dispose();
            newPathNormalized.Dispose();
            return rc;
        }

        public static Result GetEntryType(this IFileSystem fs, out DirectoryEntryType entryType, string path)
        {
            UnsafeHelpers.SkipParamInit(out entryType);

            Result rc = SetUpPath(out Path pathNormalized, path);
            if (rc.IsFailure()) return rc;

            rc = fs.GetEntryType(out entryType, in pathNormalized);

            pathNormalized.Dispose();
            return rc;
        }

        public static Result GetFreeSpaceSize(this IFileSystem fs, out long freeSpace, string path)
        {
            UnsafeHelpers.SkipParamInit(out freeSpace);

            Result rc = SetUpPath(out Path pathNormalized, path);
            if (rc.IsFailure()) return rc;

            rc = fs.GetFreeSpaceSize(out freeSpace, in pathNormalized);

            pathNormalized.Dispose();
            return rc;
        }

        public static Result GetTotalSpaceSize(this IFileSystem fs, out long totalSpace, string path)
        {
            UnsafeHelpers.SkipParamInit(out totalSpace);

            Result rc = SetUpPath(out Path pathNormalized, path);
            if (rc.IsFailure()) return rc;

            rc = fs.GetTotalSpaceSize(out totalSpace, in pathNormalized);

            pathNormalized.Dispose();
            return rc;
        }

        public static Result OpenFile(this IFileSystem fs, out IFile file, string path, OpenMode mode)
        {
            UnsafeHelpers.SkipParamInit(out file);

            Result rc = SetUpPath(out Path pathNormalized, path);
            if (rc.IsFailure()) return rc;

            rc = fs.OpenFile(out file, in pathNormalized, mode);

            pathNormalized.Dispose();
            return rc;
        }

        public static Result OpenDirectory(this IFileSystem fs, out IDirectory directory, string path, OpenDirectoryMode mode)
        {
            UnsafeHelpers.SkipParamInit(out directory);

            Result rc = SetUpPath(out Path pathNormalized, path);
            if (rc.IsFailure()) return rc;

            rc = fs.OpenDirectory(out directory, in pathNormalized, mode);

            pathNormalized.Dispose();
            return rc;
        }

        public static Result GetFileTimeStampRaw(this IFileSystem fs, out FileTimeStampRaw timeStamp, string path)
        {
            UnsafeHelpers.SkipParamInit(out timeStamp);

            Result rc = SetUpPath(out Path pathNormalized, path);
            if (rc.IsFailure()) return rc;

            rc = fs.GetFileTimeStampRaw(out timeStamp, in pathNormalized);

            pathNormalized.Dispose();
            return rc;
        }

        public static Result QueryEntry(this IFileSystem fs, Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, string path)
        {
            Result rc = SetUpPath(out Path pathNormalized, path);
            if (rc.IsFailure()) return rc;

            rc = fs.QueryEntry(outBuffer, inBuffer, queryId, in pathNormalized);

            pathNormalized.Dispose();
            return rc;
        }

        public static Result CreateDirectory(this IAttributeFileSystem fs, string path, NxFileAttributes archiveAttribute)
        {
            Result rc = SetUpPath(out Path pathNormalized, path);
            if (rc.IsFailure()) return rc;

            rc = fs.CreateDirectory(in pathNormalized, archiveAttribute);

            pathNormalized.Dispose();
            return rc;
        }

        public static Result GetFileAttributes(this IAttributeFileSystem fs, out NxFileAttributes attributes, string path)
        {
            UnsafeHelpers.SkipParamInit(out attributes);

            Result rc = SetUpPath(out Path pathNormalized, path);
            if (rc.IsFailure()) return rc;

            rc = fs.GetFileAttributes(out attributes, in pathNormalized);

            pathNormalized.Dispose();
            return rc;
        }

        public static Result SetFileAttributes(this IAttributeFileSystem fs, string path, NxFileAttributes attributes)
        {
            Result rc = SetUpPath(out Path pathNormalized, path);
            if (rc.IsFailure()) return rc;

            rc = fs.SetFileAttributes(in pathNormalized, attributes);

            pathNormalized.Dispose();
            return rc;
        }

        public static Result GetFileSize(this IAttributeFileSystem fs, out long fileSize, string path)
        {
            UnsafeHelpers.SkipParamInit(out fileSize);

            Result rc = SetUpPath(out Path pathNormalized, path);
            if (rc.IsFailure()) return rc;

            rc = fs.GetFileSize(out fileSize, in pathNormalized);

            pathNormalized.Dispose();
            return rc;
        }
    }
}
