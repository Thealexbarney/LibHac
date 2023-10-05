using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem;

/// <summary>
/// An <see cref="IFile"/> wrapper that forwards all calls to the base <see cref="IFile"/>.
/// Meant for use as a base class when the derived class only needs to override some of the methods.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public class ForwardingFile : IFile
{
    protected UniqueRef<IFile> BaseFile;

    protected ForwardingFile(ref UniqueRef<IFile> baseFile)
    {
        BaseFile = new UniqueRef<IFile>(ref baseFile);
    }

    public override void Dispose()
    {
        BaseFile.Destroy();

        base.Dispose();
    }

    protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination, in ReadOption option) =>
        BaseFile.Get.Read(out bytesRead, offset, destination, in option);

    protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option) =>
        BaseFile.Get.Write(offset, source, in option);

    protected override Result DoFlush() => BaseFile.Get.Flush();

    protected override Result DoSetSize(long size) => BaseFile.Get.SetSize(size);

    protected override Result DoGetSize(out long size) => BaseFile.Get.GetSize(out size);

    protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer) => BaseFile.Get.OperateRange(outBuffer, operationId, offset, size, inBuffer);
}

/// <summary>
/// An <see cref="IDirectory"/> wrapper that forwards all calls to the base <see cref="IDirectory"/>.
/// Primarily meant for use as a base class when the derived class only needs to override some of the methods.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public class ForwardingDirectory : IDirectory
{
    protected UniqueRef<IDirectory> BaseDirectory;

    protected ForwardingDirectory(ref UniqueRef<IDirectory> baseDirectory)
    {
        BaseDirectory = new UniqueRef<IDirectory>(ref baseDirectory);
    }

    public override void Dispose()
    {
        BaseDirectory.Destroy();

        base.Dispose();
    }

    protected override Result DoRead(out long entriesRead, Span<DirectoryEntry> entryBuffer) =>
        BaseDirectory.Get.Read(out entriesRead, entryBuffer);

    protected override Result DoGetEntryCount(out long entryCount) => BaseDirectory.Get.GetEntryCount(out entryCount);
}

/// <summary>
/// An <see cref="IFileSystem"/> wrapper that forwards all calls to the base <see cref="IFileSystem"/>.
/// Primarily meant for use as a base class when the derived class only needs to override some of the methods.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public class ForwardingFileSystem : IFileSystem
{
    protected SharedRef<IFileSystem> BaseFileSystem;

    protected ForwardingFileSystem(ref SharedRef<IFileSystem> baseFileSystem)
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

    protected override Result DoGetFileSystemAttribute(out FileSystemAttribute outAttribute) =>
        BaseFileSystem.Get.GetFileSystemAttribute(out outAttribute);

    protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
        in Path path) => BaseFileSystem.Get.QueryEntry(outBuffer, inBuffer, queryId, in path);
}