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
            BaseFileSystem.Target.CreateFile(in path, size, option);

        protected override Result DoDeleteFile(in Path path) => BaseFileSystem.Target.DeleteFile(in path);

        protected override Result DoCreateDirectory(in Path path) => BaseFileSystem.Target.CreateDirectory(in path);

        protected override Result DoDeleteDirectory(in Path path) => BaseFileSystem.Target.DeleteDirectory(in path);

        protected override Result DoDeleteDirectoryRecursively(in Path path) =>
            BaseFileSystem.Target.DeleteDirectoryRecursively(in path);

        protected override Result DoCleanDirectoryRecursively(in Path path) =>
            BaseFileSystem.Target.CleanDirectoryRecursively(in path);

        protected override Result DoRenameFile(in Path currentPath, in Path newPath) =>
            BaseFileSystem.Target.RenameFile(in currentPath, in newPath);

        protected override Result DoRenameDirectory(in Path currentPath, in Path newPath) =>
            BaseFileSystem.Target.RenameDirectory(in currentPath, in newPath);

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path) =>
            BaseFileSystem.Target.GetEntryType(out entryType, in path);

        protected override Result DoGetFreeSpaceSize(out long freeSpace, in Path path) =>
            BaseFileSystem.Target.GetFreeSpaceSize(out freeSpace, in path);

        protected override Result DoGetTotalSpaceSize(out long totalSpace, in Path path) =>
            BaseFileSystem.Target.GetTotalSpaceSize(out totalSpace, in path);

        protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode) =>
            BaseFileSystem.Target.OpenFile(ref outFile, in path, mode);

        protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
            OpenDirectoryMode mode) =>
            BaseFileSystem.Target.OpenDirectory(ref outDirectory, in path, mode);

        protected override Result DoCommit() => BaseFileSystem.Target.Commit();

        protected override Result DoCommitProvisionally(long counter) =>
            BaseFileSystem.Target.CommitProvisionally(counter);

        protected override Result DoRollback() => BaseFileSystem.Target.Rollback();

        protected override Result DoFlush() => BaseFileSystem.Target.Flush();

        protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in Path path) =>
            BaseFileSystem.Target.GetFileTimeStampRaw(out timeStamp, in path);

        protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
            in Path path) => BaseFileSystem.Target.QueryEntry(outBuffer, inBuffer, queryId, in path);
    }
}
