using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem;

// ReSharper disable once InconsistentNaming
/// <summary>
/// Wraps an <see cref="IFile"/>, converting its returned <see cref="Result"/>s to different
/// <see cref="Result"/>s based on the <see cref="ConvertResult"/> function.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
public abstract class IResultConvertFile : IFile
{
    private UniqueRef<IFile> _baseFile;

    protected IResultConvertFile(ref UniqueRef<IFile> baseFile)
    {
        _baseFile = new UniqueRef<IFile>(ref baseFile);
    }

    public override void Dispose()
    {
        _baseFile.Destroy();

        base.Dispose();
    }

    protected abstract Result ConvertResult(Result result);

    protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination, in ReadOption option)
    {
        return ConvertResult(_baseFile.Get.Read(out bytesRead, offset, destination, in option)).Ret();
    }

    protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
    {
        return ConvertResult(_baseFile.Get.Write(offset, source, in option)).Ret();
    }

    protected override Result DoFlush()
    {
        return ConvertResult(_baseFile.Get.Flush()).Ret();
    }

    protected override Result DoSetSize(long size)
    {
        return ConvertResult(_baseFile.Get.SetSize(size)).Ret();
    }

    protected override Result DoGetSize(out long size)
    {
        return ConvertResult(_baseFile.Get.GetSize(out size)).Ret();
    }

    protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        return ConvertResult(_baseFile.Get.OperateRange(outBuffer, operationId, offset, size, inBuffer)).Ret();
    }
}

// ReSharper disable once InconsistentNaming
/// <summary>
/// Wraps an <see cref="IDirectory"/>, converting its returned <see cref="Result"/>s to different
/// <see cref="Result"/>s based on the <see cref="ConvertResult"/> function.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
public abstract class IResultConvertDirectory : IDirectory
{
    private UniqueRef<IDirectory> _baseDirectory;

    protected IResultConvertDirectory(ref UniqueRef<IDirectory> baseDirectory)
    {
        _baseDirectory = new UniqueRef<IDirectory>(ref baseDirectory);
    }

    public override void Dispose()
    {
        _baseDirectory.Destroy();

        base.Dispose();
    }

    protected abstract Result ConvertResult(Result result);

    protected override Result DoRead(out long entriesRead, Span<DirectoryEntry> entryBuffer)
    {
        return ConvertResult(_baseDirectory.Get.Read(out entriesRead, entryBuffer)).Ret();
    }

    protected override Result DoGetEntryCount(out long entryCount)
    {
        return ConvertResult(_baseDirectory.Get.GetEntryCount(out entryCount)).Ret();
    }
}

// ReSharper disable once InconsistentNaming
/// <summary>
/// Wraps an <see cref="IFileSystem"/>, converting its returned <see cref="Result"/>s to different
/// <see cref="Result"/>s based on the <see cref="ConvertResult"/> function.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
public abstract class IResultConvertFileSystem<T> : ISaveDataFileSystem where T : IFileSystem
{
    private SharedRef<T> _baseFileSystem;

    protected IResultConvertFileSystem(ref SharedRef<T> baseFileSystem)
    {
        _baseFileSystem = SharedRef<T>.CreateMove(ref baseFileSystem);
    }

    public override void Dispose()
    {
        _baseFileSystem.Destroy();

        base.Dispose();
    }

    protected T GetFileSystem() => _baseFileSystem.Get;

    protected abstract Result ConvertResult(Result result);

    protected override Result DoCreateFile(ref readonly Path path, long size, CreateFileOptions option)
    {
        return ConvertResult(_baseFileSystem.Get.CreateFile(in path, size)).Ret();
    }

    protected override Result DoDeleteFile(ref readonly Path path)
    {
        return ConvertResult(_baseFileSystem.Get.DeleteFile(in path)).Ret();
    }

    protected override Result DoCreateDirectory(ref readonly Path path)
    {
        return ConvertResult(_baseFileSystem.Get.CreateDirectory(in path)).Ret();
    }

    protected override Result DoDeleteDirectory(ref readonly Path path)
    {
        return ConvertResult(_baseFileSystem.Get.DeleteDirectory(in path)).Ret();
    }

    protected override Result DoDeleteDirectoryRecursively(ref readonly Path path)
    {
        return ConvertResult(_baseFileSystem.Get.DeleteDirectoryRecursively(in path)).Ret();
    }

    protected override Result DoCleanDirectoryRecursively(ref readonly Path path)
    {
        return ConvertResult(_baseFileSystem.Get.CleanDirectoryRecursively(in path)).Ret();
    }

    protected override Result DoRenameFile(ref readonly Path currentPath, ref readonly Path newPath)
    {
        return ConvertResult(_baseFileSystem.Get.RenameFile(in currentPath, in newPath)).Ret();
    }

    protected override Result DoRenameDirectory(ref readonly Path currentPath, ref readonly Path newPath)
    {
        return ConvertResult(_baseFileSystem.Get.RenameDirectory(in currentPath, in newPath)).Ret();
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, ref readonly Path path)
    {
        return ConvertResult(_baseFileSystem.Get.GetEntryType(out entryType, in path)).Ret();
    }

    protected override Result DoCommit()
    {
        return ConvertResult(_baseFileSystem.Get.Commit()).Ret();
    }

    protected override Result DoCommitProvisionally(long counter)
    {
        return ConvertResult(_baseFileSystem.Get.CommitProvisionally(counter)).Ret();
    }

    protected override Result DoRollback()
    {
        return ConvertResult(_baseFileSystem.Get.Rollback()).Ret();
    }

    protected override Result DoFlush()
    {
        return ConvertResult(_baseFileSystem.Get.Flush()).Ret();
    }

    protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, ref readonly Path path)
    {
        return ConvertResult(_baseFileSystem.Get.GetFileTimeStampRaw(out timeStamp, in path)).Ret();
    }

    protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
        ref readonly Path path)
    {
        return ConvertResult(_baseFileSystem.Get.QueryEntry(outBuffer, inBuffer, queryId, in path)).Ret();
    }

    protected override Result DoGetFreeSpaceSize(out long freeSpace, ref readonly Path path)
    {
        return ConvertResult(_baseFileSystem.Get.GetFreeSpaceSize(out freeSpace, in path)).Ret();
    }

    protected override Result DoGetTotalSpaceSize(out long totalSpace, ref readonly Path path)
    {
        return ConvertResult(_baseFileSystem.Get.GetTotalSpaceSize(out totalSpace, in path)).Ret();
    }

    protected override Result DoGetFileSystemAttribute(out FileSystemAttribute outAttribute)
    {
        return ConvertResult(_baseFileSystem.Get.GetFileSystemAttribute(out outAttribute)).Ret();
    }
}