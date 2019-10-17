using System;
using System.Collections.Generic;
using System.IO;
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
                Directory.CreateDirectory(BasePath);
            }
        }

        internal string ResolveLocalPath(string path)
        {
            return PathTools.Combine(BasePath, path);
        }

        protected override Result GetFileAttributesImpl(string path, out NxFileAttributes attributes)
        {
            attributes = default;

            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            Result rc = GetFileInfo(out FileInfo info, localPath);
            if (rc.IsFailure()) return rc;

            if (info.Attributes == (FileAttributes)(-1))
            {
                attributes = default;
                return ResultFs.PathNotFound.Log();
            }

            attributes = info.Attributes.ToNxAttributes();
            return Result.Success;
        }

        protected override Result SetFileAttributesImpl(string path, NxFileAttributes attributes)
        {
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            Result rc = GetFileInfo(out FileInfo info, localPath);
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

        protected override Result GetFileSizeImpl(out long fileSize, string path)
        {
            fileSize = default;
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            Result rc = GetFileInfo(out FileInfo info, localPath);
            if (rc.IsFailure()) return rc;

            return GetSizeInternal(out fileSize, info);
        }

        protected override Result CreateDirectoryImpl(string path)
        {
            return CreateDirectory(path, NxFileAttributes.None);
        }

        protected override Result CreateDirectoryImpl(string path, NxFileAttributes archiveAttribute)
        {
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            Result rc = GetDirInfo(out DirectoryInfo dir, localPath);
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

        protected override Result CreateFileImpl(string path, long size, CreateFileOptions options)
        {
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            Result rc = GetFileInfo(out FileInfo file, localPath);
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

        protected override Result DeleteDirectoryImpl(string path)
        {
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            Result rc = GetDirInfo(out DirectoryInfo dir, localPath);
            if (rc.IsFailure()) return rc;

            return DeleteDirectoryInternal(dir, false);
        }

        protected override Result DeleteDirectoryRecursivelyImpl(string path)
        {
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            Result rc = GetDirInfo(out DirectoryInfo dir, localPath);
            if (rc.IsFailure()) return rc;

            return DeleteDirectoryInternal(dir, true);
        }

        protected override Result CleanDirectoryRecursivelyImpl(string path)
        {
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            foreach (string file in Directory.EnumerateFiles(localPath))
            {
                Result rc = GetFileInfo(out FileInfo fileInfo, file);
                if (rc.IsFailure()) return rc;

                rc = DeleteFileInternal(fileInfo);
                if (rc.IsFailure()) return rc;
            }

            foreach (string dir in Directory.EnumerateDirectories(localPath))
            {
                Result rc = GetDirInfo(out DirectoryInfo dirInfo, dir);
                if (rc.IsFailure()) return rc;

                rc = DeleteDirectoryInternal(dirInfo, true);
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
        }

        protected override Result DeleteFileImpl(string path)
        {
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            Result rc = GetFileInfo(out FileInfo file, localPath);
            if (rc.IsFailure()) return rc;

            return DeleteFileInternal(file);
        }

        protected override Result OpenDirectoryImpl(out IDirectory directory, string path, OpenDirectoryMode mode)
        {
            directory = default;
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            Result rc = GetDirInfo(out DirectoryInfo dirInfo, localPath);
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

        protected override Result OpenFileImpl(out IFile file, string path, OpenMode mode)
        {
            file = default;
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            Result rc = GetEntryType(out DirectoryEntryType entryType, path);
            if (rc.IsFailure()) return rc;

            if (entryType == DirectoryEntryType.Directory)
            {
                return ResultFs.PathNotFound.Log();
            }

            rc = OpenFileInternal(out FileStream fileStream, localPath, mode);
            if (rc.IsFailure()) return rc;

            file = new LocalFile(fileStream, mode);
            return Result.Success;
        }

        protected override Result RenameDirectoryImpl(string oldPath, string newPath)
        {
            oldPath = PathTools.Normalize(oldPath);
            newPath = PathTools.Normalize(newPath);

            // Official FS behavior is to do nothing in this case
            if (oldPath == newPath) return Result.Success;

            // FS does the subpath check before verifying the path exists
            if (PathTools.IsSubPath(oldPath.AsSpan(), newPath.AsSpan()))
            {
                ThrowHelper.ThrowResult(ResultFs.DestinationIsSubPathOfSource);
            }

            Result rc = GetDirInfo(out DirectoryInfo srcDir, ResolveLocalPath(oldPath));
            if (rc.IsFailure()) return rc;

            rc = GetDirInfo(out DirectoryInfo dstDir, ResolveLocalPath(newPath));
            if (rc.IsFailure()) return rc;

            return RenameDirInternal(srcDir, dstDir);
        }

        protected override Result RenameFileImpl(string oldPath, string newPath)
        {
            string srcLocalPath = ResolveLocalPath(PathTools.Normalize(oldPath));
            string dstLocalPath = ResolveLocalPath(PathTools.Normalize(newPath));

            // Official FS behavior is to do nothing in this case
            if (srcLocalPath == dstLocalPath) return Result.Success;

            Result rc = GetFileInfo(out FileInfo srcFile, srcLocalPath);
            if (rc.IsFailure()) return rc;

            rc = GetFileInfo(out FileInfo dstFile, dstLocalPath);
            if (rc.IsFailure()) return rc;

            return RenameFileInternal(srcFile, dstFile);
        }

        protected override Result GetEntryTypeImpl(out DirectoryEntryType entryType, string path)
        {
            entryType = default;
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            Result rc = GetDirInfo(out DirectoryInfo dir, localPath);
            if (rc.IsFailure()) return rc;

            if (dir.Exists)
            {
                entryType = DirectoryEntryType.Directory;
                return Result.Success;
            }

            rc = GetFileInfo(out FileInfo file, localPath);
            if (rc.IsFailure()) return rc;

            if (file.Exists)
            {
                entryType = DirectoryEntryType.File;
                return Result.Success;
            }

            entryType = DirectoryEntryType.NotFound;
            return ResultFs.PathNotFound.Log();
        }

        protected override Result GetFileTimeStampRawImpl(out FileTimeStampRaw timeStamp, string path)
        {
            timeStamp = default;
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            Result rc = GetFileInfo(out FileInfo file, localPath);
            if (rc.IsFailure()) return rc;

            if (!file.Exists) return ResultFs.PathNotFound.Log();

            timeStamp.Created = new DateTimeOffset(File.GetCreationTime(localPath)).ToUnixTimeSeconds();
            timeStamp.Accessed = new DateTimeOffset(File.GetLastAccessTime(localPath)).ToUnixTimeSeconds();
            timeStamp.Modified = new DateTimeOffset(File.GetLastWriteTime(localPath)).ToUnixTimeSeconds();

            return Result.Success;
        }

        protected override Result GetFreeSpaceSizeImpl(out long freeSpace, string path)
        {
            freeSpace = new DriveInfo(BasePath).AvailableFreeSpace;
            return Result.Success;
        }

        protected override Result GetTotalSpaceSizeImpl(out long totalSpace, string path)
        {
            totalSpace = new DriveInfo(BasePath).TotalSize;
            return Result.Success;
        }

        protected override Result CommitImpl()
        {
            return Result.Success;
        }

        protected override Result QueryEntryImpl(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, string path)
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
        // DeleteFile and RemoveDirectory only mark the file for deletion, so we need
        // to poll the filesystem until it's actually gone
        private static void EnsureDeleted(FileSystemInfo entry)
        {
            int tries = 0;

            do
            {
                entry.Refresh();
                tries++;

                if (tries > 1000)
                {
                    throw new IOException($"Unable to delete file {entry.FullName}");
                }
            } while (entry.Exists);
        }
    }
}
