using System;
using System.Diagnostics;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class SubdirectoryFileSystem : FileSystemBase
    {
        private IFileSystem BaseFileSystem { get; }
        private U8String RootPath { get; set; }
        private bool PreserveUnc { get; }

        public static Result CreateNew(out SubdirectoryFileSystem created, IFileSystem baseFileSystem, U8Span rootPath, bool preserveUnc = false)
        {
            var obj = new SubdirectoryFileSystem(baseFileSystem, preserveUnc);
            Result rc = obj.Initialize(rootPath);

            if (rc.IsSuccess())
            {
                created = obj;
                return Result.Success;
            }

            obj.Dispose();
            created = default;
            return rc;
        }

        public SubdirectoryFileSystem(IFileSystem baseFileSystem, bool preserveUnc = false)
        {
            BaseFileSystem = baseFileSystem;
            PreserveUnc = preserveUnc;
        }

        private Result Initialize(U8Span rootPath)
        {
            if (StringUtils.GetLength(rootPath, PathTools.MaxPathLength + 1) > PathTools.MaxPathLength)
                return ResultFs.TooLongPath.Log();

            Span<byte> normalizedPath = stackalloc byte[PathTools.MaxPathLength + 2];

            Result rc = PathTool.Normalize(normalizedPath, out long normalizedPathLen, rootPath, PreserveUnc, false);
            if (rc.IsFailure()) return rc;

            // Ensure a trailing separator
            if (!PathTool.IsSeparator(normalizedPath[(int)normalizedPathLen - 1]))
            {
                Debug.Assert(normalizedPathLen + 2 <= normalizedPath.Length);

                normalizedPath[(int)normalizedPathLen] = StringTraits.DirectorySeparator;
                normalizedPath[(int)normalizedPathLen + 1] = StringTraits.NullTerminator;
                normalizedPathLen++;
            }

            var buffer = new byte[normalizedPathLen + 1];
            normalizedPath.Slice(0, (int)normalizedPathLen).CopyTo(buffer);
            RootPath = new U8String(buffer);

            return Result.Success;
        }

        private Result ResolveFullPath(Span<byte> outPath, U8Span relativePath)
        {
            if (RootPath.Length + StringUtils.GetLength(relativePath, PathTools.MaxPathLength + 1) > outPath.Length)
                return ResultFs.TooLongPath.Log();

            // Copy root path to the output
            RootPath.Value.CopyTo(outPath);

            // Copy the normalized relative path to the output
            return PathTool.Normalize(outPath.Slice(RootPath.Length - 2), out _, relativePath, PreserveUnc, false);
        }

        protected override Result CreateDirectoryImpl(string path)
        {
            var u8Path = new U8String(path);

            Span<byte> fullPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Result rc = ResolveFullPath(fullPath, u8Path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.CreateDirectory(StringUtils.Utf8ZToString(fullPath));
        }

        protected override Result CreateFileImpl(string path, long size, CreateFileOptions options)
        {
            var u8Path = new U8String(path);

            Span<byte> fullPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Result rc = ResolveFullPath(fullPath, u8Path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.CreateFile(StringUtils.Utf8ZToString(fullPath), size, options);
        }

        protected override Result DeleteDirectoryImpl(string path)
        {
            var u8Path = new U8String(path);

            Span<byte> fullPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Result rc = ResolveFullPath(fullPath, u8Path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.DeleteDirectory(StringUtils.Utf8ZToString(fullPath));
        }

        protected override Result DeleteDirectoryRecursivelyImpl(string path)
        {
            var u8Path = new U8String(path);

            Span<byte> fullPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Result rc = ResolveFullPath(fullPath, u8Path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.DeleteDirectoryRecursively(StringUtils.Utf8ZToString(fullPath));
        }

        protected override Result CleanDirectoryRecursivelyImpl(string path)
        {
            var u8Path = new U8String(path);

            Span<byte> fullPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Result rc = ResolveFullPath(fullPath, u8Path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.CleanDirectoryRecursively(StringUtils.Utf8ZToString(fullPath));
        }

        protected override Result DeleteFileImpl(string path)
        {
            var u8Path = new U8String(path);

            Span<byte> fullPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Result rc = ResolveFullPath(fullPath, u8Path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.DeleteFile(StringUtils.Utf8ZToString(fullPath));
        }

        protected override Result OpenDirectoryImpl(out IDirectory directory, string path, OpenDirectoryMode mode)
        {
            directory = default;
            var u8Path = new U8String(path);

            Span<byte> fullPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Result rc = ResolveFullPath(fullPath, u8Path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.OpenDirectory(out directory, StringUtils.Utf8ZToString(fullPath), mode);
        }

        protected override Result OpenFileImpl(out IFile file, string path, OpenMode mode)
        {
            file = default;
            var u8Path = new U8String(path);

            Span<byte> fullPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Result rc = ResolveFullPath(fullPath, u8Path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.OpenFile(out file, StringUtils.Utf8ZToString(fullPath), mode);
        }

        protected override Result RenameDirectoryImpl(string oldPath, string newPath)
        {
            var u8OldPath = new U8String(oldPath);
            var u8NewPath = new U8String(newPath);

            Span<byte> fullOldPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Span<byte> fullNewPath = stackalloc byte[PathTools.MaxPathLength + 1];

            Result rc = ResolveFullPath(fullOldPath, u8OldPath);
            if (rc.IsFailure()) return rc;

            rc = ResolveFullPath(fullNewPath, u8NewPath);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.RenameDirectory(StringUtils.Utf8ZToString(fullOldPath), StringUtils.Utf8ZToString(fullNewPath));
        }

        protected override Result RenameFileImpl(string oldPath, string newPath)
        {
            var u8OldPath = new U8String(oldPath);
            var u8NewPath = new U8String(newPath);

            Span<byte> fullOldPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Span<byte> fullNewPath = stackalloc byte[PathTools.MaxPathLength + 1];

            Result rc = ResolveFullPath(fullOldPath, u8OldPath);
            if (rc.IsFailure()) return rc;

            rc = ResolveFullPath(fullNewPath, u8NewPath);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.RenameFile(StringUtils.Utf8ZToString(fullOldPath), StringUtils.Utf8ZToString(fullNewPath));
        }

        protected override Result GetEntryTypeImpl(out DirectoryEntryType entryType, string path)
        {
            entryType = default;
            var u8Path = new U8String(path);

            Span<byte> fullPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Result rc = ResolveFullPath(fullPath, u8Path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.GetEntryType(out entryType, StringUtils.Utf8ZToString(fullPath));
        }

        protected override Result CommitImpl()
        {
            return BaseFileSystem.Commit();
        }

        protected override Result GetFreeSpaceSizeImpl(out long freeSpace, string path)
        {
            freeSpace = default;
            var u8Path = new U8String(path);

            Span<byte> fullPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Result rc = ResolveFullPath(fullPath, u8Path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.GetFreeSpaceSize(out freeSpace, StringUtils.Utf8ZToString(fullPath));
        }

        protected override Result GetTotalSpaceSizeImpl(out long totalSpace, string path)
        {
            totalSpace = default;
            var u8Path = new U8String(path);

            Span<byte> fullPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Result rc = ResolveFullPath(fullPath, u8Path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.GetTotalSpaceSize(out totalSpace, StringUtils.Utf8ZToString(fullPath));
        }

        protected override Result GetFileTimeStampRawImpl(out FileTimeStampRaw timeStamp, string path)
        {
            timeStamp = default;
            var u8Path = new U8String(path);

            Span<byte> fullPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Result rc = ResolveFullPath(fullPath, u8Path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.GetFileTimeStampRaw(out timeStamp, StringUtils.Utf8ZToString(fullPath));
        }

        protected override Result QueryEntryImpl(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, string path)
        {
            var u8Path = new U8String(path);

            Span<byte> fullPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Result rc = ResolveFullPath(fullPath, u8Path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.QueryEntry(outBuffer, inBuffer, queryId, StringUtils.Utf8ZToString(fullPath));
        }
    }
}
