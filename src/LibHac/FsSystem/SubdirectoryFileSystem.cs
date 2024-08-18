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

    public SubdirectoryFileSystem(ref readonly SharedRef<IFileSystem> baseFileSystem)
    {
        _baseFileSystemShared = SharedRef<IFileSystem>.CreateCopy(in baseFileSystem);
        _baseFileSystem = _baseFileSystemShared.Get;
    }

    public override void Dispose()
    {
        _baseFileSystemShared.Destroy();
        base.Dispose();
    }

    public Result Initialize(ref readonly Path rootPath)
    {
        return _rootPath.Initialize(in rootPath);
    }

    private Result ResolveFullPath(ref Path outPath, ref readonly Path relativePath)
    {
        using Path rootPath = _rootPath.DangerousGetPath();
        return outPath.Combine(in rootPath, in relativePath);
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, ref readonly Path path)
    {
        UnsafeHelpers.SkipParamInit(out entryType);

        using var fullPath = new Path();
        Result res = ResolveFullPath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.GetEntryType(out entryType, in fullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoGetFreeSpaceSize(out long freeSpace, ref readonly Path path)
    {
        UnsafeHelpers.SkipParamInit(out freeSpace);

        using var fullPath = new Path();
        Result res = ResolveFullPath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.GetFreeSpaceSize(out freeSpace, in fullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoGetTotalSpaceSize(out long totalSpace, ref readonly Path path)
    {
        UnsafeHelpers.SkipParamInit(out totalSpace);

        using var fullPath = new Path();
        Result res = ResolveFullPath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.GetTotalSpaceSize(out totalSpace, in fullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, ref readonly Path path)
    {
        UnsafeHelpers.SkipParamInit(out timeStamp);

        using var fullPath = new Path();
        Result res = ResolveFullPath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.GetFileTimeStampRaw(out timeStamp, in fullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, ref readonly Path path, OpenMode mode)
    {
        using var fullPath = new Path();
        Result res = ResolveFullPath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.OpenFile(ref outFile, in fullPath, mode);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, ref readonly Path path,
        OpenDirectoryMode mode)
    {
        using var fullPath = new Path();
        Result res = ResolveFullPath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.OpenDirectory(ref outDirectory, in fullPath, mode);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoCreateFile(ref readonly Path path, long size, CreateFileOptions option)
    {
        using var fullPath = new Path();
        Result res = ResolveFullPath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.CreateFile(in fullPath, size, option);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoDeleteFile(ref readonly Path path)
    {
        using var fullPath = new Path();
        Result res = ResolveFullPath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.DeleteFile(in fullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoCreateDirectory(ref readonly Path path)
    {
        using var fullPath = new Path();
        Result res = ResolveFullPath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.CreateDirectory(in fullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoDeleteDirectory(ref readonly Path path)
    {
        using var fullPath = new Path();
        Result res = ResolveFullPath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.DeleteDirectory(in fullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoDeleteDirectoryRecursively(ref readonly Path path)
    {
        using var fullPath = new Path();
        Result res = ResolveFullPath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.DeleteDirectoryRecursively(in fullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoCleanDirectoryRecursively(ref readonly Path path)
    {
        using var fullPath = new Path();
        Result res = ResolveFullPath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        res = _baseFileSystem.CleanDirectoryRecursively(in fullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoRenameFile(ref readonly Path currentPath, ref readonly Path newPath)
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

    protected override Result DoRenameDirectory(ref readonly Path currentPath, ref readonly Path newPath)
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
        ref readonly Path path)
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

    protected override Result DoGetFileSystemAttribute(out FileSystemAttribute outAttribute)
    {
        int rootPathCount = _rootPath.GetLength();

        Result res = _baseFileSystem.GetFileSystemAttribute(out outAttribute);
        if (res.IsFailure()) return res.Miss();

        res = Utility.CountUtf16CharacterForUtf8String(out ulong rootPathUtf16Count, _rootPath.GetString());
        if (res.IsFailure()) return res.Miss();

        Utility.SubtractAllPathLengthMax(ref outAttribute, rootPathCount);
        Utility.SubtractAllUtf16CountMax(ref outAttribute, (int)rootPathUtf16Count);

        return Result.Success;
    }
}