using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem
{
    public class ForwardingFileSystem : IFileSystem
    {
        private ReferenceCountedDisposable<IFileSystem> BaseFileSystem { get; set; }

        public ForwardingFileSystem(ReferenceCountedDisposable<IFileSystem> baseFileSystem)
        {
            BaseFileSystem = baseFileSystem.AddReference();
        }

        protected ForwardingFileSystem(ref ReferenceCountedDisposable<IFileSystem> baseFileSystem)
        {
            BaseFileSystem = Shared.Move(ref baseFileSystem);
        }

        public override void Dispose()
        {
            BaseFileSystem?.Dispose();
            base.Dispose();
        }

        protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option) =>
            BaseFileSystem.Target.CreateFile(path, size, option);

        protected override Result DoDeleteFile(in Path path) => BaseFileSystem.Target.DeleteFile(path);

        protected override Result DoCreateDirectory(in Path path) => BaseFileSystem.Target.CreateDirectory(path);

        protected override Result DoDeleteDirectory(in Path path) => BaseFileSystem.Target.DeleteDirectory(path);

        protected override Result DoDeleteDirectoryRecursively(in Path path) =>
            BaseFileSystem.Target.DeleteDirectoryRecursively(path);

        protected override Result DoCleanDirectoryRecursively(in Path path) =>
            BaseFileSystem.Target.CleanDirectoryRecursively(path);

        protected override Result DoRenameFile(in Path currentPath, in Path newPath) =>
            BaseFileSystem.Target.RenameFile(currentPath, newPath);

        protected override Result DoRenameDirectory(in Path currentPath, in Path newPath) =>
            BaseFileSystem.Target.RenameDirectory(currentPath, newPath);

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path) =>
            BaseFileSystem.Target.GetEntryType(out entryType, path);

        protected override Result DoGetFreeSpaceSize(out long freeSpace, in Path path) =>
            BaseFileSystem.Target.GetFreeSpaceSize(out freeSpace, path);

        protected override Result DoGetTotalSpaceSize(out long totalSpace, in Path path) =>
            BaseFileSystem.Target.GetTotalSpaceSize(out totalSpace, path);

        protected override Result DoOpenFile(out IFile file, in Path path, OpenMode mode) =>
            BaseFileSystem.Target.OpenFile(out file, path, mode);

        protected override Result DoOpenDirectory(out IDirectory directory, in Path path, OpenDirectoryMode mode) =>
            BaseFileSystem.Target.OpenDirectory(out directory, path, mode);

        protected override Result DoCommit() => BaseFileSystem.Target.Commit();

        protected override Result DoCommitProvisionally(long counter) =>
            BaseFileSystem.Target.CommitProvisionally(counter);

        protected override Result DoRollback() => BaseFileSystem.Target.Rollback();

        protected override Result DoFlush() => BaseFileSystem.Target.Flush();

        protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in Path path) =>
            BaseFileSystem.Target.GetFileTimeStampRaw(out timeStamp, path);

        protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
            in Path path) => BaseFileSystem.Target.QueryEntry(outBuffer, inBuffer, queryId, path);
    }
}
