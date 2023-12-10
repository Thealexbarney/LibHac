using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text.Unicode;
using System.Threading;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem.Impl;
using LibHac.Tools.FsSystem;
using LibHac.Util;
using static LibHac.Fs.StringTraits;
using Path = LibHac.Fs.Path;

namespace LibHac.FsSystem;

public class LocalFileSystem : IAttributeFileSystem
{
    /// <summary>
    /// Specifies the case-sensitivity of a <see cref="LocalFileSystem"/>.
    /// </summary>
    public enum PathMode
    {
        /// <summary>
        /// Uses the default case-sensitivity of the underlying file system.
        /// </summary>
        DefaultCaseSensitivity,

        /// <summary>
        /// Treats the file system as case-sensitive.
        /// </summary>
        CaseSensitive
    }

    private Path.Stored _rootPath;
    private string _rootPathUtf16;
    private readonly FileSystemClient _fsClient;
    private PathMode _mode;
    private readonly bool _useUnixTime;

    public LocalFileSystem() : this(true) { }

    public LocalFileSystem(bool useUnixTimeStamps)
    {
        _useUnixTime = useUnixTimeStamps;
    }

    public LocalFileSystem(FileSystemClient fsClient, bool useUnixTimeStamps) : this(useUnixTimeStamps)
    {
        _fsClient = fsClient;
    }

    /// <summary>
    /// Opens a directory on local storage as an <see cref="IFileSystem"/>.
    /// The directory will be created if it does not exist.
    /// </summary>
    /// <param name="rootPath">The path that will be the root of the <see cref="LocalFileSystem"/>.</param>
    public LocalFileSystem(string rootPath)
    {
        Result res = Initialize(rootPath, PathMode.DefaultCaseSensitivity, true);
        if (res.IsFailure())
            throw new HorizonResultException(res, "Error creating LocalFileSystem.");
    }

    public static Result Create(out LocalFileSystem fileSystem, string rootPath,
        PathMode pathMode = PathMode.DefaultCaseSensitivity, bool ensurePathExists = true)
    {
        UnsafeHelpers.SkipParamInit(out fileSystem);

        var localFs = new LocalFileSystem();
        Result res = localFs.Initialize(rootPath, pathMode, ensurePathExists);
        if (res.IsFailure()) return res.Miss();

        fileSystem = localFs;
        return Result.Success;
    }

    public Result Initialize(string rootPath, PathMode pathMode, bool ensurePathExists)
    {
        Result res;

        if (rootPath == null)
            return ResultFs.NullptrArgument.Log();

        _mode = pathMode;

        // If the root path is empty, we interpret any incoming paths as rooted paths.
        if (rootPath == string.Empty)
        {
            using var path = new Path();
            res = path.InitializeAsEmpty();
            if (res.IsFailure()) return res.Miss();

            res = _rootPath.Initialize(in path);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }

        string rootPathNormalized;

        try
        {
            rootPathNormalized = System.IO.Path.GetFullPath(rootPath);
        }
        catch (PathTooLongException)
        {
            return ResultFs.TooLongPath.Log();
        }
        catch (Exception)
        {
            return ResultFs.InvalidCharacter.Log();
        }

        if (!Directory.Exists(rootPathNormalized))
        {
            if (!ensurePathExists || File.Exists(rootPathNormalized))
                return ResultFs.PathNotFound.Log();

            try
            {
                Directory.CreateDirectory(rootPathNormalized);
            }
            catch (Exception ex) when (ex.HResult < 0)
            {
                return HResult.HResultToHorizonResult(ex.HResult).Log();
            }
        }

        ReadOnlySpan<byte> utf8Path = StringUtils.StringToUtf8(rootPathNormalized);
        using var pathNormalized = new Path();

        if (utf8Path.At(0) == DirectorySeparator && utf8Path.At(1) != DirectorySeparator)
        {
            res = pathNormalized.Initialize(utf8Path);
            if (res.IsFailure()) return res.Miss();
        }
        else
        {
            res = pathNormalized.InitializeWithReplaceUnc(utf8Path);
            if (res.IsFailure()) return res.Miss();
        }

        var flags = new PathFlags();
        flags.AllowWindowsPath();
        flags.AllowRelativePath();
        flags.AllowEmptyPath();

        res = pathNormalized.Normalize(flags);
        if (res.IsFailure()) return res.Miss();

        res = _rootPath.Initialize(in pathNormalized);
        if (res.IsFailure()) return res.Miss();

        _rootPathUtf16 = _rootPath.ToString();

        return Result.Success;
    }

    private Result ResolveFullPath(out string outFullPath, ref readonly Path path, bool checkCaseSensitivity)
    {
        UnsafeHelpers.SkipParamInit(out outFullPath);

        // Always normalize the incoming path even if it claims to already be normalized
        // because we don't want to allow access to anything outside the root path.

        using var pathNormalized = new Path();
        Result res = pathNormalized.Initialize(path.GetString());
        if (res.IsFailure()) return res.Miss();

        var pathFlags = new PathFlags();
        pathFlags.AllowWindowsPath();
        pathFlags.AllowRelativePath();
        pathFlags.AllowEmptyPath();
        res = pathNormalized.Normalize(pathFlags);
        if (res.IsFailure()) return res.Miss();

        using Path rootPath = _rootPath.DangerousGetPath();

        using var fullPath = new Path();
        res = fullPath.Combine(in rootPath, in pathNormalized);
        if (res.IsFailure()) return res.Miss();

        string utf16FullPath = fullPath.ToString();

        if (_mode == PathMode.CaseSensitive && checkCaseSensitivity)
        {
            res = CheckPathCaseSensitively(utf16FullPath);
            if (res.IsFailure()) return res.Miss();
        }

        outFullPath = utf16FullPath;
        return Result.Success;
    }

    protected override Result DoGetFileAttributes(out NxFileAttributes attributes, ref readonly Path path)
    {
        UnsafeHelpers.SkipParamInit(out attributes);

        Result res = ResolveFullPath(out string fullPath, in path, true);
        if (res.IsFailure()) return res.Miss();

        res = GetFileInfo(out FileInfo info, fullPath);
        if (res.IsFailure()) return res.Miss();

        if (info.Attributes == (FileAttributes)(-1))
        {
            return ResultFs.PathNotFound.Log();
        }

        attributes = info.Attributes.ToNxAttributes();
        return Result.Success;
    }

    protected override Result DoSetFileAttributes(ref readonly Path path, NxFileAttributes attributes)
    {
        Result res = ResolveFullPath(out string fullPath, in path, true);
        if (res.IsFailure()) return res.Miss();

        res = GetFileInfo(out FileInfo info, fullPath);
        if (res.IsFailure()) return res.Miss();

        if (info.Attributes == (FileAttributes)(-1))
        {
            return ResultFs.PathNotFound.Log();
        }

        FileAttributes attributesOld = info.Attributes;
        FileAttributes attributesNew = attributesOld.ApplyNxAttributes(attributes);

        try
        {
            info.Attributes = attributesNew;
        }
        catch (IOException)
        {
            return ResultFs.PathNotFound.Log();
        }

        return Result.Success;
    }

    protected override Result DoGetFileSize(out long fileSize, ref readonly Path path)
    {
        UnsafeHelpers.SkipParamInit(out fileSize);

        Result res = ResolveFullPath(out string fullPath, in path, true);
        if (res.IsFailure()) return res.Miss();

        res = GetFileInfo(out FileInfo info, fullPath);
        if (res.IsFailure()) return res.Miss();

        return GetSizeInternal(out fileSize, info);
    }

    protected override Result DoCreateDirectory(ref readonly Path path)
    {
        return DoCreateDirectory(in path, NxFileAttributes.None);
    }

    protected override Result DoCreateDirectory(ref readonly Path path, NxFileAttributes archiveAttribute)
    {
        Result res = ResolveFullPath(out string fullPath, in path, false);
        if (res.IsFailure()) return res.Miss();

        res = GetDirInfo(out DirectoryInfo dir, fullPath);
        if (res.IsFailure()) return res.Miss();

        if (dir.Exists)
        {
            return ResultFs.PathAlreadyExists.Log();
        }

        if (dir.Parent?.Exists != true)
        {
            return ResultFs.PathNotFound.Log();
        }

        return CreateDirInternal(dir, archiveAttribute);
    }

    protected override Result DoCreateFile(ref readonly Path path, long size, CreateFileOptions option)
    {
        Result res = ResolveFullPath(out string fullPath, in path, false);
        if (res.IsFailure()) return res.Miss();

        res = GetFileInfo(out FileInfo file, fullPath);
        if (res.IsFailure()) return res.Miss();

        if (file.Exists)
        {
            return ResultFs.PathAlreadyExists.Log();
        }

        if (file.Directory?.Exists != true)
        {
            return ResultFs.PathNotFound.Log();
        }

        res = CreateFileInternal(out FileStream stream, file);

        using (stream)
        {
            if (res.IsFailure()) return res.Miss();

            return SetStreamLengthInternal(stream, size);
        }
    }

    protected override Result DoDeleteDirectory(ref readonly Path path)
    {
        Result res = ResolveFullPath(out string fullPath, in path, true);
        if (res.IsFailure()) return res.Miss();

        res = GetDirInfo(out DirectoryInfo dir, fullPath);
        if (res.IsFailure()) return res.Miss();

        return TargetLockedAvoidance.RetryToAvoidTargetLocked(
            () => DeleteDirectoryInternal(dir, false), _fsClient);
    }

    protected override Result DoDeleteDirectoryRecursively(ref readonly Path path)
    {
        Result res = ResolveFullPath(out string fullPath, in path, true);
        if (res.IsFailure()) return res.Miss();

        res = GetDirInfo(out DirectoryInfo dir, fullPath);
        if (res.IsFailure()) return res.Miss();

        return TargetLockedAvoidance.RetryToAvoidTargetLocked(
            () => DeleteDirectoryInternal(dir, true), _fsClient);
    }

    protected override Result DoCleanDirectoryRecursively(ref readonly Path path)
    {
        Result res = ResolveFullPath(out string fullPath, in path, true);
        if (res.IsFailure()) return res.Miss();

        res = GetDirInfo(out DirectoryInfo dir, fullPath);
        if (res.IsFailure()) return res.Miss();

        return CleanDirectoryInternal(dir, _fsClient);
    }

    protected override Result DoDeleteFile(ref readonly Path path)
    {
        Result res = ResolveFullPath(out string fullPath, in path, true);
        if (res.IsFailure()) return res.Miss();

        res = GetFileInfo(out FileInfo file, fullPath);
        if (res.IsFailure()) return res.Miss();

        return TargetLockedAvoidance.RetryToAvoidTargetLocked(
            () => DeleteFileInternal(file), _fsClient);
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, ref readonly Path path,
        OpenDirectoryMode mode)
    {
        Result res = ResolveFullPath(out string fullPath, in path, true);
        if (res.IsFailure()) return res.Miss();

        res = GetDirInfo(out DirectoryInfo dirInfo, fullPath);
        if (res.IsFailure()) return res.Miss();

        if (!dirInfo.Attributes.HasFlag(FileAttributes.Directory))
        {
            return ResultFs.PathNotFound.Log();
        }

        IDirectory dirTemp = null;
        res = TargetLockedAvoidance.RetryToAvoidTargetLocked(() =>
            OpenDirectoryInternal(out dirTemp, mode, dirInfo), _fsClient);
        if (res.IsFailure()) return res.Miss();

        outDirectory.Reset(dirTemp);
        return Result.Success;
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, ref readonly Path path, OpenMode mode)
    {
        Result res = ResolveFullPath(out string fullPath, in path, true);
        if (res.IsFailure()) return res.Miss();

        res = GetEntryType(out DirectoryEntryType entryType, in path);
        if (res.IsFailure()) return res.Miss();

        if (entryType == DirectoryEntryType.Directory)
        {
            return ResultFs.PathNotFound.Log();
        }

        FileStream fileStream = null;

        res = TargetLockedAvoidance.RetryToAvoidTargetLocked(() =>
            OpenFileInternal(out fileStream, fullPath, mode), _fsClient);
        if (res.IsFailure()) return res.Miss();

        outFile.Reset(new LocalFile(fileStream, mode));
        return Result.Success;
    }

    protected override Result DoRenameDirectory(ref readonly Path currentPath, ref readonly Path newPath)
    {
        Result res = ResolveFullPath(out string fullCurrentPath, in currentPath, true);
        if (res.IsFailure()) return res.Miss();

        res = ResolveFullPath(out string fullNewPath, in newPath, false);
        if (res.IsFailure()) return res.Miss();

        // Official FS behavior is to do nothing in this case
        if (fullCurrentPath == fullNewPath) return Result.Success;

        res = GetDirInfo(out DirectoryInfo currentDirInfo, fullCurrentPath);
        if (res.IsFailure()) return res.Miss();

        res = GetDirInfo(out DirectoryInfo newDirInfo, fullNewPath);
        if (res.IsFailure()) return res.Miss();

        return TargetLockedAvoidance.RetryToAvoidTargetLocked(
            () => RenameDirInternal(currentDirInfo, newDirInfo), _fsClient);
    }

    protected override Result DoRenameFile(ref readonly Path currentPath, ref readonly Path newPath)
    {
        Result res = ResolveFullPath(out string fullCurrentPath, in currentPath, true);
        if (res.IsFailure()) return res.Miss();

        res = ResolveFullPath(out string fullNewPath, in newPath, false);
        if (res.IsFailure()) return res.Miss();

        // Official FS behavior is to do nothing in this case
        if (fullCurrentPath == fullNewPath) return Result.Success;

        res = GetFileInfo(out FileInfo currentFileInfo, fullCurrentPath);
        if (res.IsFailure()) return res.Miss();

        res = GetFileInfo(out FileInfo newFileInfo, fullNewPath);
        if (res.IsFailure()) return res.Miss();

        return TargetLockedAvoidance.RetryToAvoidTargetLocked(
            () => RenameFileInternal(currentFileInfo, newFileInfo), _fsClient);
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, ref readonly Path path)
    {
        UnsafeHelpers.SkipParamInit(out entryType);

        Result res = ResolveFullPath(out string fullPath, in path, true);
        if (res.IsFailure()) return res.Miss();

        res = GetDirInfo(out DirectoryInfo dir, fullPath);
        if (res.IsFailure()) return res.Miss();

        if (dir.Exists)
        {
            entryType = DirectoryEntryType.Directory;
            return Result.Success;
        }

        res = GetFileInfo(out FileInfo file, fullPath);
        if (res.IsFailure()) return res.Miss();

        if (file.Exists)
        {
            entryType = DirectoryEntryType.File;
            return Result.Success;
        }

        return ResultFs.PathNotFound.Log();
    }

    protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, ref readonly Path path)
    {
        UnsafeHelpers.SkipParamInit(out timeStamp);

        Result res = ResolveFullPath(out string fullPath, in path, true);
        if (res.IsFailure()) return res.Miss();

        res = GetFileInfo(out FileInfo file, fullPath);
        if (res.IsFailure()) return res.Miss();

        if (!file.Exists) return ResultFs.PathNotFound.Log();

        if (_useUnixTime)
        {
            timeStamp.Created = new DateTimeOffset(file.CreationTimeUtc).ToUnixTimeSeconds();
            timeStamp.Accessed = new DateTimeOffset(file.LastAccessTimeUtc).ToUnixTimeSeconds();
            timeStamp.Modified = new DateTimeOffset(file.LastWriteTimeUtc).ToUnixTimeSeconds();
        }
        else
        {
            timeStamp.Created = new DateTimeOffset(file.CreationTimeUtc).ToFileTime();
            timeStamp.Accessed = new DateTimeOffset(file.LastAccessTimeUtc).ToFileTime();
            timeStamp.Modified = new DateTimeOffset(file.LastWriteTimeUtc).ToFileTime();
        }

        timeStamp.IsLocalTime = false;

        return Result.Success;
    }

    protected override Result DoGetFreeSpaceSize(out long freeSpace, ref readonly Path path)
    {
        UnsafeHelpers.SkipParamInit(out freeSpace);

        Result res = ResolveFullPath(out string fullPath, in path, true);
        if (res.IsFailure()) return res.Miss();

        freeSpace = new DriveInfo(fullPath).AvailableFreeSpace;
        return Result.Success;
    }

    protected override Result DoGetTotalSpaceSize(out long totalSpace, ref readonly Path path)
    {
        UnsafeHelpers.SkipParamInit(out totalSpace);

        Result res = ResolveFullPath(out string fullPath, in path, true);
        if (res.IsFailure()) return res.Miss();

        totalSpace = new DriveInfo(fullPath).TotalSize;
        return Result.Success;
    }

    protected override Result DoGetFileSystemAttribute(out FileSystemAttribute outAttribute)
    {
        const int winMaxPathComponentLength = 255;
        const int winMaxPathLength = 259;
        const int winMaxDirectoryPathLength = 247;
        
        outAttribute = default;

        outAttribute.Utf16DirectoryNameLengthMax = winMaxPathComponentLength;
        outAttribute.Utf16DirectoryNameLengthMaxHasValue = true;
        outAttribute.Utf16FileNameLengthMax = winMaxPathComponentLength;
        outAttribute.Utf16FileNameLengthMaxHasValue = true;
        outAttribute.Utf16DirectoryPathLengthMax = winMaxPathLength;
        outAttribute.Utf16DirectoryPathLengthMaxHasValue = true;
        outAttribute.Utf16FilePathLengthMax = winMaxPathLength;
        outAttribute.Utf16FilePathLengthMaxHasValue = true;
        outAttribute.Utf16CreateDirectoryPathLengthMax = winMaxDirectoryPathLength;
        outAttribute.Utf16CreateDirectoryPathLengthMaxHasValue = true;
        outAttribute.Utf16DeleteDirectoryPathLengthMax = winMaxPathLength;
        outAttribute.Utf16DeleteDirectoryPathLengthMaxHasValue = true;
        outAttribute.Utf16RenameSourceDirectoryPathLengthMax = winMaxDirectoryPathLength;
        outAttribute.Utf16RenameSourceDirectoryPathLengthMaxHasValue = true;
        outAttribute.Utf16RenameDestinationDirectoryPathLengthMax = winMaxDirectoryPathLength;
        outAttribute.Utf16RenameDestinationDirectoryPathLengthMaxHasValue = true;
        outAttribute.Utf16OpenDirectoryPathLengthMax = winMaxPathLength;
        outAttribute.Utf16OpenDirectoryPathLengthMaxHasValue = true;

        int rootPathCount = _rootPath.GetLength();

        Result res = Utility.CountUtf16CharacterForUtf8String(out ulong rootPathUtf16Count, _rootPath.GetString());
        if (res.IsFailure()) return res.Miss();

        Utility.SubtractAllPathLengthMax(ref outAttribute, rootPathCount);
        Utility.SubtractAllUtf16CountMax(ref outAttribute, (int)rootPathUtf16Count);

        return Result.Success;
    }

    protected override Result DoCommit()
    {
        return Result.Success;
    }

    protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
        ref readonly Path path)
    {
        return ResultFs.UnsupportedOperation.Log();
    }

    internal static FileAccess GetFileAccess(OpenMode mode)
    {
        // FileAccess and OpenMode have the same flags
        return (FileAccess)(mode & OpenMode.ReadWrite);
    }

    internal static FileShare GetFileShare(OpenMode mode)
    {
        return mode.HasFlag(OpenMode.Write) ? FileShare.Read : FileShare.ReadWrite;
    }

    internal static Result OpenFileInternal(out FileStream stream, string path, OpenMode mode)
    {
        try
        {
            stream = new FileStream(path, FileMode.Open, GetFileAccess(mode), GetFileShare(mode));
            return Result.Success;
        }
        catch (Exception ex) when (ex.HResult < 0)
        {
            UnsafeHelpers.SkipParamInit(out stream);
            return HResult.HResultToHorizonResult(ex.HResult).Log();
        }
    }

    private static Result OpenDirectoryInternal(out IDirectory directory, OpenDirectoryMode mode,
        DirectoryInfo dirInfo)
    {
        try
        {
            IEnumerator<FileSystemInfo> entryEnumerator = dirInfo.EnumerateFileSystemInfos().GetEnumerator();

            directory = new LocalDirectory(entryEnumerator, dirInfo, mode);
            return Result.Success;
        }
        catch (Exception ex) when (ex.HResult < 0)
        {
            UnsafeHelpers.SkipParamInit(out directory);
            return HResult.HResultToHorizonResult(ex.HResult).Log();
        }
    }

    private static Result GetSizeInternal(out long fileSize, FileInfo file)
    {
        UnsafeHelpers.SkipParamInit(out fileSize);

        try
        {
            fileSize = file.Length;
            return Result.Success;
        }
        catch (Exception ex) when (ex.HResult < 0)
        {
            return HResult.HResultToHorizonResult(ex.HResult).Log();
        }
    }

    private static Result CreateFileInternal(out FileStream file, FileInfo fileInfo)
    {
        UnsafeHelpers.SkipParamInit(out file);

        try
        {
            file = new FileStream(fileInfo.FullName, FileMode.CreateNew, FileAccess.ReadWrite);
        }
        catch (Exception ex) when (ex.HResult < 0)
        {
            return HResult.HResultToHorizonResult(ex.HResult).Log();
        }

        return Result.Success;
    }

    private static Result SetStreamLengthInternal(Stream stream, long size)
    {
        try
        {
            stream.SetLength(size);
        }
        catch (Exception ex) when (ex.HResult < 0)
        {
            return HResult.HResultToHorizonResult(ex.HResult).Log();
        }

        return Result.Success;
    }

    private static Result CleanDirectoryInternal(DirectoryInfo dir, FileSystemClient fsClient)
    {
        try
        {
            foreach (FileInfo fileInfo in dir.EnumerateFiles())
            {
                Result res = TargetLockedAvoidance.RetryToAvoidTargetLocked(() => DeleteFileInternal(fileInfo),
                    fsClient);
                if (res.IsFailure()) return res.Miss();
            }

            foreach (DirectoryInfo dirInfo in dir.EnumerateDirectories())
            {
                Result res = TargetLockedAvoidance.RetryToAvoidTargetLocked(() => DeleteDirectoryInternal(dirInfo, true),
                    fsClient);
                if (res.IsFailure()) return res.Miss();
            }
        }
        catch (Exception ex) when (ex.HResult < 0)
        {
            return HResult.HResultToHorizonResult(ex.HResult).Log();
        }

        return Result.Success;
    }

    private static Result DeleteDirectoryInternal(DirectoryInfo dir, bool recursive)
    {
        if (!dir.Exists)
            return ResultFs.PathNotFound.Log();

        try
        {
            try
            {
                dir.Delete(recursive);
            }
            catch (Exception ex) when (ex.HResult is HResult.COR_E_IO or HResult.ERROR_ACCESS_DENIED)
            {
                if (recursive)
                {
                    Result res = DeleteDirectoryRecursivelyWithReadOnly(dir);
                    if (res.IsFailure()) return res.Miss();
                }
                else
                {
                    // Try to delete read-only directories by first removing the read-only flag
                    if (dir.Attributes.HasFlag(FileAttributes.ReadOnly))
                    {
                        dir.Attributes &= ~FileAttributes.ReadOnly;
                    }

                    dir.Delete(false);
                }
            }
        }
        catch (Exception ex) when (ex.HResult < 0)
        {
            return HResult.HResultToHorizonResult(ex.HResult).Log();
        }

        return EnsureDeleted(dir);
    }

    private static Result DeleteFileInternal(FileInfo file)
    {
        if (!file.Exists)
            return ResultFs.PathNotFound.Log();

        try
        {
            try
            {
                file.Delete();
            }
            catch (UnauthorizedAccessException ex) when (ex.HResult == HResult.ERROR_ACCESS_DENIED)
            {
                // Try to delete read-only files by first removing the read-only flag.
                if (file.Attributes.HasFlag(FileAttributes.ReadOnly))
                {
                    file.Attributes &= ~FileAttributes.ReadOnly;
                }

                file.Delete();
            }
        }
        catch (Exception ex) when (ex.HResult < 0)
        {
            return HResult.HResultToHorizonResult(ex.HResult).Log();
        }

        return EnsureDeleted(file);
    }

    private static Result DeleteDirectoryRecursivelyWithReadOnly(DirectoryInfo rootDir)
    {
        try
        {
            foreach (FileSystemInfo info in rootDir.EnumerateFileSystemInfos())
            {
                if (info is FileInfo file)
                {
                    // Check each file for the read-only flag before deleting.
                    if (file.Attributes.HasFlag(FileAttributes.ReadOnly))
                    {
                        file.Attributes &= ~FileAttributes.ReadOnly;
                    }

                    file.Delete();
                }
                else if (info is DirectoryInfo dir)
                {
                    Result res = DeleteDirectoryRecursivelyWithReadOnly(dir);
                    if (res.IsFailure()) return res.Miss();
                }
                else
                {
                    return ResultFs.UnexpectedInLocalFileSystemF.Log();
                }
            }

            // The directory should be empty now. Remove any read-only flag and delete it.
            if (rootDir.Attributes.HasFlag(FileAttributes.ReadOnly))
            {
                rootDir.Attributes &= ~FileAttributes.ReadOnly;
            }

            rootDir.Delete(true);
        }
        catch (Exception ex) when (ex.HResult < 0)
        {
            return HResult.HResultToHorizonResult(ex.HResult).Log();
        }

        return Result.Success;
    }

    private static Result CreateDirInternal(DirectoryInfo dir, NxFileAttributes attributes)
    {
        try
        {
            dir.Create();
            dir.Refresh();

            if (attributes.HasFlag(NxFileAttributes.Archive))
            {
                dir.Attributes |= FileAttributes.Archive;
            }
        }
        catch (Exception ex) when (ex.HResult < 0)
        {
            return HResult.HResultToHorizonResult(ex.HResult).Log();
        }

        return Result.Success;
    }

    private static Result RenameDirInternal(DirectoryInfo source, DirectoryInfo dest)
    {
        if (!source.Exists) return ResultFs.PathNotFound.Log();
        if (dest.Exists) return ResultFs.PathAlreadyExists.Log();

        try
        {
            source.MoveTo(dest.FullName);
        }
        catch (Exception ex) when (ex.HResult < 0)
        {
            return HResult.HResultToHorizonResult(ex.HResult).Log();
        }

        return Result.Success;
    }

    private static Result RenameFileInternal(FileInfo source, FileInfo dest)
    {
        if (!source.Exists) return ResultFs.PathNotFound.Log();
        if (dest.Exists) return ResultFs.PathAlreadyExists.Log();

        try
        {
            source.MoveTo(dest.FullName);
        }
        catch (Exception ex) when (ex.HResult < 0)
        {
            return HResult.HResultToHorizonResult(ex.HResult).Log();
        }

        return Result.Success;
    }

    // GetFileInfo and GetDirInfo detect invalid paths
    private static Result GetFileInfo(out FileInfo fileInfo, string path)
    {
        UnsafeHelpers.SkipParamInit(out fileInfo);

        try
        {
            fileInfo = new FileInfo(path);
            return Result.Success;
        }
        catch (Exception ex) when (ex.HResult < 0)
        {
            return HResult.HResultToHorizonResult(ex.HResult).Log();
        }
    }

    private static Result GetDirInfo(out DirectoryInfo directoryInfo, string path)
    {
        UnsafeHelpers.SkipParamInit(out directoryInfo);

        try
        {
            directoryInfo = new DirectoryInfo(path);
            return Result.Success;
        }
        catch (Exception ex) when (ex.HResult < 0)
        {
            return HResult.HResultToHorizonResult(ex.HResult).Log();
        }
    }

    // Delete operations on IFileSystem should be synchronous
    // DeleteFile and RemoveDirectory only mark the file for deletion on Windows,
    // so we need to poll the filesystem until it's actually gone
    private static Result EnsureDeleted(FileSystemInfo entry)
    {
        const int noDelayRetryCount = 1000;
        const int delayRetryCount = 100;
        const int retryDelay = 10;

        // The entry is usually deleted within the first 5-10 tries
        for (int i = 0; i < noDelayRetryCount; i++)
        {
            entry.Refresh();

            if (!entry.Exists)
                return Result.Success;
        }

        for (int i = 0; i < delayRetryCount; i++)
        {
            Thread.Sleep(retryDelay);
            entry.Refresh();

            if (!entry.Exists)
                return Result.Success;
        }

        return ResultFs.TargetLocked.Log();
    }

    public static Result GetCaseSensitivePath(out int bytesWritten, Span<byte> buffer, U8Span path,
        U8Span workingDirectoryPath)
    {
        UnsafeHelpers.SkipParamInit(out bytesWritten);

        string pathUtf16 = StringUtils.Utf8ZToString(path);
        string workingDirectoryPathUtf16 = StringUtils.Utf8ZToString(workingDirectoryPath);

        Result res = GetCaseSensitivePathFull(out string caseSensitivePath, out int rootPathLength, pathUtf16,
            workingDirectoryPathUtf16);
        if (res.IsFailure()) return res.Miss();

        OperationStatus status = Utf8.FromUtf16(caseSensitivePath.AsSpan(rootPathLength),
            buffer.Slice(0, buffer.Length - 1), out _, out int utf8BytesWritten);

        if (status == OperationStatus.DestinationTooSmall)
            return ResultFs.TooLongPath.Log();

        if (status == OperationStatus.InvalidData || status == OperationStatus.NeedMoreData)
            return ResultFs.InvalidCharacter.Log();

        buffer[utf8BytesWritten] = NullTerminator;
        bytesWritten = utf8BytesWritten;

        return Result.Success;
    }

    private Result CheckPathCaseSensitively(string path)
    {
        Result res = GetCaseSensitivePathFull(out string caseSensitivePath, out _, path, _rootPathUtf16);
        if (res.IsFailure()) return res.Miss();

        if (path.Length != caseSensitivePath.Length)
            return ResultFs.PathNotFound.Log();

        for (int i = 0; i < path.Length; i++)
        {
            if (!(path[i] == caseSensitivePath[i] || WindowsPath.IsDosDelimiterW(path[i]) &&
                WindowsPath.IsDosDelimiterW(caseSensitivePath[i])))
            {
                return ResultFs.PathNotFound.Log();
            }
        }

        return Result.Success;
    }

    private static Result GetCaseSensitivePathFull(out string caseSensitivePath, out int rootPathLength,
        string path, string workingDirectoryPath)
    {
        caseSensitivePath = default;
        UnsafeHelpers.SkipParamInit(out rootPathLength);

        string fullPath;
        int workingDirectoryPathLength;

        if (WindowsPath.IsWindowsPathW(path))
        {
            fullPath = path;
            workingDirectoryPathLength = 0;
        }
        else
        {
            // We only want to send back the relative part of the path starting with a '/', so
            // track where the root path ends.
            if (WindowsPath.IsDosDelimiterW(workingDirectoryPath[^1]))
            {
                workingDirectoryPathLength = workingDirectoryPath.Length - 1;
            }
            else
            {
                workingDirectoryPathLength = workingDirectoryPath.Length;
            }

            fullPath = Combine(workingDirectoryPath, path);
        }

        Result res = GetCorrectCasedPath(out caseSensitivePath, fullPath);
        if (res.IsFailure()) return res.Miss();

        rootPathLength = workingDirectoryPathLength;
        return Result.Success;
    }

    private static string Combine(string path1, string path2)
    {
        if (path1 == null || path2 == null) throw new NullReferenceException();

        if (string.IsNullOrEmpty(path1)) return path2;
        if (string.IsNullOrEmpty(path2)) return path1;

        bool path1HasSeparator = WindowsPath.IsDosDelimiterW(path1[path1.Length - 1]);
        bool path2HasSeparator = WindowsPath.IsDosDelimiterW(path2[0]);

        if (!path1HasSeparator && !path2HasSeparator)
        {
            return path1 + DirectorySeparator + path2;
        }

        if (path1HasSeparator ^ path2HasSeparator)
        {
            return path1 + path2;
        }

        return path1 + path2.Substring(1);
    }

    private static readonly char[] SplitChars = [(char)DirectorySeparator, (char)AltDirectorySeparator];

    // Copyright (c) Microsoft Corporation.
    // Licensed under the MIT License.
    public static Result GetCorrectCasedPath(out string casedPath, string path)
    {
        UnsafeHelpers.SkipParamInit(out casedPath);

        string exactPath = string.Empty;
        int itemsToSkip = 0;
        if (WindowsPath.IsUncPathW(path))
        {
            // With the Split method, a UNC path like \\server\share, we need to skip
            // trying to enumerate the server and share, so skip the first two empty
            // strings, then server, and finally share name.
            itemsToSkip = 4;
        }

        foreach (string item in path.Split(SplitChars))
        {
            if (itemsToSkip-- > 0)
            {
                // This handles the UNC server and share and 8.3 short path syntax
                exactPath += item + (char)DirectorySeparator;
            }
            else if (string.IsNullOrEmpty(exactPath))
            {
                // This handles the drive letter or / root path start
                exactPath = item + (char)DirectorySeparator;
            }
            else if (string.IsNullOrEmpty(item))
            {
                // This handles the trailing slash case
                if (!exactPath.EndsWith((char)DirectorySeparator))
                {
                    exactPath += (char)DirectorySeparator;
                }

                break;
            }
            else if (item.Contains('~'))
            {
                // This handles short path names
                exactPath += (char)DirectorySeparator + item;
            }
            else
            {
                // Use GetFileSystemEntries to get the correct casing of this element
                try
                {
                    string[] entries = Directory.GetFileSystemEntries(exactPath, item);
                    if (entries.Length > 0)
                    {
                        int itemIndex = entries[0].LastIndexOf((char)AltDirectorySeparator);

                        // GetFileSystemEntries will return paths in the root directory in this format: C:/Foo
                        if (itemIndex == -1)
                        {
                            itemIndex = entries[0].LastIndexOf((char)DirectorySeparator);
                            exactPath += entries[0].Substring(itemIndex + 1);
                        }
                        else
                        {
                            exactPath += (char)DirectorySeparator + entries[0].Substring(itemIndex + 1);
                        }
                    }
                    else
                    {
                        // If previous call didn't return anything, something failed so we just return the path we were given
                        return ResultFs.PathNotFound.Log();
                    }
                }
                catch
                {
                    // If we can't enumerate, we stop and just return the original path
                    return ResultFs.PathNotFound.Log();
                }
            }
        }

        casedPath = exactPath;
        return Result.Success;
    }
}