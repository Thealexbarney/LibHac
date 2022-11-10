using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.Sf;
using LibHac.Util;
using IFile = LibHac.Fs.Fsa.IFile;
using IFileSf = LibHac.FsSrv.Sf.IFile;
using IDirectory = LibHac.Fs.Fsa.IDirectory;
using IDirectorySf = LibHac.FsSrv.Sf.IDirectory;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;
using PathSf = LibHac.FsSrv.Sf.Path;

// ReSharper disable CheckNamespace
namespace LibHac.Fs.Impl;

/// <summary>
/// An adapter for using an <see cref="IFileSf"/> service object as an <see cref="IFile"/>. Used
/// when receiving a Horizon IPC file object so it can be used as an <see cref="IFile"/> locally.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
internal class FileServiceObjectAdapter : IFile
{
    private SharedRef<IFileSf> _baseFile;

    public FileServiceObjectAdapter(ref SharedRef<IFileSf> baseFile)
    {
        _baseFile = SharedRef<IFileSf>.CreateMove(ref baseFile);
    }

    public override void Dispose()
    {
        _baseFile.Destroy();
        base.Dispose();
    }

    protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination, in ReadOption option)
    {
        return _baseFile.Get.Read(out bytesRead, offset, new OutBuffer(destination), destination.Length, option);
    }

    protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
    {
        return _baseFile.Get.Write(offset, new InBuffer(source), source.Length, option);
    }

    protected override Result DoFlush()
    {
        return _baseFile.Get.Flush();
    }

    protected override Result DoSetSize(long size)
    {
        return _baseFile.Get.SetSize(size);
    }

    protected override Result DoGetSize(out long size)
    {
        return _baseFile.Get.GetSize(out size);
    }

    protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        switch (operationId)
        {
            case OperationId.QueryRange:
                if (outBuffer.Length != Unsafe.SizeOf<QueryRangeInfo>())
                    return ResultFs.InvalidSize.Log();

                ref QueryRangeInfo info = ref SpanHelpers.AsStruct<QueryRangeInfo>(outBuffer);

                return _baseFile.Get.OperateRange(out info, (int)OperationId.QueryRange, offset, size);
            case OperationId.InvalidateCache:
                return _baseFile.Get.OperateRange(out _, (int)OperationId.InvalidateCache, offset, size);
            default:
                return _baseFile.Get.OperateRangeWithBuffer(new OutBuffer(outBuffer), new InBuffer(inBuffer),
                    (int)operationId, offset, size);
        }
    }
}

/// <summary>
/// An adapter for using an <see cref="IDirectorySf"/> service object as an <see cref="IDirectory"/>. Used
/// when receiving a Horizon IPC directory object so it can be used as an <see cref="IDirectory"/> locally.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
internal class DirectoryServiceObjectAdapter : IDirectory
{
    private SharedRef<IDirectorySf> _baseDirectory;

    public DirectoryServiceObjectAdapter(ref SharedRef<IDirectorySf> baseDirectory)
    {
        _baseDirectory = SharedRef<IDirectorySf>.CreateMove(ref baseDirectory);
    }

    public override void Dispose()
    {
        _baseDirectory.Destroy();
        base.Dispose();
    }

    protected override Result DoRead(out long entriesRead, Span<DirectoryEntry> entryBuffer)
    {
        Span<byte> buffer = MemoryMarshal.Cast<DirectoryEntry, byte>(entryBuffer);
        return _baseDirectory.Get.Read(out entriesRead, new OutBuffer(buffer));
    }

    protected override Result DoGetEntryCount(out long entryCount)
    {
        return _baseDirectory.Get.GetEntryCount(out entryCount);
    }
}

/// <summary>
/// An adapter for using an <see cref="IFileSystemSf"/> service object as an <see cref="IFileSystem"/>. Used
/// when receiving a Horizon IPC file system object so it can be used as an <see cref="IFileSystem"/> locally.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
internal class FileSystemServiceObjectAdapter : IFileSystem, IMultiCommitTarget
{
    private SharedRef<IFileSystemSf> _baseFs;

    private static Result GetPathForServiceObject(out PathSf sfPath, in Path path)
    {
        UnsafeHelpers.SkipParamInit(out sfPath);

        int length = StringUtils.Copy(SpanHelpers.AsByteSpan(ref sfPath), path.GetString(),
            PathTool.EntryNameLengthMax + 1);

        if (length > PathTool.EntryNameLengthMax)
            return ResultFs.TooLongPath.Log();

        return Result.Success;
    }

    public FileSystemServiceObjectAdapter(ref SharedRef<IFileSystemSf> baseFileSystem)
    {
        _baseFs = SharedRef<IFileSystemSf>.CreateMove(ref baseFileSystem);
    }

    public override void Dispose()
    {
        _baseFs.Destroy();
        base.Dispose();
    }

    protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option)
    {
        Result res = GetPathForServiceObject(out PathSf sfPath, in path);
        if (res.IsFailure()) return res.Miss();

        return _baseFs.Get.CreateFile(in sfPath, size, (int)option);
    }

    protected override Result DoDeleteFile(in Path path)
    {
        Result res = GetPathForServiceObject(out PathSf sfPath, in path);
        if (res.IsFailure()) return res.Miss();

        return _baseFs.Get.DeleteFile(in sfPath);
    }

    protected override Result DoCreateDirectory(in Path path)
    {
        Result res = GetPathForServiceObject(out PathSf sfPath, in path);
        if (res.IsFailure()) return res.Miss();

        return _baseFs.Get.CreateDirectory(in sfPath);
    }

    protected override Result DoDeleteDirectory(in Path path)
    {
        Result res = GetPathForServiceObject(out PathSf sfPath, in path);
        if (res.IsFailure()) return res.Miss();

        return _baseFs.Get.DeleteDirectory(in sfPath);
    }

    protected override Result DoDeleteDirectoryRecursively(in Path path)
    {
        Result res = GetPathForServiceObject(out PathSf sfPath, in path);
        if (res.IsFailure()) return res.Miss();

        return _baseFs.Get.DeleteDirectoryRecursively(in sfPath);
    }

    protected override Result DoCleanDirectoryRecursively(in Path path)
    {
        Result res = GetPathForServiceObject(out PathSf sfPath, in path);
        if (res.IsFailure()) return res.Miss();

        return _baseFs.Get.CleanDirectoryRecursively(in sfPath);
    }

    protected override Result DoRenameFile(in Path currentPath, in Path newPath)
    {
        Result res = GetPathForServiceObject(out PathSf currentSfPath, in currentPath);
        if (res.IsFailure()) return res.Miss();

        res = GetPathForServiceObject(out PathSf newSfPath, in newPath);
        if (res.IsFailure()) return res.Miss();

        return _baseFs.Get.RenameFile(in currentSfPath, in newSfPath);
    }

    protected override Result DoRenameDirectory(in Path currentPath, in Path newPath)
    {
        Result res = GetPathForServiceObject(out PathSf currentSfPath, in currentPath);
        if (res.IsFailure()) return res.Miss();

        res = GetPathForServiceObject(out PathSf newSfPath, in newPath);
        if (res.IsFailure()) return res.Miss();

        return _baseFs.Get.RenameDirectory(in currentSfPath, in newSfPath);
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
    {
        UnsafeHelpers.SkipParamInit(out entryType);

        Result res = GetPathForServiceObject(out PathSf sfPath, in path);
        if (res.IsFailure()) return res.Miss();

        ref uint sfEntryType = ref Unsafe.As<DirectoryEntryType, uint>(ref entryType);

        return _baseFs.Get.GetEntryType(out sfEntryType, in sfPath);
    }

    protected override Result DoGetFreeSpaceSize(out long freeSpace, in Path path)
    {
        UnsafeHelpers.SkipParamInit(out freeSpace);

        Result res = GetPathForServiceObject(out PathSf sfPath, in path);
        if (res.IsFailure()) return res.Miss();

        return _baseFs.Get.GetFreeSpaceSize(out freeSpace, in sfPath);
    }

    protected override Result DoGetTotalSpaceSize(out long totalSpace, in Path path)
    {
        UnsafeHelpers.SkipParamInit(out totalSpace);

        Result res = GetPathForServiceObject(out PathSf sfPath, in path);
        if (res.IsFailure()) return res.Miss();

        return _baseFs.Get.GetTotalSpaceSize(out totalSpace, in sfPath);
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
    {
        Result res = GetPathForServiceObject(out PathSf sfPath, in path);
        if (res.IsFailure()) return res.Miss();

        using var fileServiceObject = new SharedRef<IFileSf>();

        res = _baseFs.Get.OpenFile(ref fileServiceObject.Ref(), in sfPath, (uint)mode);
        if (res.IsFailure()) return res.Miss();

        outFile.Reset(new FileServiceObjectAdapter(ref fileServiceObject.Ref()));
        return Result.Success;
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
        OpenDirectoryMode mode)
    {
        Result res = GetPathForServiceObject(out PathSf sfPath, in path);
        if (res.IsFailure()) return res.Miss();

        using var directoryServiceObject = new SharedRef<IDirectorySf>();

        res = _baseFs.Get.OpenDirectory(ref directoryServiceObject.Ref(), in sfPath, (uint)mode);
        if (res.IsFailure()) return res.Miss();

        outDirectory.Reset(new DirectoryServiceObjectAdapter(ref directoryServiceObject.Ref()));
        return Result.Success;
    }

    protected override Result DoCommit()
    {
        return _baseFs.Get.Commit();
    }

    protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in Path path)
    {
        UnsafeHelpers.SkipParamInit(out timeStamp);

        Result res = GetPathForServiceObject(out PathSf sfPath, in path);
        if (res.IsFailure()) return res.Miss();

        return _baseFs.Get.GetFileTimeStampRaw(out timeStamp, in sfPath);
    }

    protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
        in Path path)
    {
        Result res = GetPathForServiceObject(out PathSf sfPath, in path);
        if (res.IsFailure()) return res.Miss();

        return _baseFs.Get.QueryEntry(new OutBuffer(outBuffer), new InBuffer(inBuffer), (int)queryId, in sfPath);
    }

    public SharedRef<IFileSystemSf> GetFileSystem()
    {
        return SharedRef<IFileSystemSf>.CreateCopy(in _baseFs);
    }

    public SharedRef<IFileSystemSf> GetMultiCommitTarget()
    {
        return GetFileSystem();
    }
}