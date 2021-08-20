using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Unicode;
using System.Threading;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem.Impl;
using LibHac.Util;
using static LibHac.Fs.StringTraits;

namespace LibHac.FsSystem
{
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

        private string _rootPath;
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
            _rootPath = Path.GetFullPath(rootPath);

            if (!Directory.Exists(_rootPath))
            {
                if (File.Exists(_rootPath))
                {
                    throw new DirectoryNotFoundException($"The specified path is a file. ({rootPath})");
                }

                Directory.CreateDirectory(_rootPath);
            }
        }

        public static Result Create(out LocalFileSystem fileSystem, string rootPath,
            PathMode pathMode = PathMode.DefaultCaseSensitivity, bool ensurePathExists = true)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            var localFs = new LocalFileSystem();
            Result rc = localFs.Initialize(rootPath, pathMode, ensurePathExists);
            if (rc.IsFailure()) return rc;

            fileSystem = localFs;
            return Result.Success;
        }

        public Result Initialize(string rootPath, PathMode pathMode, bool ensurePathExists)
        {
            if (rootPath == null)
                return ResultFs.NullptrArgument.Log();

            _mode = pathMode;

            // If the root path is empty, we interpret any incoming paths as rooted paths.
            if (rootPath == string.Empty)
            {
                _rootPath = rootPath;
                return Result.Success;
            }

            try
            {
                _rootPath = Path.GetFullPath(rootPath);
            }
            catch (PathTooLongException)
            {
                return ResultFs.TooLongPath.Log();
            }
            catch (Exception)
            {
                return ResultFs.InvalidCharacter.Log();
            }

            if (!Directory.Exists(_rootPath))
            {
                if (!ensurePathExists || File.Exists(_rootPath))
                    return ResultFs.PathNotFound.Log();

                try
                {
                    Directory.CreateDirectory(_rootPath);
                }
                catch (Exception ex) when (ex.HResult < 0)
                {
                    return HResult.HResultToHorizonResult(ex.HResult).Log();
                }
            }

            return Result.Success;
        }

        private Result ResolveFullPath(out string fullPath, U8Span path, bool checkCaseSensitivity)
        {
            UnsafeHelpers.SkipParamInit(out fullPath);

            Unsafe.SkipInit(out FsPath normalizedPath);

            Result rc = PathNormalizer.Normalize(normalizedPath.Str, out _, path, false, false);
            if (rc.IsFailure()) return rc;

            fullPath = PathTools.Combine(_rootPath, normalizedPath.ToString());

            if (_mode == PathMode.CaseSensitive && checkCaseSensitivity)
            {
                rc = CheckPathCaseSensitively(fullPath);
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
        }

        private Result CheckSubPath(U8Span path1, U8Span path2)
        {
            Unsafe.SkipInit(out FsPath normalizedPath1);
            Unsafe.SkipInit(out FsPath normalizedPath2);

            Result rc = PathNormalizer.Normalize(normalizedPath1.Str, out _, path1, false, false);
            if (rc.IsFailure()) return rc;

            rc = PathNormalizer.Normalize(normalizedPath2.Str, out _, path2, false, false);
            if (rc.IsFailure()) return rc;

            if (PathUtility.IsSubPath(normalizedPath1, normalizedPath2))
            {
                return ResultFs.DirectoryNotRenamable.Log();
            }

            return Result.Success;
        }

        protected override Result DoGetFileAttributes(out NxFileAttributes attributes, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out attributes);

            Result rc = ResolveFullPath(out string fullPath, path, true);
            if (rc.IsFailure()) return rc;

            rc = GetFileInfo(out FileInfo info, fullPath);
            if (rc.IsFailure()) return rc;

            if (info.Attributes == (FileAttributes)(-1))
            {
                return ResultFs.PathNotFound.Log();
            }

            attributes = info.Attributes.ToNxAttributes();
            return Result.Success;
        }

        protected override Result DoSetFileAttributes(U8Span path, NxFileAttributes attributes)
        {
            Result rc = ResolveFullPath(out string fullPath, path, true);
            if (rc.IsFailure()) return rc;

            rc = GetFileInfo(out FileInfo info, fullPath);
            if (rc.IsFailure()) return rc;

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

        protected override Result DoGetFileSize(out long fileSize, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out fileSize);

            Result rc = ResolveFullPath(out string fullPath, path, true);
            if (rc.IsFailure()) return rc;

            rc = GetFileInfo(out FileInfo info, fullPath);
            if (rc.IsFailure()) return rc;

            return GetSizeInternal(out fileSize, info);
        }

        protected override Result DoCreateDirectory(U8Span path)
        {
            return DoCreateDirectory(path, NxFileAttributes.None);
        }

        protected override Result DoCreateDirectory(U8Span path, NxFileAttributes archiveAttribute)
        {
            Result rc = ResolveFullPath(out string fullPath, path, false);
            if (rc.IsFailure()) return rc;

            rc = GetDirInfo(out DirectoryInfo dir, fullPath);
            if (rc.IsFailure()) return rc;

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

        protected override Result DoCreateFile(U8Span path, long size, CreateFileOptions options)
        {
            Result rc = ResolveFullPath(out string fullPath, path, false);
            if (rc.IsFailure()) return rc;

            rc = GetFileInfo(out FileInfo file, fullPath);
            if (rc.IsFailure()) return rc;

            if (file.Exists)
            {
                return ResultFs.PathAlreadyExists.Log();
            }

            if (file.Directory?.Exists != true)
            {
                return ResultFs.PathNotFound.Log();
            }

            rc = CreateFileInternal(out FileStream stream, file);

            using (stream)
            {
                if (rc.IsFailure()) return rc;

                return SetStreamLengthInternal(stream, size);
            }
        }

        protected override Result DoDeleteDirectory(U8Span path)
        {
            Result rc = ResolveFullPath(out string fullPath, path, true);
            if (rc.IsFailure()) return rc;

            rc = GetDirInfo(out DirectoryInfo dir, fullPath);
            if (rc.IsFailure()) return rc;

            return TargetLockedAvoidance.RetryToAvoidTargetLocked(
                () => DeleteDirectoryInternal(dir, false), _fsClient);
        }

        protected override Result DoDeleteDirectoryRecursively(U8Span path)
        {
            Result rc = ResolveFullPath(out string fullPath, path, true);
            if (rc.IsFailure()) return rc;

            rc = GetDirInfo(out DirectoryInfo dir, fullPath);
            if (rc.IsFailure()) return rc;

            return TargetLockedAvoidance.RetryToAvoidTargetLocked(
                () => DeleteDirectoryInternal(dir, true), _fsClient);
        }

        protected override Result DoCleanDirectoryRecursively(U8Span path)
        {
            Result rc = ResolveFullPath(out string fullPath, path, true);
            if (rc.IsFailure()) return rc;

            rc = GetDirInfo(out DirectoryInfo dir, fullPath);
            if (rc.IsFailure()) return rc;

            return CleanDirectoryInternal(dir, _fsClient);
        }

        protected override Result DoDeleteFile(U8Span path)
        {
            Result rc = ResolveFullPath(out string fullPath, path, true);
            if (rc.IsFailure()) return rc;

            rc = GetFileInfo(out FileInfo file, fullPath);
            if (rc.IsFailure()) return rc;

            return TargetLockedAvoidance.RetryToAvoidTargetLocked(
                () => DeleteFileInternal(file), _fsClient);
        }

        protected override Result DoOpenDirectory(out IDirectory directory, U8Span path, OpenDirectoryMode mode)
        {
            UnsafeHelpers.SkipParamInit(out directory);
            Result rc = ResolveFullPath(out string fullPath, path, true);
            if (rc.IsFailure()) return rc;

            rc = GetDirInfo(out DirectoryInfo dirInfo, fullPath);
            if (rc.IsFailure()) return rc;

            if (!dirInfo.Attributes.HasFlag(FileAttributes.Directory))
            {
                return ResultFs.PathNotFound.Log();
            }

            IDirectory dirTemp = null;
            rc = TargetLockedAvoidance.RetryToAvoidTargetLocked(() =>
                OpenDirectoryInternal(out dirTemp, mode, dirInfo), _fsClient);
            if (rc.IsFailure()) return rc;

            directory = dirTemp;
            return Result.Success;
        }

        protected override Result DoOpenFile(out IFile file, U8Span path, OpenMode mode)
        {
            UnsafeHelpers.SkipParamInit(out file);

            Result rc = ResolveFullPath(out string fullPath, path, true);
            if (rc.IsFailure()) return rc;

            rc = GetEntryType(out DirectoryEntryType entryType, path);
            if (rc.IsFailure()) return rc;

            if (entryType == DirectoryEntryType.Directory)
            {
                return ResultFs.PathNotFound.Log();
            }

            FileStream fileStream = null;

            rc = TargetLockedAvoidance.RetryToAvoidTargetLocked(() =>
                OpenFileInternal(out fileStream, fullPath, mode), _fsClient);
            if (rc.IsFailure()) return rc;

            file = new LocalFile(fileStream, mode);
            return Result.Success;
        }

        protected override Result DoRenameDirectory(U8Span oldPath, U8Span newPath)
        {
            Result rc = CheckSubPath(oldPath, newPath);
            if (rc.IsFailure()) return rc;

            rc = ResolveFullPath(out string fullCurrentPath, oldPath, true);
            if (rc.IsFailure()) return rc;

            rc = ResolveFullPath(out string fullNewPath, newPath, false);
            if (rc.IsFailure()) return rc;

            // Official FS behavior is to do nothing in this case
            if (fullCurrentPath == fullNewPath) return Result.Success;

            rc = GetDirInfo(out DirectoryInfo currentDirInfo, fullCurrentPath);
            if (rc.IsFailure()) return rc;

            rc = GetDirInfo(out DirectoryInfo newDirInfo, fullNewPath);
            if (rc.IsFailure()) return rc;

            return TargetLockedAvoidance.RetryToAvoidTargetLocked(
                () => RenameDirInternal(currentDirInfo, newDirInfo), _fsClient);
        }

        protected override Result DoRenameFile(U8Span oldPath, U8Span newPath)
        {
            Result rc = ResolveFullPath(out string fullCurrentPath, oldPath, true);
            if (rc.IsFailure()) return rc;

            rc = ResolveFullPath(out string fullNewPath, newPath, false);
            if (rc.IsFailure()) return rc;

            // Official FS behavior is to do nothing in this case
            if (fullCurrentPath == fullNewPath) return Result.Success;

            rc = GetFileInfo(out FileInfo currentFileInfo, fullCurrentPath);
            if (rc.IsFailure()) return rc;

            rc = GetFileInfo(out FileInfo newFileInfo, fullNewPath);
            if (rc.IsFailure()) return rc;

            return TargetLockedAvoidance.RetryToAvoidTargetLocked(
                () => RenameFileInternal(currentFileInfo, newFileInfo), _fsClient);
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out entryType);

            Result rc = ResolveFullPath(out string fullPath, path, true);
            if (rc.IsFailure()) return rc;

            rc = GetDirInfo(out DirectoryInfo dir, fullPath);
            if (rc.IsFailure()) return rc;

            if (dir.Exists)
            {
                entryType = DirectoryEntryType.Directory;
                return Result.Success;
            }

            rc = GetFileInfo(out FileInfo file, fullPath);
            if (rc.IsFailure()) return rc;

            if (file.Exists)
            {
                entryType = DirectoryEntryType.File;
                return Result.Success;
            }

            return ResultFs.PathNotFound.Log();
        }

        protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out timeStamp);

            Result rc = ResolveFullPath(out string fullPath, path, true);
            if (rc.IsFailure()) return rc;

            rc = GetFileInfo(out FileInfo file, fullPath);
            if (rc.IsFailure()) return rc;

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

        protected override Result DoGetFreeSpaceSize(out long freeSpace, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out freeSpace);

            Result rc = ResolveFullPath(out string fullPath, path, true);
            if (rc.IsFailure()) return rc;

            freeSpace = new DriveInfo(fullPath).AvailableFreeSpace;
            return Result.Success;
        }

        protected override Result DoGetTotalSpaceSize(out long totalSpace, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out totalSpace);

            Result rc = ResolveFullPath(out string fullPath, path, true);
            if (rc.IsFailure()) return rc;

            totalSpace = new DriveInfo(fullPath).TotalSize;
            return Result.Success;
        }

        protected override Result DoCommit()
        {
            return Result.Success;
        }

        protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
            U8Span path)
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
                    Result rc = TargetLockedAvoidance.RetryToAvoidTargetLocked(() => DeleteFileInternal(fileInfo),
                        fsClient);
                    if (rc.IsFailure()) return rc;
                }

                foreach (DirectoryInfo dirInfo in dir.EnumerateDirectories())
                {
                    Result rc = TargetLockedAvoidance.RetryToAvoidTargetLocked(() => DeleteDirectoryInternal(dirInfo, true),
                        fsClient);
                    if (rc.IsFailure()) return rc;
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
                dir.Delete(recursive);
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
                file.Delete();
            }
            catch (Exception ex) when (ex.HResult < 0)
            {
                return HResult.HResultToHorizonResult(ex.HResult).Log();
            }

            return EnsureDeleted(file);
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

            Result rc = GetCaseSensitivePathFull(out string caseSensitivePath, out int rootPathLength, pathUtf16,
                workingDirectoryPathUtf16);
            if (rc.IsFailure()) return rc;

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
            Result rc = GetCaseSensitivePathFull(out string caseSensitivePath, out _, path, _rootPath);
            if (rc.IsFailure()) return rc;

            if (path.Length != caseSensitivePath.Length)
                return ResultFs.PathNotFound.Log();

            for (int i = 0; i < path.Length; i++)
            {
                if (!(path[i] == caseSensitivePath[i] || WindowsPath.IsDosDelimiter(path[i]) &&
                    WindowsPath.IsDosDelimiter(caseSensitivePath[i])))
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

            if (WindowsPath.IsPathRooted(path))
            {
                fullPath = path;
                workingDirectoryPathLength = 0;
            }
            else
            {
                // We only want to send back the relative part of the path starting with a '/', so
                // track where the root path ends.
                if (WindowsPath.IsDosDelimiter(workingDirectoryPath[^1]))
                {
                    workingDirectoryPathLength = workingDirectoryPath.Length - 1;
                }
                else
                {
                    workingDirectoryPathLength = workingDirectoryPath.Length;
                }

                fullPath = Combine(workingDirectoryPath, path);
            }

            Result rc = GetCorrectCasedPath(out caseSensitivePath, fullPath);
            if (rc.IsFailure()) return rc;

            rootPathLength = workingDirectoryPathLength;
            return Result.Success;
        }

        private static string Combine(string path1, string path2)
        {
            if (path1 == null || path2 == null) throw new NullReferenceException();

            if (string.IsNullOrEmpty(path1)) return path2;
            if (string.IsNullOrEmpty(path2)) return path1;

            bool path1HasSeparator = WindowsPath.IsDosDelimiter(path1[path1.Length - 1]);
            bool path2HasSeparator = WindowsPath.IsDosDelimiter(path2[0]);

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

        private static readonly char[] SplitChars = { (char)DirectorySeparator, (char)AltDirectorySeparator };

        // Copyright (c) Microsoft Corporation.
        // Licensed under the MIT License.
        public static Result GetCorrectCasedPath(out string casedPath, string path)
        {
            UnsafeHelpers.SkipParamInit(out casedPath);

            string exactPath = string.Empty;
            int itemsToSkip = 0;
            if (WindowsPath.IsUnc(path))
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
}
