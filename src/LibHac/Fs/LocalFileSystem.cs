using System;
using System.IO;
using System.Security;

namespace LibHac.Fs
{
    public class LocalFileSystem : IAttributeFileSystem
    {
        private const int ErrorHandleDiskFull = unchecked((int)0x80070027);
        private const int ErrorFileExists = unchecked((int)0x80070050);
        private const int ErrorDiskFull = unchecked((int)0x80070070);
        private const int ErrorDirNotEmpty = unchecked((int)0x80070091);

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

        public NxFileAttributes GetFileAttributes(string path)
        {
            path = PathTools.Normalize(path);
            return File.GetAttributes(ResolveLocalPath(path)).ToNxAttributes();
        }

        public void SetFileAttributes(string path, NxFileAttributes attributes)
        {
            path = PathTools.Normalize(path);
            string localPath = ResolveLocalPath(path);

            FileAttributes attributesOld = File.GetAttributes(localPath);
            FileAttributes attributesNew = attributesOld.ApplyNxAttributes(attributes);

            File.SetAttributes(localPath, attributesNew);
        }

        public long GetFileSize(string path)
        {
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            FileInfo info = GetFileInfo(localPath);
            return GetSizeInternal(info);
        }

        public Result CreateDirectory(string path)
        {
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            DirectoryInfo dir = GetDirInfo(localPath);

            if (dir.Exists)
            {
                return ResultFs.PathAlreadyExists.Log();
            }

            if (dir.Parent?.Exists != true)
            {
                return ResultFs.PathNotFound.Log();
            }

            return CreateDirInternal(dir);
        }

        public Result CreateFile(string path, long size, CreateFileOptions options)
        {
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            FileInfo file = GetFileInfo(localPath);

            if (file.Exists)
            {
                return ResultFs.PathAlreadyExists.Log();
            }

            if (file.Directory?.Exists != true)
            {
                return ResultFs.PathNotFound.Log();
            }

            Result rc = CreateFileInternal(out FileStream stream, file);

            using (stream)
            {
                if (rc.IsFailure()) return rc;

                return SetStreamLengthInternal(stream, size);
            }
        }

        public Result DeleteDirectory(string path)
        {
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            DirectoryInfo dir = GetDirInfo(localPath);

            return DeleteDirectoryInternal(dir, false);
        }

        public Result DeleteDirectoryRecursively(string path)
        {
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            DirectoryInfo dir = GetDirInfo(localPath);

            return DeleteDirectoryInternal(dir, true);
        }

        public Result CleanDirectoryRecursively(string path)
        {
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            foreach (string file in Directory.EnumerateFiles(localPath))
            {
                Result rc = DeleteFileInternal(GetFileInfo(file));
                if (rc.IsFailure()) return rc;
            }

            foreach (string dir in Directory.EnumerateDirectories(localPath))
            {
                Result rc = DeleteDirectoryInternal(GetDirInfo(dir), true);
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
        }

        public Result DeleteFile(string path)
        {
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            FileInfo file = GetFileInfo(localPath);

            return DeleteFileInternal(file);
        }

        public Result OpenDirectory(out IDirectory directory, string path, OpenDirectoryMode mode)
        {
            // Getting the local path is done in the LocalDirectory constructor
            path = PathTools.Normalize(path);
            directory = default;

            Result rc = GetEntryType(out DirectoryEntryType entryType, path);
            if (rc.IsFailure()) return rc;

            if (entryType == DirectoryEntryType.File)
            {
                return ResultFs.PathNotFound.Log();
            }

            directory = new LocalDirectory(this, path, mode);
            return Result.Success;
        }

        public Result OpenFile(out IFile file, string path, OpenMode mode)
        {
            file = default;
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            Result rc = GetEntryType(out DirectoryEntryType entryType, path);
            if (rc.IsFailure()) return rc;

            if (entryType == DirectoryEntryType.Directory)
            {
                return ResultFs.PathNotFound.Log();
            }

            file = new LocalFile(localPath, mode);
            return Result.Success;
        }

        public Result RenameDirectory(string oldPath, string newPath)
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

            DirectoryInfo srcDir = GetDirInfo(ResolveLocalPath(oldPath));
            DirectoryInfo dstDir = GetDirInfo(ResolveLocalPath(newPath));

            return RenameDirInternal(srcDir, dstDir);
        }

        public Result RenameFile(string oldPath, string newPath)
        {
            string srcLocalPath = ResolveLocalPath(PathTools.Normalize(oldPath));
            string dstLocalPath = ResolveLocalPath(PathTools.Normalize(newPath));

            // Official FS behavior is to do nothing in this case
            if (srcLocalPath == dstLocalPath) return Result.Success;

            FileInfo srcFile = GetFileInfo(srcLocalPath);
            FileInfo dstFile = GetFileInfo(dstLocalPath);

            return RenameFileInternal(srcFile, dstFile);
        }

        public Result GetEntryType(out DirectoryEntryType entryType, string path)
        {
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            DirectoryInfo dir = GetDirInfo(localPath);

            if (dir.Exists)
            {
                entryType = DirectoryEntryType.Directory;
                return Result.Success;
            }

            FileInfo file = GetFileInfo(localPath);

            if (file.Exists)
            {
                entryType = DirectoryEntryType.File;
                return Result.Success;
            }

            entryType = DirectoryEntryType.NotFound;
            return ResultFs.PathNotFound.Log();
        }

        public Result GetFileTimeStampRaw(out FileTimeStampRaw timeStamp, string path)
        {
            timeStamp = default;
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            if (!GetFileInfo(localPath).Exists) return ResultFs.PathNotFound.Log();

            timeStamp.Created = new DateTimeOffset(File.GetCreationTime(localPath)).ToUnixTimeSeconds();
            timeStamp.Accessed = new DateTimeOffset(File.GetLastAccessTime(localPath)).ToUnixTimeSeconds();
            timeStamp.Modified = new DateTimeOffset(File.GetLastWriteTime(localPath)).ToUnixTimeSeconds();

            return Result.Success;
        }

        public Result GetFreeSpaceSize(out long freeSpace, string path)
        {
            freeSpace = new DriveInfo(BasePath).AvailableFreeSpace;
            return Result.Success;
        }

        public Result GetTotalSpaceSize(out long totalSpace, string path)
        {
            totalSpace = new DriveInfo(BasePath).TotalSize;
            return Result.Success;
        }

        public Result Commit()
        {
            return Result.Success;
        }

        public Result QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, string path)
        {
            return ResultFs.UnsupportedOperation.Log();
        }

        private static long GetSizeInternal(FileInfo file)
        {
            try
            {
                return file.Length;
            }
            catch (FileNotFoundException ex)
            {
                ThrowHelper.ThrowResult(ResultFs.PathNotFound, ex);
                throw;
            }
            catch (Exception ex) when (ex is SecurityException || ex is UnauthorizedAccessException)
            {
                // todo: Should a HorizonResultException be thrown?
                throw;
            }
        }

        private static Result CreateFileInternal(out FileStream file, FileInfo fileInfo)
        {
            file = default;

            try
            {
                file = new FileStream(fileInfo.FullName, FileMode.CreateNew, FileAccess.ReadWrite);
            }
            catch (DirectoryNotFoundException)
            {
                return ResultFs.PathNotFound.Log();
            }
            catch (IOException ex) when (ex.HResult == ErrorDiskFull || ex.HResult == ErrorHandleDiskFull)
            {
                return ResultFs.InsufficientFreeSpace.Log();
            }
            catch (IOException ex) when (ex.HResult == ErrorFileExists)
            {
                return ResultFs.PathAlreadyExists.Log();
            }
            catch (Exception ex) when (ex is SecurityException || ex is UnauthorizedAccessException)
            {
                // todo: What Result value should be returned?
                throw;
            }

            return Result.Success;
        }

        private static Result SetStreamLengthInternal(Stream stream, long size)
        {
            try
            {
                stream.SetLength(size);
            }
            catch (IOException ex) when (ex.HResult == ErrorDiskFull || ex.HResult == ErrorHandleDiskFull)
            {
                return ResultFs.InsufficientFreeSpace.Log();
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
            catch (DirectoryNotFoundException)
            {
                return ResultFs.PathNotFound.Log();
            }
            catch (IOException ex) when (ex.HResult == ErrorDirNotEmpty)
            {
                return ResultFs.DirectoryNotEmpty.Log();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // todo: What Result value should be returned?
                throw;
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
            catch (DirectoryNotFoundException)
            {
                return ResultFs.PathNotFound.Log();
            }
            catch (IOException ex) when (ex.HResult == ErrorDirNotEmpty)
            {
                return ResultFs.DirectoryNotEmpty.Log();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // todo: What Result value should be returned?
                throw;
            }

            EnsureDeleted(file);

            return Result.Success;
        }

        private static Result CreateDirInternal(DirectoryInfo dir)
        {
            try
            {
                dir.Create();
            }
            catch (DirectoryNotFoundException)
            {
                return ResultFs.PathNotFound.Log();
            }
            catch (IOException ex) when (ex.HResult == ErrorDiskFull || ex.HResult == ErrorHandleDiskFull)
            {
                return ResultFs.InsufficientFreeSpace.Log();
            }
            catch (Exception ex) when (ex is SecurityException || ex is UnauthorizedAccessException)
            {
                // todo: What Result value should be returned?
                throw;
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
            catch (DirectoryNotFoundException)
            {
                return ResultFs.PathNotFound.Log();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // todo: What Result value should be returned?
                throw;
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
            catch (DirectoryNotFoundException)
            {
                return ResultFs.PathNotFound.Log();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // todo: What Result value should be returned?
                throw;
            }

            return Result.Success;
        }


        // GetFileInfo and GetDirInfo detect invalid paths
        private static FileInfo GetFileInfo(string path)
        {
            try
            {
                return new FileInfo(path);
            }
            catch (Exception ex) when (ex is ArgumentNullException || ex is ArgumentException ||
                                       ex is PathTooLongException)
            {
                ThrowHelper.ThrowResult(ResultFs.PathNotFound, ex);
                throw;
            }
        }

        private static DirectoryInfo GetDirInfo(string path)
        {
            try
            {
                return new DirectoryInfo(path);
            }
            catch (Exception ex) when (ex is ArgumentNullException || ex is ArgumentException ||
                                       ex is PathTooLongException)
            {
                ThrowHelper.ThrowResult(ResultFs.PathNotFound, ex);
                throw;
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
