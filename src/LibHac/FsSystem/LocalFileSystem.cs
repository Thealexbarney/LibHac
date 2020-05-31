﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class LocalFileSystem : AttributeFileSystemBase
    {
        private string BasePath { get; }

        /// <summary>
        /// Opens a directory on local storage as an <see cref="IFileSystem"/>.
        /// The directory will be created if it does not exist.
        /// </summary>
        /// <param name="basePath">The path that will be the root of the <see cref="LocalFileSystem"/>.</param>
        public LocalFileSystem(string basePath)
        {
            BasePath = Path.GetFullPath(basePath);

            if (!Directory.Exists(BasePath))
            {
                if (File.Exists(BasePath))
                {
                    throw new DirectoryNotFoundException($"The specified path is a file. ({basePath})");
                }

                Directory.CreateDirectory(BasePath);
            }
        }

        private Result ResolveFullPath(out string fullPath, U8Span path)
        {
            fullPath = default;

            FsPath normalizedPath;
            unsafe { _ = &normalizedPath; } // workaround for CS0165

            Result rc = PathTool.Normalize(normalizedPath.Str, out _, path, false, false);
            if (rc.IsFailure()) return rc;

            fullPath = PathTools.Combine(BasePath, normalizedPath.ToString());
            return Result.Success;
        }

        private Result CheckSubPath(U8Span path1, U8Span path2)
        {
            FsPath normalizedPath1;
            FsPath normalizedPath2;
            unsafe { _ = &normalizedPath1; } // workaround for CS0165
            unsafe { _ = &normalizedPath2; } // workaround for CS0165

            Result rc = PathTool.Normalize(normalizedPath1.Str, out _, path1, false, false);
            if (rc.IsFailure()) return rc;

            rc = PathTool.Normalize(normalizedPath2.Str, out _, path2, false, false);
            if (rc.IsFailure()) return rc;

            if (PathTool.IsSubpath(normalizedPath1, normalizedPath2))
            {
                return ResultFs.DestinationIsSubPathOfSource.Log();
            }

            return Result.Success;
        }

        protected override Result GetFileAttributesImpl(out NxFileAttributes attributes, U8Span path)
        {
            attributes = default;

            Result rc = ResolveFullPath(out string fullPath, path);
            if (rc.IsFailure()) return rc;

            rc = GetFileInfo(out FileInfo info, fullPath);
            if (rc.IsFailure()) return rc;

            if (info.Attributes == (FileAttributes)(-1))
            {
                attributes = default;
                return ResultFs.PathNotFound.Log();
            }

            attributes = info.Attributes.ToNxAttributes();
            return Result.Success;
        }

        protected override Result SetFileAttributesImpl(U8Span path, NxFileAttributes attributes)
        {
            Result rc = ResolveFullPath(out string fullPath, path);
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

        protected override Result GetFileSizeImpl(out long fileSize, U8Span path)
        {
            fileSize = default;

            Result rc = ResolveFullPath(out string fullPath, path);
            if (rc.IsFailure()) return rc;

            rc = GetFileInfo(out FileInfo info, fullPath);
            if (rc.IsFailure()) return rc;

            return GetSizeInternal(out fileSize, info);
        }

        protected override Result CreateDirectoryImpl(U8Span path)
        {
            return CreateDirectory(path, NxFileAttributes.None);
        }

        protected override Result CreateDirectoryImpl(U8Span path, NxFileAttributes archiveAttribute)
        {
            Result rc = ResolveFullPath(out string fullPath, path);
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

        protected override Result CreateFileImpl(U8Span path, long size, CreateFileOptions options)
        {
            Result rc = ResolveFullPath(out string fullPath, path);
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

        protected override Result DeleteDirectoryImpl(U8Span path)
        {
            Result rc = ResolveFullPath(out string fullPath, path);
            if (rc.IsFailure()) return rc;

            rc = GetDirInfo(out DirectoryInfo dir, fullPath);
            if (rc.IsFailure()) return rc;

            return DeleteDirectoryInternal(dir, false);
        }

        protected override Result DeleteDirectoryRecursivelyImpl(U8Span path)
        {
            Result rc = ResolveFullPath(out string fullPath, path);
            if (rc.IsFailure()) return rc;

            rc = GetDirInfo(out DirectoryInfo dir, fullPath);
            if (rc.IsFailure()) return rc;

            return DeleteDirectoryInternal(dir, true);
        }

        protected override Result CleanDirectoryRecursivelyImpl(U8Span path)
        {
            Result rc = ResolveFullPath(out string fullPath, path);
            if (rc.IsFailure()) return rc;

            foreach (string file in Directory.EnumerateFiles(fullPath))
            {
                rc = GetFileInfo(out FileInfo fileInfo, file);
                if (rc.IsFailure()) return rc;

                rc = DeleteFileInternal(fileInfo);
                if (rc.IsFailure()) return rc;
            }

            foreach (string dir in Directory.EnumerateDirectories(fullPath))
            {
                rc = GetDirInfo(out DirectoryInfo dirInfo, dir);
                if (rc.IsFailure()) return rc;

                rc = DeleteDirectoryInternal(dirInfo, true);
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
        }

        protected override Result DeleteFileImpl(U8Span path)
        {
            Result rc = ResolveFullPath(out string fullPath, path);
            if (rc.IsFailure()) return rc;

            rc = GetFileInfo(out FileInfo file, fullPath);
            if (rc.IsFailure()) return rc;

            return DeleteFileInternal(file);
        }

        protected override Result OpenDirectoryImpl(out IDirectory directory, U8Span path, OpenDirectoryMode mode)
        {
            directory = default;
            Result rc = ResolveFullPath(out string fullPath, path);
            if (rc.IsFailure()) return rc;

            rc = GetDirInfo(out DirectoryInfo dirInfo, fullPath);
            if (rc.IsFailure()) return rc;

            if (!dirInfo.Attributes.HasFlag(FileAttributes.Directory))
            {
                return ResultFs.PathNotFound.Log();
            }

            try
            {
                IEnumerator<FileSystemInfo> entryEnumerator = dirInfo.EnumerateFileSystemInfos().GetEnumerator();

                directory = new LocalDirectory(entryEnumerator, dirInfo, mode);
                return Result.Success;
            }
            catch (Exception ex) when (ex.HResult < 0)
            {
                return HResult.HResultToHorizonResult(ex.HResult).Log();
            }
        }

        protected override Result OpenFileImpl(out IFile file, U8Span path, OpenMode mode)
        {
            file = default;

            Result rc = ResolveFullPath(out string fullPath, path);
            if (rc.IsFailure()) return rc;

            rc = GetEntryType(out DirectoryEntryType entryType, path);
            if (rc.IsFailure()) return rc;

            if (entryType == DirectoryEntryType.Directory)
            {
                return ResultFs.PathNotFound.Log();
            }

            rc = OpenFileInternal(out FileStream fileStream, fullPath, mode);
            if (rc.IsFailure()) return rc;

            file = new LocalFile(fileStream, mode);
            return Result.Success;
        }

        protected override Result RenameDirectoryImpl(U8Span oldPath, U8Span newPath)
        {
            Result rc = CheckSubPath(oldPath, newPath);
            if (rc.IsFailure()) return rc;

            rc = ResolveFullPath(out string fullCurrentPath, oldPath);
            if (rc.IsFailure()) return rc;

            rc = ResolveFullPath(out string fullNewPath, newPath);
            if (rc.IsFailure()) return rc;

            // Official FS behavior is to do nothing in this case
            if (fullCurrentPath == fullNewPath) return Result.Success;

            rc = GetDirInfo(out DirectoryInfo currentDirInfo, fullCurrentPath);
            if (rc.IsFailure()) return rc;

            rc = GetDirInfo(out DirectoryInfo newDirInfo, fullNewPath);
            if (rc.IsFailure()) return rc;

            return RenameDirInternal(currentDirInfo, newDirInfo);
        }

        protected override Result RenameFileImpl(U8Span oldPath, U8Span newPath)
        {
            Result rc = ResolveFullPath(out string fullCurrentPath, oldPath);
            if (rc.IsFailure()) return rc;

            rc = ResolveFullPath(out string fullNewPath, newPath);
            if (rc.IsFailure()) return rc;

            // Official FS behavior is to do nothing in this case
            if (fullCurrentPath == fullNewPath) return Result.Success;

            rc = GetFileInfo(out FileInfo currentFileInfo, fullCurrentPath);
            if (rc.IsFailure()) return rc;

            rc = GetFileInfo(out FileInfo newFileInfo, fullNewPath);
            if (rc.IsFailure()) return rc;

            return RenameFileInternal(currentFileInfo, newFileInfo);
        }

        protected override Result GetEntryTypeImpl(out DirectoryEntryType entryType, U8Span path)
        {
            entryType = default;

            Result rc = ResolveFullPath(out string fullPath, path);
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

            entryType = default;
            return ResultFs.PathNotFound.Log();
        }

        protected override Result GetFileTimeStampRawImpl(out FileTimeStampRaw timeStamp, U8Span path)
        {
            timeStamp = default;

            Result rc = ResolveFullPath(out string fullPath, path);
            if (rc.IsFailure()) return rc;

            rc = GetFileInfo(out FileInfo file, fullPath);
            if (rc.IsFailure()) return rc;

            if (!file.Exists) return ResultFs.PathNotFound.Log();

            timeStamp.Created = new DateTimeOffset(file.CreationTimeUtc).ToUnixTimeSeconds();
            timeStamp.Accessed = new DateTimeOffset(file.LastAccessTimeUtc).ToUnixTimeSeconds();
            timeStamp.Modified = new DateTimeOffset(file.LastWriteTime).ToUnixTimeSeconds();

            return Result.Success;
        }

        protected override Result GetFreeSpaceSizeImpl(out long freeSpace, U8Span path)
        {
            freeSpace = new DriveInfo(BasePath).AvailableFreeSpace;
            return Result.Success;
        }

        protected override Result GetTotalSpaceSizeImpl(out long totalSpace, U8Span path)
        {
            totalSpace = new DriveInfo(BasePath).TotalSize;
            return Result.Success;
        }

        protected override Result CommitImpl()
        {
            return Result.Success;
        }

        protected override Result QueryEntryImpl(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
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
                stream = default;
                return HResult.HResultToHorizonResult(ex.HResult).Log();
            }
        }

        private static Result GetSizeInternal(out long fileSize, FileInfo file)
        {
            try
            {
                fileSize = file.Length;
                return Result.Success;
            }
            catch (Exception ex) when (ex.HResult < 0)
            {
                fileSize = default;
                return HResult.HResultToHorizonResult(ex.HResult).Log();
            }
        }

        private static Result CreateFileInternal(out FileStream file, FileInfo fileInfo)
        {
            file = default;

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

        private static Result DeleteDirectoryInternal(DirectoryInfo dir, bool recursive)
        {
            if (!dir.Exists) return ResultFs.PathNotFound.Log();

            try
            {
                dir.Delete(recursive);
            }
            catch (Exception ex) when (ex.HResult < 0)
            {
                return HResult.HResultToHorizonResult(ex.HResult).Log();
            }

            EnsureDeleted(dir);

            return Result.Success;
        }

        private static Result DeleteFileInternal(FileInfo file)
        {
            if (!file.Exists) return ResultFs.PathNotFound.Log();

            try
            {
                file.Delete();
            }
            catch (Exception ex) when (ex.HResult < 0)
            {
                return HResult.HResultToHorizonResult(ex.HResult).Log();
            }

            EnsureDeleted(file);

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
            try
            {
                fileInfo = new FileInfo(path);
                return Result.Success;
            }
            catch (Exception ex) when (ex.HResult < 0)
            {
                fileInfo = default;
                return HResult.HResultToHorizonResult(ex.HResult).Log();
            }
        }

        private static Result GetDirInfo(out DirectoryInfo directoryInfo, string path)
        {
            try
            {
                directoryInfo = new DirectoryInfo(path);
                return Result.Success;
            }
            catch (Exception ex) when (ex.HResult < 0)
            {
                directoryInfo = default;
                return HResult.HResultToHorizonResult(ex.HResult).Log();
            }
        }

        // Delete operations on IFileSystem should be synchronous
        // DeleteFile and RemoveDirectory only mark the file for deletion on Windows,
        // so we need to poll the filesystem until it's actually gone
        private static void EnsureDeleted(FileSystemInfo entry)
        {
            const int noDelayRetryCount = 1000;
            const int retryDelay = 500;

            // The entry is usually deleted within the first 5-10 tries
            for (int i = 0; i < noDelayRetryCount; i++)
            {
                entry.Refresh();

                if (!entry.Exists)
                    return;
            }

            // Nintendo's solution is to check every 500 ms with no timeout
            while (entry.Exists)
            {
                Thread.Sleep(retryDelay);
                entry.Refresh();
            }
        }
    }
}
