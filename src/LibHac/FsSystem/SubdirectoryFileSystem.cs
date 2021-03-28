using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;

namespace LibHac.FsSystem
{
    public class SubdirectoryFileSystem : IFileSystem
    {
        private IFileSystem BaseFileSystem { get; }
        private ReferenceCountedDisposable<IFileSystem> BaseFileSystemShared { get; }
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
            UnsafeHelpers.SkipParamInit(out created);
            return rc;
        }

        public SubdirectoryFileSystem(IFileSystem baseFileSystem, bool preserveUnc = false)
        {
            BaseFileSystem = baseFileSystem;
            PreserveUnc = preserveUnc;
        }

        public SubdirectoryFileSystem(ref ReferenceCountedDisposable<IFileSystem> baseFileSystem, bool preserveUnc = false)
        {
            BaseFileSystemShared = Shared.Move(ref baseFileSystem);
            BaseFileSystem = BaseFileSystemShared.Target;
            PreserveUnc = preserveUnc;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                BaseFileSystemShared?.Dispose();
            }

            base.Dispose(disposing);
        }

        public Result Initialize(U8Span rootPath)
        {
            if (StringUtils.GetLength(rootPath, PathTools.MaxPathLength + 1) > PathTools.MaxPathLength)
                return ResultFs.TooLongPath.Log();

            Span<byte> normalizedPath = stackalloc byte[PathTools.MaxPathLength + 2];

            Result rc = PathNormalizer.Normalize(normalizedPath, out long normalizedPathLen, rootPath, PreserveUnc, false);
            if (rc.IsFailure()) return rc;

            // Ensure a trailing separator
            if (!PathNormalizer.IsSeparator(normalizedPath[(int)normalizedPathLen - 1]))
            {
                Debug.Assert(normalizedPathLen + 2 <= normalizedPath.Length);

                normalizedPath[(int)normalizedPathLen] = StringTraits.DirectorySeparator;
                normalizedPath[(int)normalizedPathLen + 1] = StringTraits.NullTerminator;
                normalizedPathLen++;
            }

            byte[] buffer = new byte[normalizedPathLen + 1];
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
            return PathNormalizer.Normalize(outPath.Slice(RootPath.Length - 2), out _, relativePath, PreserveUnc, false);
        }

        protected override Result DoCreateDirectory(U8Span path)
        {
            Span<byte> fullPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Result rc = ResolveFullPath(fullPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.CreateDirectory(new U8Span(fullPath));
        }

        protected override Result DoCreateFile(U8Span path, long size, CreateFileOptions options)
        {
            Span<byte> fullPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Result rc = ResolveFullPath(fullPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.CreateFile(new U8Span(fullPath), size, options);
        }

        protected override Result DoDeleteDirectory(U8Span path)
        {
            Span<byte> fullPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Result rc = ResolveFullPath(fullPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.DeleteDirectory(new U8Span(fullPath));
        }

        protected override Result DoDeleteDirectoryRecursively(U8Span path)
        {
            Span<byte> fullPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Result rc = ResolveFullPath(fullPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.DeleteDirectoryRecursively(new U8Span(fullPath));
        }

        protected override Result DoCleanDirectoryRecursively(U8Span path)
        {
            Span<byte> fullPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Result rc = ResolveFullPath(fullPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.CleanDirectoryRecursively(new U8Span(fullPath));
        }

        protected override Result DoDeleteFile(U8Span path)
        {
            Span<byte> fullPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Result rc = ResolveFullPath(fullPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.DeleteFile(new U8Span(fullPath));
        }

        protected override Result DoOpenDirectory(out IDirectory directory, U8Span path, OpenDirectoryMode mode)
        {
            UnsafeHelpers.SkipParamInit(out directory);

            Span<byte> fullPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Result rc = ResolveFullPath(fullPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.OpenDirectory(out directory, new U8Span(fullPath), mode);
        }

        protected override Result DoOpenFile(out IFile file, U8Span path, OpenMode mode)
        {
            UnsafeHelpers.SkipParamInit(out file);

            Span<byte> fullPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Result rc = ResolveFullPath(fullPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.OpenFile(out file, new U8Span(fullPath), mode);
        }

        protected override Result DoRenameDirectory(U8Span oldPath, U8Span newPath)
        {
            Span<byte> fullOldPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Span<byte> fullNewPath = stackalloc byte[PathTools.MaxPathLength + 1];

            Result rc = ResolveFullPath(fullOldPath, oldPath);
            if (rc.IsFailure()) return rc;

            rc = ResolveFullPath(fullNewPath, newPath);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.RenameDirectory(new U8Span(fullOldPath), new U8Span(fullNewPath));
        }

        protected override Result DoRenameFile(U8Span oldPath, U8Span newPath)
        {
            Span<byte> fullOldPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Span<byte> fullNewPath = stackalloc byte[PathTools.MaxPathLength + 1];

            Result rc = ResolveFullPath(fullOldPath, oldPath);
            if (rc.IsFailure()) return rc;

            rc = ResolveFullPath(fullNewPath, newPath);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.RenameFile(new U8Span(fullOldPath), new U8Span(fullNewPath));
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out entryType);

            Unsafe.SkipInit(out FsPath fullPath);

            Result rc = ResolveFullPath(fullPath.Str, path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.GetEntryType(out entryType, fullPath);
        }

        protected override Result DoCommit()
        {
            return BaseFileSystem.Commit();
        }

        protected override Result DoCommitProvisionally(long counter)
        {
            return BaseFileSystem.CommitProvisionally(counter);
        }

        protected override Result DoRollback()
        {
            return BaseFileSystem.Rollback();
        }

        protected override Result DoGetFreeSpaceSize(out long freeSpace, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out freeSpace);

            Span<byte> fullPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Result rc = ResolveFullPath(fullPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.GetFreeSpaceSize(out freeSpace, new U8Span(fullPath));
        }

        protected override Result DoGetTotalSpaceSize(out long totalSpace, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out totalSpace);

            Span<byte> fullPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Result rc = ResolveFullPath(fullPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.GetTotalSpaceSize(out totalSpace, new U8Span(fullPath));
        }

        protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out timeStamp);

            Span<byte> fullPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Result rc = ResolveFullPath(fullPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.GetFileTimeStampRaw(out timeStamp, new U8Span(fullPath));
        }

        protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
            U8Span path)
        {
            Span<byte> fullPath = stackalloc byte[PathTools.MaxPathLength + 1];
            Result rc = ResolveFullPath(fullPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFileSystem.QueryEntry(outBuffer, inBuffer, queryId, new U8Span(fullPath));
        }
    }
}
