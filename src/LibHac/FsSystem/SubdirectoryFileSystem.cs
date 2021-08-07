using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem
{
    /// <summary>
    /// An <see cref="IFileSystem"/> that uses a directory of another <see cref="IFileSystem"/> as its root directory.
    /// </summary>
    /// <remarks>Based on FS 12.0.3 (nnSdk 12.3.1)</remarks>
    public class SubdirectoryFileSystem : IFileSystem
    {
        private IFileSystem _baseFileSystem;
        private ReferenceCountedDisposable<IFileSystem> _baseFileSystemShared;
        private Path.Stored _rootPath;

        public SubdirectoryFileSystem(IFileSystem baseFileSystem)
        {
            _baseFileSystem = baseFileSystem;
        }

        public SubdirectoryFileSystem(ref ReferenceCountedDisposable<IFileSystem> baseFileSystem)
        {
            _baseFileSystemShared = Shared.Move(ref baseFileSystem);
            _baseFileSystem = _baseFileSystemShared.Target;
        }

        public override void Dispose()
        {
            ReferenceCountedDisposable<IFileSystem> sharedFs = Shared.Move(ref _baseFileSystemShared);
            sharedFs?.Dispose();
            base.Dispose();
        }

        public Result Initialize(in Path rootPath)
        {
            return _rootPath.Initialize(in rootPath);
        }

        private Result ResolveFullPath(ref Path outPath, in Path relativePath)
        {
            Path rootPath = _rootPath.GetPath();
            return outPath.Combine(in rootPath, in relativePath);
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
        {
            UnsafeHelpers.SkipParamInit(out entryType);

            var fullPath = new Path();
            Result rc = ResolveFullPath(ref fullPath, in path);
            if (rc.IsFailure()) return rc;

            rc = _baseFileSystem.GetEntryType(out entryType, in fullPath);
            if (rc.IsFailure()) return rc;

            fullPath.Dispose();
            return Result.Success;
        }

        protected override Result DoGetFreeSpaceSize(out long freeSpace, in Path path)
        {
            UnsafeHelpers.SkipParamInit(out freeSpace);

            var fullPath = new Path();
            Result rc = ResolveFullPath(ref fullPath, in path);
            if (rc.IsFailure()) return rc;

            rc = _baseFileSystem.GetFreeSpaceSize(out freeSpace, in fullPath);
            if (rc.IsFailure()) return rc;

            fullPath.Dispose();
            return Result.Success;
        }

        protected override Result DoGetTotalSpaceSize(out long totalSpace, in Path path)
        {
            UnsafeHelpers.SkipParamInit(out totalSpace);

            var fullPath = new Path();
            Result rc = ResolveFullPath(ref fullPath, in path);
            if (rc.IsFailure()) return rc;

            rc = _baseFileSystem.GetTotalSpaceSize(out totalSpace, in fullPath);
            if (rc.IsFailure()) return rc;

            fullPath.Dispose();
            return Result.Success;
        }

        protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in Path path)
        {
            UnsafeHelpers.SkipParamInit(out timeStamp);

            var fullPath = new Path();
            Result rc = ResolveFullPath(ref fullPath, in path);
            if (rc.IsFailure()) return rc;

            rc = _baseFileSystem.GetFileTimeStampRaw(out timeStamp, in fullPath);
            if (rc.IsFailure()) return rc;

            fullPath.Dispose();
            return Result.Success;
        }

        protected override Result DoOpenFile(out IFile file, in Path path, OpenMode mode)
        {
            UnsafeHelpers.SkipParamInit(out file);

            var fullPath = new Path();
            Result rc = ResolveFullPath(ref fullPath, in path);
            if (rc.IsFailure()) return rc;

            rc = _baseFileSystem.OpenFile(out file, in fullPath, mode);
            if (rc.IsFailure()) return rc;

            fullPath.Dispose();
            return Result.Success;
        }

        protected override Result DoOpenDirectory(out IDirectory directory, in Path path, OpenDirectoryMode mode)
        {
            UnsafeHelpers.SkipParamInit(out directory);

            var fullPath = new Path();
            Result rc = ResolveFullPath(ref fullPath, in path);
            if (rc.IsFailure()) return rc;

            rc = _baseFileSystem.OpenDirectory(out directory, in fullPath, mode);
            if (rc.IsFailure()) return rc;

            fullPath.Dispose();
            return Result.Success;
        }

        protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option)
        {
            var fullPath = new Path();
            Result rc = ResolveFullPath(ref fullPath, in path);
            if (rc.IsFailure()) return rc;

            rc = _baseFileSystem.CreateFile(in fullPath, size, option);
            if (rc.IsFailure()) return rc;

            fullPath.Dispose();
            return Result.Success;
        }

        protected override Result DoDeleteFile(in Path path)
        {
            var fullPath = new Path();
            Result rc = ResolveFullPath(ref fullPath, in path);
            if (rc.IsFailure()) return rc;

            rc = _baseFileSystem.DeleteFile(in fullPath);
            if (rc.IsFailure()) return rc;

            fullPath.Dispose();
            return Result.Success;
        }

        protected override Result DoCreateDirectory(in Path path)
        {
            var fullPath = new Path();
            Result rc = ResolveFullPath(ref fullPath, in path);
            if (rc.IsFailure()) return rc;

            rc = _baseFileSystem.CreateDirectory(in fullPath);
            if (rc.IsFailure()) return rc;

            fullPath.Dispose();
            return Result.Success;
        }

        protected override Result DoDeleteDirectory(in Path path)
        {
            var fullPath = new Path();
            Result rc = ResolveFullPath(ref fullPath, in path);
            if (rc.IsFailure()) return rc;

            rc = _baseFileSystem.DeleteDirectory(in fullPath);
            if (rc.IsFailure()) return rc;

            fullPath.Dispose();
            return Result.Success;
        }

        protected override Result DoDeleteDirectoryRecursively(in Path path)
        {
            var fullPath = new Path();
            Result rc = ResolveFullPath(ref fullPath, in path);
            if (rc.IsFailure()) return rc;

            rc = _baseFileSystem.DeleteDirectoryRecursively(in fullPath);
            if (rc.IsFailure()) return rc;

            fullPath.Dispose();
            return Result.Success;
        }

        protected override Result DoCleanDirectoryRecursively(in Path path)
        {
            var fullPath = new Path();
            Result rc = ResolveFullPath(ref fullPath, in path);
            if (rc.IsFailure()) return rc;

            rc = _baseFileSystem.CleanDirectoryRecursively(in fullPath);
            if (rc.IsFailure()) return rc;

            fullPath.Dispose();
            return Result.Success;
        }

        protected override Result DoRenameFile(in Path currentPath, in Path newPath)
        {
            var currentFullPath = new Path();
            Result rc = ResolveFullPath(ref currentFullPath, in currentPath);
            if (rc.IsFailure()) return rc;

            var newFullPath = new Path();
            rc = ResolveFullPath(ref newFullPath, in newPath);
            if (rc.IsFailure()) return rc;

            rc = _baseFileSystem.RenameFile(in currentFullPath, in newFullPath);
            if (rc.IsFailure()) return rc;

            currentFullPath.Dispose();
            newFullPath.Dispose();
            return Result.Success;
        }

        protected override Result DoRenameDirectory(in Path currentPath, in Path newPath)
        {
            var currentFullPath = new Path();
            Result rc = ResolveFullPath(ref currentFullPath, in currentPath);
            if (rc.IsFailure()) return rc;

            var newFullPath = new Path();
            rc = ResolveFullPath(ref newFullPath, in newPath);
            if (rc.IsFailure()) return rc;

            rc = _baseFileSystem.RenameDirectory(in currentFullPath, in newFullPath);
            if (rc.IsFailure()) return rc;

            currentFullPath.Dispose();
            newFullPath.Dispose();
            return Result.Success;
        }

        protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
            in Path path)
        {
            var fullPath = new Path();
            Result rc = ResolveFullPath(ref fullPath, in path);
            if (rc.IsFailure()) return rc;

            rc = _baseFileSystem.QueryEntry(outBuffer, inBuffer, queryId, in fullPath);
            if (rc.IsFailure()) return rc;

            fullPath.Dispose();
            return Result.Success;
        }

        protected override Result DoCommit()
        {
            return _baseFileSystem.Commit();
        }

        protected override Result DoCommitProvisionally(long counter)
        {
            return _baseFileSystem.CommitProvisionally(counter);
        }

        protected override Result DoRollback()
        {
            return _baseFileSystem.Rollback();
        }
    }
}
