using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;

namespace LibHac.FsSystem
{
    public static class DirectoryUtils
    {
        public delegate Result Blah(ReadOnlySpan<byte> path, ref DirectoryEntry entry);

        public static Result IterateDirectoryRecursivelyInternal(IFileSystem fs, Span<byte> workPath,
            ref DirectoryEntry entry, Blah onEnterDir, Blah onExitDir, Blah onFile)
        {
            Result rc = fs.OpenDirectory(out IDirectory _, new U8Span(workPath), OpenDirectoryMode.All);
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
                Result rc = sourceFs.OpenFile(out srcFile, new U8Span(sourcePath), OpenMode.Read);
                if (rc.IsFailure()) return rc;

                Unsafe.SkipInit(out FsPath dstPath);
                dstPath.Str[0] = 0;
                int dstPathLen = StringUtils.Concat(dstPath.Str, destParentPath);
                dstPathLen = StringUtils.Concat(dstPath.Str, dirEntry.Name, dstPathLen);

                if (dstPathLen > FsPath.MaxLength)
                {
                    throw new ArgumentException();
                }

                rc = destFs.CreateFile(dstPath, dirEntry.Size, CreateFileOptions.None);
                if (rc.IsFailure()) return rc;

                rc = destFs.OpenFile(out dstFile, dstPath, OpenMode.Write);
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
