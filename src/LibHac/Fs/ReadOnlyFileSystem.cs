using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Fsa;

namespace LibHac.Fs;

/// <summary>
/// Wraps an <see cref="IFile"/> and only allows read operations on it.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
file class ReadOnlyFile : IFile
{
    private UniqueRef<IFile> _baseFile;

    public ReadOnlyFile(ref UniqueRef<IFile> baseFile)
    {
        _baseFile = new UniqueRef<IFile>(ref baseFile);

        Assert.SdkRequires(_baseFile.HasValue);
    }

    public override void Dispose()
    {
        _baseFile.Destroy();

        base.Dispose();
    }

    protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination,
        in ReadOption option)
    {
        return _baseFile.Get.Read(out bytesRead, offset, destination, option);
    }

    protected override Result DoGetSize(out long size)
    {
        return _baseFile.Get.GetSize(out size);
    }

    protected override Result DoFlush()
    {
        return Result.Success;
    }

    protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
    {
        return ResultFs.UnsupportedWriteForReadOnlyFile.Log();
    }

    protected override Result DoSetSize(long size)
    {
        return ResultFs.UnsupportedWriteForReadOnlyFile.Log();
    }

    protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        switch (operationId)
        {
            case OperationId.InvalidateCache:
            case OperationId.QueryRange:
                return _baseFile.Get.OperateRange(outBuffer, operationId, offset, size, inBuffer);
            default:
                return ResultFs.UnsupportedOperateRangeForReadOnlyFile.Log();
        }
    }
}

/// <summary>
/// Wraps an <see cref="IFileSystem"/> and only allows read operations on it.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public class ReadOnlyFileSystem : IFileSystem
{
    private SharedRef<IFileSystem> _baseFileSystem;

    public ReadOnlyFileSystem(ref SharedRef<IFileSystem> baseFileSystem)
    {
        _baseFileSystem = SharedRef<IFileSystem>.CreateMove(ref baseFileSystem);

        Assert.SdkRequires(_baseFileSystem.HasValue);
    }

    public override void Dispose()
    {
        _baseFileSystem.Destroy();

        base.Dispose();
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
    {
        // The Read flag must be the only flag set
        if ((mode & OpenMode.All) != OpenMode.Read)
            return ResultFs.InvalidModeForFileOpen.Log();

        using var baseFile = new UniqueRef<IFile>();
        Result res = _baseFileSystem.Get.OpenFile(ref baseFile.Ref, in path, mode);
        if (res.IsFailure()) return res.Miss();

        outFile.Reset(new ReadOnlyFile(ref baseFile.Ref));
        return Result.Success;
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
        OpenDirectoryMode mode)
    {
        // An IDirectory is already read-only so we don't need a wrapper ReadOnlyDictionary class
        return _baseFileSystem.Get.OpenDirectory(ref outDirectory, in path, mode);
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
    {
        return _baseFileSystem.Get.GetEntryType(out entryType, in path);
    }

    protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option) =>
        ResultFs.UnsupportedWriteForReadOnlyFileSystem.Log();

    protected override Result DoDeleteFile(in Path path) =>
        ResultFs.UnsupportedWriteForReadOnlyFileSystem.Log();

    protected override Result DoCreateDirectory(in Path path) =>
        ResultFs.UnsupportedWriteForReadOnlyFileSystem.Log();

    protected override Result DoDeleteDirectory(in Path path) =>
        ResultFs.UnsupportedWriteForReadOnlyFileSystem.Log();

    protected override Result DoDeleteDirectoryRecursively(in Path path) =>
        ResultFs.UnsupportedWriteForReadOnlyFileSystem.Log();

    protected override Result DoCleanDirectoryRecursively(in Path path) =>
        ResultFs.UnsupportedWriteForReadOnlyFileSystem.Log();

    protected override Result DoRenameFile(in Path currentPath, in Path newPath) =>
        ResultFs.UnsupportedWriteForReadOnlyFileSystem.Log();

    protected override Result DoRenameDirectory(in Path currentPath, in Path newPath) =>
        ResultFs.UnsupportedWriteForReadOnlyFileSystem.Log();

    protected override Result DoCommit() =>
        Result.Success;

    protected override Result DoCommitProvisionally(long counter) =>
        ResultFs.UnsupportedCommitProvisionallyForReadOnlyFileSystem.Log();

    protected override Result DoGetFreeSpaceSize(out long freeSpace, in Path path)
    {
        return _baseFileSystem.Get.GetFreeSpaceSize(out freeSpace, in path);
    }

    protected override Result DoGetTotalSpaceSize(out long totalSpace, in Path path)
    {
        Unsafe.SkipInit(out totalSpace);
        return ResultFs.UnsupportedGetTotalSpaceSizeForReadOnlyFileSystem.Log();
    }

    protected override Result DoGetFileSystemAttribute(out FileSystemAttribute outAttribute)
    {
        return _baseFileSystem.Get.GetFileSystemAttribute(out outAttribute);
    }
}