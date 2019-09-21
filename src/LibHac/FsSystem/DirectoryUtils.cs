using System;
using LibHac.Common;

namespace LibHac.FsSystem
{
    public static class DirectoryUtils
    {
        public delegate Result Blah(ReadOnlySpan<byte> path, ref DirectoryEntry entry);

        public static Result IterateDirectoryRecursivelyInternal(IFileSystem fs, Span<byte> workPath,
            ref DirectoryEntry entry, Blah onEnterDir, Blah onExitDir, Blah onFile)
        {
            string currentPath = Util.GetUtf8StringNullTerminated(workPath);

            Result rc = fs.OpenDirectory(out IDirectory _, currentPath, OpenDirectoryMode.All);
            if (rc.IsFailure()) return rc;

            onFile(workPath, ref entry);

            return Result.Success;
        }

        public static Result IterateDirectoryRecursively(IFileSystem fs, ReadOnlySpan<byte> path, Blah onEnterDir, Blah onExitDir, Blah onFile)
        {
            return Result.Success;
        }

        public static Result CopyDirectoryRecursively(IFileSystem sourceFs, IFileSystem destFs, string sourcePath,
            string destPath)
        {
            return Result.Success;
        }

        public static Result CopyFile(IFileSystem destFs, IFileSystem sourceFs, ReadOnlySpan<byte> destParentPath,
            ReadOnlySpan<byte> sourcePath, ref DirectoryEntry dirEntry, Span<byte> copyBuffer)
        {
            IFile srcFile = null;
            IFile dstFile = null;

            try
            {
                Result rc = sourceFs.OpenFile(out srcFile, StringUtils.Utf8ZToString(sourcePath), OpenMode.Read);
                if (rc.IsFailure()) return rc;

                FsPath dstPath = default;
                int dstPathLen = StringUtils.Concat(dstPath.Str, destParentPath);
                dstPathLen = StringUtils.Concat(dstPath.Str, dstPathLen, dirEntry.Name);

                if (dstPathLen > FsPath.MaxLength)
                {
                    throw new ArgumentException();
                }

                string dstPathStr = StringUtils.Utf8ZToString(dstPath.Str);

                rc = destFs.CreateFile(dstPathStr, dirEntry.Size, CreateFileOptions.None);
                if (rc.IsFailure()) return rc;

                rc = destFs.OpenFile(out dstFile, dstPathStr, OpenMode.Write);
                if (rc.IsFailure()) return rc;

                long fileSize = dirEntry.Size;
                long offset = 0;

                while (offset < fileSize)
                {
                    rc = srcFile.Read(out long bytesRead, offset, copyBuffer, ReadOption.None);
                    if (rc.IsFailure()) return rc;

                    rc = dstFile.Write(offset, copyBuffer.Slice(0, (int)bytesRead), WriteOption.None);
                    if (rc.IsFailure()) return rc;

                    offset += bytesRead;
                }

                return Result.Success;
            }
            finally
            {
                srcFile?.Dispose();
                dstFile?.Dispose();
            }
        }
    }
}
