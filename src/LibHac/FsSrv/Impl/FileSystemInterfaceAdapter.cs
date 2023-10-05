using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Sf;
using IFile = LibHac.Fs.Fsa.IFile;
using IFileSf = LibHac.FsSrv.Sf.IFile;
using IDirectory = LibHac.Fs.Fsa.IDirectory;
using IDirectorySf = LibHac.FsSrv.Sf.IDirectory;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;
using PathSf = LibHac.FsSrv.Sf.Path;

namespace LibHac.FsSrv.Impl;

/// <summary>
/// Wraps an <see cref="IFile"/> to allow interfacing with it via the <see cref="IFileSf"/> interface over IPC.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public class FileInterfaceAdapter : IFileSf
{
    private SharedRef<FileSystemInterfaceAdapter> _parentFs;
    private UniqueRef<IFile> _baseFile;
    private bool _allowAllOperations;

    public FileInterfaceAdapter(ref UniqueRef<IFile> baseFile,
        ref SharedRef<FileSystemInterfaceAdapter> parentFileSystem, bool allowAllOperations)
    {
        _parentFs = SharedRef<FileSystemInterfaceAdapter>.CreateMove(ref parentFileSystem);
        _baseFile = new UniqueRef<IFile>(ref baseFile);
        _allowAllOperations = allowAllOperations;
    }

    public void Dispose()
    {
        _baseFile.Destroy();
        _parentFs.Destroy();
    }

    public Result Read(out long bytesRead, long offset, OutBuffer destination, long size, ReadOption option)
    {
        const int maxTryCount = 2;
        UnsafeHelpers.SkipParamInit(out bytesRead);

        if (offset < 0)
            return ResultFs.InvalidOffset.Log();

        if (size < 0)
            return ResultFs.InvalidSize.Log();

        if (destination.Size < (int)size)
            return ResultFs.InvalidSize.Log();

        Result res = Result.Success;
        long readSize = 0;

        for (int tryNum = 0; tryNum < maxTryCount; tryNum++)
        {
            res = _baseFile.Get.Read(out readSize, offset, destination.Buffer.Slice(0, (int)size), option);

            // Retry on ResultDataCorrupted
            if (!ResultFs.DataCorrupted.Includes(res))
                break;
        }

        if (res.IsFailure()) return res.Miss();

        bytesRead = readSize;
        return Result.Success;
    }

    public Result Write(long offset, InBuffer source, long size, WriteOption option)
    {
        if (offset < 0)
            return ResultFs.InvalidOffset.Log();

        if (size < 0)
            return ResultFs.InvalidSize.Log();

        if (source.Size < (int)size)
            return ResultFs.InvalidSize.Log();

        using var scopedPriorityChanger =
            new ScopedThreadPriorityChangerByAccessPriority(ScopedThreadPriorityChangerByAccessPriority.AccessMode.Write);

        return _baseFile.Get.Write(offset, source.Buffer.Slice(0, (int)size), option);
    }

    public Result Flush()
    {
        return _baseFile.Get.Flush();
    }

    public Result SetSize(long size)
    {
        if (size < 0)
            return ResultFs.InvalidSize.Log();

        return _baseFile.Get.SetSize(size);
    }

    public Result GetSize(out long size)
    {
        const int maxTryCount = 2;
        UnsafeHelpers.SkipParamInit(out size);

        Result res = Result.Success;
        long tmpSize = 0;

        for (int tryNum = 0; tryNum < maxTryCount; tryNum++)
        {
            res = _baseFile.Get.GetSize(out tmpSize);

            // Retry on ResultDataCorrupted
            if (!ResultFs.DataCorrupted.Includes(res))
                break;
        }

        if (res.IsFailure()) return res.Miss();

        size = tmpSize;
        return Result.Success;
    }

    public Result OperateRange(out QueryRangeInfo rangeInfo, int operationId, long offset, long size)
    {
        UnsafeHelpers.SkipParamInit(out rangeInfo);
        rangeInfo.Clear();

        if (operationId == (int)OperationId.QueryRange)
        {
            Unsafe.SkipInit(out QueryRangeInfo info);

            Result res = _baseFile.Get.OperateRange(SpanHelpers.AsByteSpan(ref info), OperationId.QueryRange, offset,
                size, ReadOnlySpan<byte>.Empty);
            if (res.IsFailure()) return res.Miss();

            rangeInfo.Merge(in info);
        }
        else if (operationId == (int)OperationId.InvalidateCache)
        {
            Result res = _baseFile.Get.OperateRange(Span<byte>.Empty, OperationId.InvalidateCache, offset, size,
                ReadOnlySpan<byte>.Empty);
            if (res.IsFailure()) return res.Miss();
        }

        return Result.Success;
    }

    public Result OperateRangeWithBuffer(OutBuffer outBuffer, InBuffer inBuffer, int operationId, long offset, long size)
    {
        static Result PermissionCheck(OperationId operationId, FileInterfaceAdapter fileAdapter)
        {
            if (operationId == OperationId.QueryUnpreparedRange ||
                operationId == OperationId.QueryLazyLoadCompletionRate ||
                operationId == OperationId.SetLazyLoadPriority)
            {
                return Result.Success;
            }

            if (!fileAdapter._allowAllOperations)
                return ResultFs.PermissionDenied.Log();

            return Result.Success;
        }

        Result res = PermissionCheck((OperationId)operationId, this);
        if (res.IsFailure()) return res.Miss();

        res = _baseFile.Get.OperateRange(outBuffer.Buffer, (OperationId)operationId, offset, size, inBuffer.Buffer);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }
}

/// <summary>
/// Wraps an <see cref="IDirectory"/> to allow interfacing with it via the <see cref="IDirectorySf"/> interface over IPC.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public class DirectoryInterfaceAdapter : IDirectorySf
{
    private SharedRef<FileSystemInterfaceAdapter> _parentFs;
    private UniqueRef<IDirectory> _baseDirectory;

    public DirectoryInterfaceAdapter(ref UniqueRef<IDirectory> baseDirectory,
        ref SharedRef<FileSystemInterfaceAdapter> parentFileSystem)
    {
        _parentFs = SharedRef<FileSystemInterfaceAdapter>.CreateMove(ref parentFileSystem);
        _baseDirectory = new UniqueRef<IDirectory>(ref baseDirectory);
    }

    public void Dispose()
    {
        _baseDirectory.Destroy();
        _parentFs.Destroy();
    }

    public Result Read(out long entriesRead, OutBuffer entryBuffer)
    {
        const int maxTryCount = 2;
        UnsafeHelpers.SkipParamInit(out entriesRead);

        Span<DirectoryEntry> entries = MemoryMarshal.Cast<byte, DirectoryEntry>(entryBuffer.Buffer);

        Result res = Result.Success;
        long numRead = 0;

        for (int tryNum = 0; tryNum < maxTryCount; tryNum++)
        {
            res = _baseDirectory.Get.Read(out numRead, entries);

            // Retry on ResultDataCorrupted
            if (!ResultFs.DataCorrupted.Includes(res))
                break;
        }

        if (res.IsFailure()) return res.Miss();

        entriesRead = numRead;
        return Result.Success;
    }

    public Result GetEntryCount(out long entryCount)
    {
        UnsafeHelpers.SkipParamInit(out entryCount);

        Result res = _baseDirectory.Get.GetEntryCount(out long count);
        if (res.IsFailure()) return res.Miss();

        entryCount = count;
        return Result.Success;
    }
}

/// <summary>
/// Wraps an <see cref="IFileSystem"/> to allow interfacing with it via the <see cref="IFileSystemSf"/> interface over IPC.
/// All incoming paths are normalized before they are passed to the base <see cref="IFileSystem"/>.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public class FileSystemInterfaceAdapter : IFileSystemSf
{
    private SharedRef<IFileSystem> _baseFileSystem;
    private PathFlags _pathFlags;
    private bool _allowAllOperations;

    // In FS, FileSystemInterfaceAdapter is derived from ISharedObject, so that's used for ref-counting when
    // creating files and directories. We don't have an ISharedObject, so a self-reference is used instead.
    private WeakRef<FileSystemInterfaceAdapter> _selfReference;

    private FileSystemInterfaceAdapter(ref SharedRef<IFileSystem> fileSystem,
        bool allowAllOperations)
    {
        _baseFileSystem = SharedRef<IFileSystem>.CreateMove(ref fileSystem);
        _allowAllOperations = allowAllOperations;
    }

    private FileSystemInterfaceAdapter(ref SharedRef<IFileSystem> fileSystem, PathFlags flags,
        bool allowAllOperations)
    {
        _baseFileSystem = SharedRef<IFileSystem>.CreateMove(ref fileSystem);
        _pathFlags = flags;
        _allowAllOperations = allowAllOperations;
    }

    public static SharedRef<IFileSystemSf> CreateShared(ref SharedRef<IFileSystem> baseFileSystem, bool allowAllOperations)
    {
        var adapter = new FileSystemInterfaceAdapter(ref baseFileSystem, allowAllOperations);
        using var sharedAdapter = new SharedRef<FileSystemInterfaceAdapter>(adapter);

        adapter._selfReference.Set(in sharedAdapter);

        return SharedRef<IFileSystemSf>.CreateMove(ref sharedAdapter.Ref);
    }

    public static SharedRef<IFileSystemSf> CreateShared(
        ref SharedRef<IFileSystem> baseFileSystem, PathFlags flags, bool allowAllOperations)
    {
        var adapter = new FileSystemInterfaceAdapter(ref baseFileSystem, flags, allowAllOperations);
        using var sharedAdapter = new SharedRef<FileSystemInterfaceAdapter>(adapter);

        adapter._selfReference.Set(in sharedAdapter);

        return SharedRef<IFileSystemSf>.CreateMove(ref sharedAdapter.Ref);
    }

    public void Dispose()
    {
        _baseFileSystem.Destroy();
        _selfReference.Destroy();
    }

    private static ReadOnlySpan<byte> RootDir => "/"u8;

    private Result SetUpPath(ref Path fsPath, in PathSf sfPath)
    {
        Result res;

        if (_pathFlags.IsWindowsPathAllowed())
        {
            res = fsPath.InitializeWithReplaceUnc(sfPath.Str);
            if (res.IsFailure()) return res.Miss();
        }
        else
        {
            res = fsPath.Initialize(sfPath.Str);
            if (res.IsFailure()) return res.Miss();
        }

        res = fsPath.Normalize(_pathFlags);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result CreateFile(in PathSf path, long size, int option)
    {
        if (size < 0)
            return ResultFs.InvalidSize.Log();

        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.Get.CreateFile(in pathNormalized, size, (CreateFileOptions)option);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result DeleteFile(in PathSf path)
    {
        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.Get.DeleteFile(in pathNormalized);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result CreateDirectory(in PathSf path)
    {
        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        if (pathNormalized == RootDir)
            return ResultFs.PathAlreadyExists.Log();

        res = _baseFileSystem.Get.CreateDirectory(in pathNormalized);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result DeleteDirectory(in PathSf path)
    {
        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        if (pathNormalized == RootDir)
            return ResultFs.DirectoryUndeletable.Log();

        res = _baseFileSystem.Get.DeleteDirectory(in pathNormalized);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result DeleteDirectoryRecursively(in PathSf path)
    {
        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        if (pathNormalized == RootDir)
            return ResultFs.DirectoryUndeletable.Log();

        res = _baseFileSystem.Get.DeleteDirectoryRecursively(in pathNormalized);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result CleanDirectoryRecursively(in PathSf path)
    {
        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.Get.CleanDirectoryRecursively(in pathNormalized);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result RenameFile(in PathSf currentPath, in PathSf newPath)
    {
        using var currentPathNormalized = new Path();
        Result res = SetUpPath(ref currentPathNormalized.Ref(), in currentPath);
        if (res.IsFailure()) return res.Miss();

        using var newPathNormalized = new Path();
        res = SetUpPath(ref newPathNormalized.Ref(), in newPath);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.Get.RenameFile(in currentPathNormalized, in newPathNormalized);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result RenameDirectory(in PathSf currentPath, in PathSf newPath)
    {
        using var currentPathNormalized = new Path();
        Result res = SetUpPath(ref currentPathNormalized.Ref(), in currentPath);
        if (res.IsFailure()) return res.Miss();

        using var newPathNormalized = new Path();
        res = SetUpPath(ref newPathNormalized.Ref(), in newPath);
        if (res.IsFailure()) return res.Miss();

        if (PathUtility.IsSubPath(currentPathNormalized.GetString(), newPathNormalized.GetString()))
            return ResultFs.DirectoryUnrenamable.Log();

        res = _baseFileSystem.Get.RenameDirectory(in currentPathNormalized, in newPathNormalized);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result GetEntryType(out uint entryType, in PathSf path)
    {
        UnsafeHelpers.SkipParamInit(out entryType);

        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.Get.GetEntryType(out DirectoryEntryType type, in pathNormalized);
        if (res.IsFailure()) return res.Miss();

        entryType = (uint)type;
        return Result.Success;
    }

    public Result GetFreeSpaceSize(out long freeSpace, in PathSf path)
    {
        UnsafeHelpers.SkipParamInit(out freeSpace);

        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.Get.GetFreeSpaceSize(out long space, in pathNormalized);
        if (res.IsFailure()) return res.Miss();

        freeSpace = space;
        return Result.Success;
    }

    public Result GetTotalSpaceSize(out long totalSpace, in PathSf path)
    {
        UnsafeHelpers.SkipParamInit(out totalSpace);

        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.Get.GetTotalSpaceSize(out long space, in pathNormalized);
        if (res.IsFailure()) return res.Miss();

        totalSpace = space;
        return Result.Success;
    }

    public Result OpenFile(ref SharedRef<IFileSf> outFile, in PathSf path, uint mode)
    {
        const int maxTryCount = 2;

        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        using var file = new UniqueRef<IFile>();

        for (int tryNum = 0; tryNum < maxTryCount; tryNum++)
        {
            res = _baseFileSystem.Get.OpenFile(ref file.Ref, in pathNormalized, (OpenMode)mode);

            // Retry on ResultDataCorrupted
            if (!ResultFs.DataCorrupted.Includes(res))
                break;
        }

        if (res.IsFailure()) return res.Miss();

        using SharedRef<FileSystemInterfaceAdapter> selfReference =
            SharedRef<FileSystemInterfaceAdapter>.Create(in _selfReference);

        var adapter = new FileInterfaceAdapter(ref file.Ref, ref selfReference.Ref, _allowAllOperations);
        outFile.Reset(adapter);

        return Result.Success;
    }

    public Result OpenDirectory(ref SharedRef<IDirectorySf> outDirectory, in PathSf path, uint mode)
    {
        const int maxTryCount = 2;

        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        using var directory = new UniqueRef<IDirectory>();

        for (int tryNum = 0; tryNum < maxTryCount; tryNum++)
        {
            res = _baseFileSystem.Get.OpenDirectory(ref directory.Ref, in pathNormalized,
                (OpenDirectoryMode)mode);

            // Retry on ResultDataCorrupted
            if (!ResultFs.DataCorrupted.Includes(res))
                break;
        }

        if (res.IsFailure()) return res.Miss();

        using SharedRef<FileSystemInterfaceAdapter> selfReference =
            SharedRef<FileSystemInterfaceAdapter>.Create(in _selfReference);

        var adapter = new DirectoryInterfaceAdapter(ref directory.Ref, ref selfReference.Ref);
        outDirectory.Reset(adapter);

        return Result.Success;
    }

    public Result Commit()
    {
        return _baseFileSystem.Get.Commit();
    }

    public Result GetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in PathSf path)
    {
        UnsafeHelpers.SkipParamInit(out timeStamp);

        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.Get.GetFileTimeStampRaw(out FileTimeStampRaw tempTimeStamp, in pathNormalized);
        if (res.IsFailure()) return res.Miss();

        timeStamp = tempTimeStamp;
        return Result.Success;
    }

    public Result QueryEntry(OutBuffer outBuffer, InBuffer inBuffer, int queryId, in PathSf path)
    {
        static Result PermissionCheck(QueryId queryId, FileSystemInterfaceAdapter fsAdapter)
        {
            if (queryId == QueryId.SetConcatenationFileAttribute ||
                queryId == QueryId.IsSignedSystemPartition ||
                queryId == QueryId.QueryUnpreparedFileInformation)
            {
                return Result.Success;
            }

            if (!fsAdapter._allowAllOperations)
                return ResultFs.PermissionDenied.Log();

            return Result.Success;
        }

        Result res = PermissionCheck((QueryId)queryId, this);
        if (res.IsFailure()) return res.Miss();

        using var pathNormalized = new Path();
        res = SetUpPath(ref pathNormalized.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.Get.QueryEntry(outBuffer.Buffer, inBuffer.Buffer, (QueryId)queryId,
            in pathNormalized);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result GetFileSystemAttribute(out FileSystemAttribute outAttribute)
    {
        UnsafeHelpers.SkipParamInit(out outAttribute);

        Result res = _baseFileSystem.Get.GetFileSystemAttribute(out FileSystemAttribute attribute);
        if (res.IsFailure()) return res.Miss();

        outAttribute = attribute;
        return Result.Success;
    }

    public Result GetImpl(ref SharedRef<IFileSystem> fileSystem)
    {
        fileSystem.SetByCopy(in _baseFileSystem);
        return Result.Success;
    }
}