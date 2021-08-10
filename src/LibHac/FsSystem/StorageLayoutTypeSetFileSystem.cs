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

        public override void Dispose()
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            BaseFileSystem?.Dispose();
            base.Dispose();
        }

        protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.CreateFile(path, size, option);
        }

        protected override Result DoDeleteFile(in Path path)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.DeleteFile(path);
        }

        protected override Result DoCreateDirectory(in Path path)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.CreateDirectory(path);
        }

        protected override Result DoDeleteDirectory(in Path path)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.DeleteDirectory(path);
        }

        protected override Result DoDeleteDirectoryRecursively(in Path path)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.DeleteDirectoryRecursively(path);
        }

        protected override Result DoCleanDirectoryRecursively(in Path path)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.CleanDirectoryRecursively(path);
        }

        protected override Result DoRenameFile(in Path currentPath, in Path newPath)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.RenameFile(currentPath, newPath);
        }

        protected override Result DoRenameDirectory(in Path currentPath, in Path newPath)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.RenameDirectory(currentPath, newPath);
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.GetEntryType(out entryType, path);
        }

        protected override Result DoGetFreeSpaceSize(out long freeSpace, in Path path)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.GetFreeSpaceSize(out freeSpace, path);
        }

        protected override Result DoGetTotalSpaceSize(out long totalSpace, in Path path)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.GetTotalSpaceSize(out totalSpace, path);
        }

        protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.OpenFile(ref outFile, path, mode);
        }

        protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
            OpenDirectoryMode mode)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.OpenDirectory(ref outDirectory, path, mode);
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

        protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in Path path)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.GetFileTimeStampRaw(out timeStamp, path);
        }

        protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
            in Path path)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseFileSystem.Target.QueryEntry(outBuffer, inBuffer, queryId, path);
        }
    }
}
