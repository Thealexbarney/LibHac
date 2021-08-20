using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem
{
    public class ForwardingFileSystem : IFileSystem
    {
        protected SharedRef<IFileSystem> BaseFileSystem;

        public ForwardingFileSystem(ref SharedRef<IFileSystem> baseFileSystem)
        {
            BaseFileSystem = SharedRef<IFileSystem>.CreateMove(ref baseFileSystem);
        }

        public override void Dispose()
        {
            BaseFileSystem.Destroy();
            base.Dispose();
        }

        protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option) =>
            BaseFileSystem.Get.CreateFile(in path, size, option);

        protected override Result DoDeleteFile(in Path path) => BaseFileSystem.Get.DeleteFile(in path);

        protected override Result DoCreateDirectory(in Path path) => BaseFileSystem.Get.CreateDirectory(in path);

        protected override Result DoDeleteDirectory(in Path path) => BaseFileSystem.Get.DeleteDirectory(in path);

        protected override Result DoDeleteDirectoryRecursively(in Path path) =>
            BaseFileSystem.Get.DeleteDirectoryRecursively(in path);

        protected override Result DoCleanDirectoryRecursively(in Path path) =>
            BaseFileSystem.Get.CleanDirectoryRecursively(in path);

        protected override Result DoRenameFile(in Path currentPath, in Path newPath) =>
            BaseFileSystem.Get.RenameFile(in currentPath, in newPath);

        protected override Result DoRenameDirectory(in Path currentPath, in Path newPath) =>
            BaseFileSystem.Get.RenameDirectory(in currentPath, in newPath);

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path) =>
            BaseFileSystem.Get.GetEntryType(out entryType, in path);

        protected override Result DoGetFreeSpaceSize(out long freeSpace, in Path path) =>
            BaseFileSystem.Get.GetFreeSpaceSize(out freeSpace, in path);

        protected override Result DoGetTotalSpaceSize(out long totalSpace, in Path path) =>
            BaseFileSystem.Get.GetTotalSpaceSize(out totalSpace, in path);

        protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode) =>
            BaseFileSystem.Get.OpenFile(ref outFile, in path, mode);

        protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
            OpenDirectoryMode mode) =>
            BaseFileSystem.Get.OpenDirectory(ref outDirectory, in path, mode);

        protected override Result DoCommit() => BaseFileSystem.Get.Commit();

        protected override Result DoCommitProvisionally(long counter) =>
            BaseFileSystem.Get.CommitProvisionally(counter);

        protected override Result DoRollback() => BaseFileSystem.Get.Rollback();

        protected override Result DoFlush() => BaseFileSystem.Get.Flush();

        protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in Path path) =>
            BaseFileSystem.Get.GetFileTimeStampRaw(out timeStamp, in path);

        protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
            in Path path) => BaseFileSystem.Get.QueryEntry(outBuffer, inBuffer, queryId, in path);
    }
}
