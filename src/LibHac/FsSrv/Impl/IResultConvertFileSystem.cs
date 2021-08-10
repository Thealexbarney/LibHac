using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.Impl
{
    // ReSharper disable once InconsistentNaming
    public abstract class IResultConvertFile : IFile
    {
        protected UniqueRef<IFile> BaseFile;

        protected IResultConvertFile(ref UniqueRef<IFile> baseFile)
        {
            BaseFile = new UniqueRef<IFile>(ref baseFile);
        }

        public override void Dispose()
        {
            BaseFile.Dispose();
            base.Dispose();
        }

        protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination, in ReadOption option)
        {
            return ConvertResult(BaseFile.Get.Read(out bytesRead, offset, destination, option));
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
        {
            return ConvertResult(BaseFile.Get.Write(offset, source, option));
        }

        protected override Result DoFlush()
        {
            return ConvertResult(BaseFile.Get.Flush());
        }

        protected override Result DoSetSize(long size)
        {
            return ConvertResult(BaseFile.Get.SetSize(size));
        }

        protected override Result DoGetSize(out long size)
        {
            return ConvertResult(BaseFile.Get.GetSize(out size));
        }

        protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            return ConvertResult(BaseFile.Get.OperateRange(outBuffer, operationId, offset, size, inBuffer));
        }

        protected abstract Result ConvertResult(Result result);
    }

    // ReSharper disable once InconsistentNaming
    public abstract class IResultConvertDirectory : IDirectory
    {
        protected UniqueRef<IDirectory> BaseDirectory;

        protected IResultConvertDirectory(ref UniqueRef<IDirectory> baseDirectory)
        {
            BaseDirectory = new UniqueRef<IDirectory>(ref baseDirectory);
        }

        public override void Dispose()
        {
            BaseDirectory.Dispose();
            base.Dispose();
        }

        protected override Result DoRead(out long entriesRead, Span<DirectoryEntry> entryBuffer)
        {
            return ConvertResult(BaseDirectory.Get.Read(out entriesRead, entryBuffer));
        }

        protected override Result DoGetEntryCount(out long entryCount)
        {
            return ConvertResult(BaseDirectory.Get.GetEntryCount(out entryCount));
        }

        protected abstract Result ConvertResult(Result result);
    }

    // ReSharper disable once InconsistentNaming
    public abstract class IResultConvertFileSystem : IFileSystem
    {
        protected ReferenceCountedDisposable<IFileSystem> BaseFileSystem;

        protected IResultConvertFileSystem(ref ReferenceCountedDisposable<IFileSystem> baseFileSystem)
        {
            BaseFileSystem = Shared.Move(ref baseFileSystem);
        }

        public override void Dispose()
        {
            BaseFileSystem?.Dispose();
            BaseFileSystem = null;
            base.Dispose();
        }

        protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option)
        {
            return ConvertResult(BaseFileSystem.Target.CreateFile(path, size, option));
        }

        protected override Result DoDeleteFile(in Path path)
        {
            return ConvertResult(BaseFileSystem.Target.DeleteFile(path));
        }

        protected override Result DoCreateDirectory(in Path path)
        {
            return ConvertResult(BaseFileSystem.Target.CreateDirectory(path));
        }

        protected override Result DoDeleteDirectory(in Path path)
        {
            return ConvertResult(BaseFileSystem.Target.DeleteDirectory(path));
        }

        protected override Result DoDeleteDirectoryRecursively(in Path path)
        {
            return ConvertResult(BaseFileSystem.Target.DeleteDirectoryRecursively(path));
        }

        protected override Result DoCleanDirectoryRecursively(in Path path)
        {
            return ConvertResult(BaseFileSystem.Target.CleanDirectoryRecursively(path));
        }

        protected override Result DoRenameFile(in Path currentPath, in Path newPath)
        {
            return ConvertResult(BaseFileSystem.Target.RenameFile(currentPath, newPath));
        }

        protected override Result DoRenameDirectory(in Path currentPath, in Path newPath)
        {
            return ConvertResult(BaseFileSystem.Target.RenameDirectory(currentPath, newPath));
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
        {
            return ConvertResult(BaseFileSystem.Target.GetEntryType(out entryType, path));
        }

        protected abstract override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode);

        protected abstract override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
            OpenDirectoryMode mode);

        protected override Result DoCommit()
        {
            return ConvertResult(BaseFileSystem.Target.Commit());
        }

        protected override Result DoCommitProvisionally(long counter)
        {
            return ConvertResult(BaseFileSystem.Target.CommitProvisionally(counter));
        }

        protected override Result DoRollback()
        {
            return ConvertResult(BaseFileSystem.Target.Rollback());
        }

        protected override Result DoFlush()
        {
            return ConvertResult(BaseFileSystem.Target.Flush());
        }

        protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in Path path)
        {
            return ConvertResult(BaseFileSystem.Target.GetFileTimeStampRaw(out timeStamp, path));
        }

        protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
            in Path path)
        {
            return ConvertResult(BaseFileSystem.Target.QueryEntry(outBuffer, inBuffer, queryId, path));
        }

        protected override Result DoGetFreeSpaceSize(out long freeSpace, in Path path)
        {
            return ConvertResult(BaseFileSystem.Target.GetFreeSpaceSize(out freeSpace, path));
        }

        protected override Result DoGetTotalSpaceSize(out long totalSpace, in Path path)
        {
            return ConvertResult(BaseFileSystem.Target.GetTotalSpaceSize(out totalSpace, path));
        }

        protected abstract Result ConvertResult(Result result);
    }
}