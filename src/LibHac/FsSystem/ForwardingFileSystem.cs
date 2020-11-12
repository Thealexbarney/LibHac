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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                BaseFileSystem?.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override Result DoCreateFile(U8Span path, long size, CreateFileOptions option) =>
            BaseFileSystem.Target.CreateFile(path, size, option);

        protected override Result DoDeleteFile(U8Span path) => BaseFileSystem.Target.DeleteFile(path);

        protected override Result DoCreateDirectory(U8Span path) => BaseFileSystem.Target.CreateDirectory(path);

        protected override Result DoDeleteDirectory(U8Span path) => BaseFileSystem.Target.DeleteDirectory(path);

        protected override Result DoDeleteDirectoryRecursively(U8Span path) =>
            BaseFileSystem.Target.DeleteDirectoryRecursively(path);

        protected override Result DoCleanDirectoryRecursively(U8Span path) =>
            BaseFileSystem.Target.CleanDirectoryRecursively(path);

        protected override Result DoRenameFile(U8Span oldPath, U8Span newPath) =>
            BaseFileSystem.Target.RenameFile(oldPath, newPath);

        protected override Result DoRenameDirectory(U8Span oldPath, U8Span newPath) =>
            BaseFileSystem.Target.RenameDirectory(oldPath, newPath);

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, U8Span path) =>
            BaseFileSystem.Target.GetEntryType(out entryType, path);

        protected override Result DoGetFreeSpaceSize(out long freeSpace, U8Span path) =>
            BaseFileSystem.Target.GetFreeSpaceSize(out freeSpace, path);

        protected override Result DoGetTotalSpaceSize(out long totalSpace, U8Span path) =>
            BaseFileSystem.Target.GetTotalSpaceSize(out totalSpace, path);

        protected override Result DoOpenFile(out IFile file, U8Span path, OpenMode mode) =>
            BaseFileSystem.Target.OpenFile(out file, path, mode);

        protected override Result DoOpenDirectory(out IDirectory directory, U8Span path, OpenDirectoryMode mode) =>
            BaseFileSystem.Target.OpenDirectory(out directory, path, mode);

        protected override Result DoCommit() => BaseFileSystem.Target.Commit();

        protected override Result DoCommitProvisionally(long counter) =>
            BaseFileSystem.Target.CommitProvisionally(counter);

        protected override Result DoRollback() => BaseFileSystem.Target.Rollback();

        protected override Result DoFlush() => BaseFileSystem.Target.Flush();

        protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, U8Span path) =>
            BaseFileSystem.Target.GetFileTimeStampRaw(out timeStamp, path);

        protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
            U8Span path) => BaseFileSystem.Target.QueryEntry(outBuffer, inBuffer, queryId, path);
    }
}
