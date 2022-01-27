using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.Impl;

// ReSharper disable once InconsistentNaming
/// <summary>
/// Wraps an <see cref="IFile"/>, converting its returned <see cref="Result"/>s to different
/// <see cref="Result"/>s based on the <see cref="ConvertResult"/> function.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public abstract class IResultConvertFile : IFile
{
    protected UniqueRef<IFile> BaseFile;

    protected IResultConvertFile(ref UniqueRef<IFile> baseFile)
    {
        BaseFile = new UniqueRef<IFile>(ref baseFile);
    }

    public override void Dispose()
    {
        BaseFile.Destroy();
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
/// <summary>
/// Wraps an <see cref="IDirectory"/>, converting its returned <see cref="Result"/>s to different
/// <see cref="Result"/>s based on the <see cref="ConvertResult"/> function.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public abstract class IResultConvertDirectory : IDirectory
{
    protected UniqueRef<IDirectory> BaseDirectory;

    protected IResultConvertDirectory(ref UniqueRef<IDirectory> baseDirectory)
    {
        BaseDirectory = new UniqueRef<IDirectory>(ref baseDirectory);
    }

    public override void Dispose()
    {
        BaseDirectory.Destroy();
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
/// <summary>
/// Wraps an <see cref="IFileSystem"/>, converting its returned <see cref="Result"/>s to different
/// <see cref="Result"/>s based on the <see cref="ConvertResult"/> function.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public abstract class IResultConvertFileSystem : IFileSystem
{
    protected SharedRef<IFileSystem> BaseFileSystem;

    protected IResultConvertFileSystem(ref SharedRef<IFileSystem> baseFileSystem)
    {
        BaseFileSystem = SharedRef<IFileSystem>.CreateMove(ref baseFileSystem);
    }

    public override void Dispose()
    {
        BaseFileSystem.Destroy();
        base.Dispose();
    }

    protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option)
    {
        return ConvertResult(BaseFileSystem.Get.CreateFile(path, size, option));
    }

    protected override Result DoDeleteFile(in Path path)
    {
        return ConvertResult(BaseFileSystem.Get.DeleteFile(path));
    }

    protected override Result DoCreateDirectory(in Path path)
    {
        return ConvertResult(BaseFileSystem.Get.CreateDirectory(path));
    }

    protected override Result DoDeleteDirectory(in Path path)
    {
        return ConvertResult(BaseFileSystem.Get.DeleteDirectory(path));
    }

    protected override Result DoDeleteDirectoryRecursively(in Path path)
    {
        return ConvertResult(BaseFileSystem.Get.DeleteDirectoryRecursively(path));
    }

    protected override Result DoCleanDirectoryRecursively(in Path path)
    {
        return ConvertResult(BaseFileSystem.Get.CleanDirectoryRecursively(path));
    }

    protected override Result DoRenameFile(in Path currentPath, in Path newPath)
    {
        return ConvertResult(BaseFileSystem.Get.RenameFile(currentPath, newPath));
    }

    protected override Result DoRenameDirectory(in Path currentPath, in Path newPath)
    {
        return ConvertResult(BaseFileSystem.Get.RenameDirectory(currentPath, newPath));
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
    {
        return ConvertResult(BaseFileSystem.Get.GetEntryType(out entryType, path));
    }

    // Note: The original code uses templates to determine which type of IFile/IDirectory to return. To make things
    // easier in C# these two functions have been made abstract functions.
    protected abstract override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode);

    protected abstract override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
        OpenDirectoryMode mode);

    protected override Result DoCommit()
    {
        return ConvertResult(BaseFileSystem.Get.Commit());
    }

    protected override Result DoCommitProvisionally(long counter)
    {
        return ConvertResult(BaseFileSystem.Get.CommitProvisionally(counter));
    }

    protected override Result DoRollback()
    {
        return ConvertResult(BaseFileSystem.Get.Rollback());
    }

    protected override Result DoFlush()
    {
        return ConvertResult(BaseFileSystem.Get.Flush());
    }

    protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in Path path)
    {
        return ConvertResult(BaseFileSystem.Get.GetFileTimeStampRaw(out timeStamp, path));
    }

    protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
        in Path path)
    {
        return ConvertResult(BaseFileSystem.Get.QueryEntry(outBuffer, inBuffer, queryId, path));
    }

    protected override Result DoGetFreeSpaceSize(out long freeSpace, in Path path)
    {
        return ConvertResult(BaseFileSystem.Get.GetFreeSpaceSize(out freeSpace, path));
    }

    protected override Result DoGetTotalSpaceSize(out long totalSpace, in Path path)
    {
        return ConvertResult(BaseFileSystem.Get.GetTotalSpaceSize(out totalSpace, path));
    }

    protected abstract Result ConvertResult(Result result);
}