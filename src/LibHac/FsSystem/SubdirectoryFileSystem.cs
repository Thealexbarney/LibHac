using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem;

/// <summary>
/// An <see cref="IFileSystem"/> that uses a directory of another <see cref="IFileSystem"/> as its root directory.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public class SubdirectoryFileSystem : IFileSystem
{
    private IFileSystem _baseFileSystem;
    private SharedRef<IFileSystem> _baseFileSystemShared;
    private Path.Stored _rootPath;

    public SubdirectoryFileSystem(IFileSystem baseFileSystem)
    {
        _baseFileSystem = baseFileSystem;
    }

    public SubdirectoryFileSystem(ref SharedRef<IFileSystem> baseFileSystem)
    {
        _baseFileSystemShared = SharedRef<IFileSystem>.CreateMove(ref baseFileSystem);
        _baseFileSystem = _baseFileSystemShared.Get;
    }

    public override void Dispose()
    {
        _baseFileSystemShared.Destroy();
        base.Dispose();
    }

    public Result Initialize(in Path rootPath)
    {
        return _rootPath.Initialize(in rootPath);
    }

    private Result ResolveFullPath(ref Path outPath, in Path relativePath)
    {
        using Path rootPath = _rootPath.DangerousGetPath();
        return outPath.Combine(in rootPath, in relativePath);
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
    {
        UnsafeHelpers.SkipParamInit(out entryType);

        using var fullPath = new Path();
        Result res = ResolveFullPath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.GetEntryType(out entryType, in fullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoGetFreeSpaceSize(out long freeSpace, in Path path)
    {
        UnsafeHelpers.SkipParamInit(out freeSpace);

        using var fullPath = new Path();
        Result res = ResolveFullPath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.GetFreeSpaceSize(out freeSpace, in fullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoGetTotalSpaceSize(out long totalSpace, in Path path)
    {
        UnsafeHelpers.SkipParamInit(out totalSpace);

        using var fullPath = new Path();
        Result res = ResolveFullPath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.GetTotalSpaceSize(out totalSpace, in fullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in Path path)
    {
        UnsafeHelpers.SkipParamInit(out timeStamp);

        using var fullPath = new Path();
        Result res = ResolveFullPath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.GetFileTimeStampRaw(out timeStamp, in fullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
    {
        using var fullPath = new Path();
        Result res = ResolveFullPath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.OpenFile(ref outFile, in fullPath, mode);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
        OpenDirectoryMode mode)
    {
        using var fullPath = new Path();
        Result res = ResolveFullPath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.OpenDirectory(ref outDirectory, in fullPath, mode);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option)
    {
        using var fullPath = new Path();
        Result res = ResolveFullPath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.CreateFile(in fullPath, size, option);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoDeleteFile(in Path path)
    {
        using var fullPath = new Path();
        Result res = ResolveFullPath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.DeleteFile(in fullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoCreateDirectory(in Path path)
    {
        using var fullPath = new Path();
        Result res = ResolveFullPath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.CreateDirectory(in fullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoDeleteDirectory(in Path path)
    {
        using var fullPath = new Path();
        Result res = ResolveFullPath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.DeleteDirectory(in fullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoDeleteDirectoryRecursively(in Path path)
    {
        using var fullPath = new Path();
        Result res = ResolveFullPath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.DeleteDirectoryRecursively(in fullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoCleanDirectoryRecursively(in Path path)
    {
        using var fullPath = new Path();
        Result res = ResolveFullPath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.CleanDirectoryRecursively(in fullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoRenameFile(in Path currentPath, in Path newPath)
    {
        using var currentFullPath = new Path();
        Result res = ResolveFullPath(ref currentFullPath.Ref(), in currentPath);
        if (res.IsFailure()) return res.Miss();

        using var newFullPath = new Path();
        res = ResolveFullPath(ref newFullPath.Ref(), in newPath);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.RenameFile(in currentFullPath, in newFullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoRenameDirectory(in Path currentPath, in Path newPath)
    {
        using var currentFullPath = new Path();
        Result res = ResolveFullPath(ref currentFullPath.Ref(), in currentPath);
        if (res.IsFailure()) return res.Miss();

        using var newFullPath = new Path();
        res = ResolveFullPath(ref newFullPath.Ref(), in newPath);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.RenameDirectory(in currentFullPath, in newFullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
        in Path path)
    {
        using var fullPath = new Path();
        Result res = ResolveFullPath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.QueryEntry(outBuffer, inBuffer, queryId, in fullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoCommit()
    {
        return _baseFileSystem.Commit();
    }

    protected override Result DoCommitProvisionally(long counter)
    {
        return _baseFileSystem.CommitProvisionally(counter);
    }

    protected override Result DoRollback()
    {
        return _baseFileSystem.Rollback();
    }
}