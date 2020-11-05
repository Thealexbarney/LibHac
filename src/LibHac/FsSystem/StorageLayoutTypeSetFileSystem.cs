using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem
{
    internal class StorageLayoutTypeSetFileSystem : IFileSystem
    {
        private ReferenceCountedDisposable<IFileSystem> BaseFileSystem { get; }
        private StorageType StorageFlag { get; }

        public StorageLayoutTypeSetFileSystem(ReferenceCountedDisposable<IFileSystem> baseFileSystem,
            StorageType storageFlag)
        {
            BaseFileSystem = baseFileSystem.AddReference();
            StorageFlag = storageFlag;
        }

        protected StorageLayoutTypeSetFileSystem(ref ReferenceCountedDisposable<IFileSystem> baseFileSystem,
            StorageType storageFlag)
        {
            BaseFileSystem = Shared.Move(ref baseFileSystem);
            StorageFlag = storageFlag;
        }

        public static ReferenceCountedDisposable<IFileSystem> CreateShared(
            ReferenceCountedDisposable<IFileSystem> baseFileSystem, StorageType storageFlag)
        {
            return new ReferenceCountedDisposable<IFileSystem>(
                new StorageLayoutTypeSetFileSystem(baseFileSystem, storageFlag));
        }

        public static ReferenceCountedDisposable<IFileSystem> CreateShared(
            ref ReferenceCountedDisposable<IFileSystem> baseFileSystem, StorageType storageFlag)
        {
            return new ReferenceCountedDisposable<IFileSystem>(
                new StorageLayoutTypeSetFileSystem(ref baseFileSystem, storageFlag));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
                BaseFileSystem?.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override Result DoCreateFile(U8Span path, long size, CreateFileOptions option)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.CreateFile(path, size, option);
        }

        protected override Result DoDeleteFile(U8Span path)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.DeleteFile(path);
        }

        protected override Result DoCreateDirectory(U8Span path)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.CreateDirectory(path);
        }

        protected override Result DoDeleteDirectory(U8Span path)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.DeleteDirectory(path);
        }

        protected override Result DoDeleteDirectoryRecursively(U8Span path)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.DeleteDirectoryRecursively(path);
        }

        protected override Result DoCleanDirectoryRecursively(U8Span path)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.CleanDirectoryRecursively(path);
        }

        protected override Result DoRenameFile(U8Span oldPath, U8Span newPath)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.RenameFile(oldPath, newPath);
        }

        protected override Result DoRenameDirectory(U8Span oldPath, U8Span newPath)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.RenameDirectory(oldPath, newPath);
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, U8Span path)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.GetEntryType(out entryType, path);
        }

        protected override Result DoGetFreeSpaceSize(out long freeSpace, U8Span path)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.GetFreeSpaceSize(out freeSpace, path);
        }

        protected override Result DoGetTotalSpaceSize(out long totalSpace, U8Span path)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.GetTotalSpaceSize(out totalSpace, path);
        }

        protected override Result DoOpenFile(out IFile file, U8Span path, OpenMode mode)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.OpenFile(out file, path, mode);
        }

        protected override Result DoOpenDirectory(out IDirectory directory, U8Span path, OpenDirectoryMode mode)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.OpenDirectory(out directory, path, mode);
        }

        protected override Result DoCommit()
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.Commit();
        }

        protected override Result DoCommitProvisionally(long counter)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.CommitProvisionally(counter);
        }

        protected override Result DoRollback()
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.Rollback();
        }

        protected override Result DoFlush()
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.Flush();
        }

        protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, U8Span path)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.GetFileTimeStampRaw(out timeStamp, path);
        }

        protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, U8Span path)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.QueryEntry(outBuffer, inBuffer, queryId, path);
        }
    }
}
