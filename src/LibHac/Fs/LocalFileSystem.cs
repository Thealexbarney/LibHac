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

        public void CreateDirectory(string path)
        {
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            DirectoryInfo dir = GetDirInfo(localPath);

            if (dir.Exists)
            {
                ThrowHelper.ThrowResult(ResultFs.PathAlreadyExists);
            }

            if (dir.Parent?.Exists != true)
            {
                ThrowHelper.ThrowResult(ResultFs.PathNotFound);
            }

            CreateDirInternal(dir);
        }

        public void CreateFile(string path, long size, CreateFileOptions options)
        {
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            FileInfo file = GetFileInfo(localPath);

            if (file.Exists)
            {
                ThrowHelper.ThrowResult(ResultFs.PathAlreadyExists);
            }

            if (file.Directory?.Exists != true)
            {
                ThrowHelper.ThrowResult(ResultFs.PathNotFound);
            }

            using (FileStream stream = CreateFileInternal(file))
            {
                SetStreamLengthInternal(stream, size);
            }
        }

        public void DeleteDirectory(string path)
        {
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            DirectoryInfo dir = GetDirInfo(localPath);

            DeleteDirectoryInternal(dir, false);
        }

        public void DeleteDirectoryRecursively(string path)
        {
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            DirectoryInfo dir = GetDirInfo(localPath);

            DeleteDirectoryInternal(dir, true);
        }

        public void CleanDirectoryRecursively(string path)
        {
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            foreach (string file in Directory.EnumerateFiles(localPath))
            {
                DeleteFileInternal(GetFileInfo(file));
            }

            foreach (string dir in Directory.EnumerateDirectories(localPath))
            {
                DeleteDirectoryInternal(GetDirInfo(dir), true);
            }
        }

        public void DeleteFile(string path)
        {
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            FileInfo file = GetFileInfo(localPath);

            DeleteFileInternal(file);
        }

        public IDirectory OpenDirectory(string path, OpenDirectoryMode mode)
        {
            path = PathTools.Normalize(path);

            if (GetEntryType(path) == DirectoryEntryType.File)
            {
                ThrowHelper.ThrowResult(ResultFs.PathNotFound);
            }

            return new LocalDirectory(this, path, mode);
        }

        public IFile OpenFile(string path, OpenMode mode)
        {
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            if (GetEntryType(path) == DirectoryEntryType.Directory)
            {
                ThrowHelper.ThrowResult(ResultFs.PathNotFound);
            }

            return new LocalFile(localPath, mode);
        }

        public void RenameDirectory(string srcPath, string dstPath)
        {
            srcPath = PathTools.Normalize(srcPath);
            dstPath = PathTools.Normalize(dstPath);

            // Official FS behavior is to do nothing in this case
            if (srcPath == dstPath) return;

            // FS does the subpath check before verifying the path exists
            if (PathTools.IsSubPath(srcPath.AsSpan(), dstPath.AsSpan()))
            {
                ThrowHelper.ThrowResult(ResultFs.DestinationIsSubPathOfSource);
            }

            DirectoryInfo srcDir = GetDirInfo(ResolveLocalPath(srcPath));
            DirectoryInfo dstDir = GetDirInfo(ResolveLocalPath(dstPath));

            RenameDirInternal(srcDir, dstDir);
        }

        public void RenameFile(string srcPath, string dstPath)
        {
            string srcLocalPath = ResolveLocalPath(PathTools.Normalize(srcPath));
            string dstLocalPath = ResolveLocalPath(PathTools.Normalize(dstPath));

            // Official FS behavior is to do nothing in this case
            if (srcLocalPath == dstLocalPath) return;

            FileInfo srcFile = GetFileInfo(srcLocalPath);
            FileInfo dstFile = GetFileInfo(dstLocalPath);

            RenameFileInternal(srcFile, dstFile);
        }

        public DirectoryEntryType GetEntryType(string path)
        {
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            DirectoryInfo dir = GetDirInfo(localPath);

            if (dir.Exists)
            {
                return DirectoryEntryType.Directory;
            }

            FileInfo file = GetFileInfo(localPath);

            if (file.Exists)
            {
                return DirectoryEntryType.File;
            }

            return DirectoryEntryType.NotFound;
        }

        public FileTimeStampRaw GetFileTimeStampRaw(string path)
        {
            string localPath = ResolveLocalPath(PathTools.Normalize(path));

            if (!GetFileInfo(localPath).Exists) ThrowHelper.ThrowResult(ResultFs.PathNotFound);

            FileTimeStampRaw timeStamp = default;

            timeStamp.Created = new DateTimeOffset(File.GetCreationTime(localPath)).ToUnixTimeSeconds();
            timeStamp.Accessed = new DateTimeOffset(File.GetLastAccessTime(localPath)).ToUnixTimeSeconds();
            timeStamp.Modified = new DateTimeOffset(File.GetLastWriteTime(localPath)).ToUnixTimeSeconds();

            return timeStamp;
        }

        public long GetFreeSpaceSize(string path)
        {
            return new DriveInfo(BasePath).AvailableFreeSpace;
        }

        public long GetTotalSpaceSize(string path)
        {
            return new DriveInfo(BasePath).TotalSize;
        }

        public void Commit() { }

        public void QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, string path, QueryId queryId) =>
            ThrowHelper.ThrowResult(ResultFs.UnsupportedOperation);

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

        private static FileStream CreateFileInternal(FileInfo file)
        {
            try
            {
                return new FileStream(file.FullName, FileMode.CreateNew, FileAccess.ReadWrite);
            }
            catch (DirectoryNotFoundException ex)
            {
                ThrowHelper.ThrowResult(ResultFs.PathNotFound, ex);
                throw;
            }
            catch (IOException ex) when (ex.HResult == ErrorDiskFull || ex.HResult == ErrorHandleDiskFull)
            {
                ThrowHelper.ThrowResult(ResultFs.InsufficientFreeSpace, ex);
                throw;
            }
            catch (IOException ex) when (ex.HResult == ErrorFileExists)
            {
                ThrowHelper.ThrowResult(ResultFs.PathAlreadyExists, ex);
                throw;
            }
            catch (Exception ex) when (ex is SecurityException || ex is UnauthorizedAccessException)
            {
                // todo: Should a HorizonResultException be thrown?
                throw;
            }
        }

        private static void SetStreamLengthInternal(Stream stream, long size)
        {
            try
            {
                stream.SetLength(size);
            }
            catch (IOException ex) when (ex.HResult == ErrorDiskFull || ex.HResult == ErrorHandleDiskFull)
            {
                ThrowHelper.ThrowResult(ResultFs.InsufficientFreeSpace, ex);
                throw;
            }
        }

        private static void DeleteDirectoryInternal(DirectoryInfo dir, bool recursive)
        {
            if (!dir.Exists) ThrowHelper.ThrowResult(ResultFs.PathNotFound);

            try
            {
                dir.Delete(recursive);
            }
            catch (DirectoryNotFoundException ex)
            {
                ThrowHelper.ThrowResult(ResultFs.PathNotFound, ex);
                throw;
            }
            catch (IOException ex) when (ex.HResult == ErrorDirNotEmpty)
            {
                ThrowHelper.ThrowResult(ResultFs.DirectoryNotEmpty, ex);
                throw;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // todo: Should a HorizonResultException be thrown?
                throw;
            }

            EnsureDeleted(dir);
        }

        private static void DeleteFileInternal(FileInfo file)
        {
            if (!file.Exists) ThrowHelper.ThrowResult(ResultFs.PathNotFound);

            try
            {
                file.Delete();
            }
            catch (DirectoryNotFoundException ex)
            {
                ThrowHelper.ThrowResult(ResultFs.PathNotFound, ex);
                throw;
            }
            catch (IOException ex) when (ex.HResult == ErrorDirNotEmpty)
            {
                ThrowHelper.ThrowResult(ResultFs.DirectoryNotEmpty, ex);
                throw;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // todo: Should a HorizonResultException be thrown?
                throw;
            }

            EnsureDeleted(file);
        }

        private static void CreateDirInternal(DirectoryInfo dir)
        {
            try
            {
                dir.Create();
            }
            catch (DirectoryNotFoundException ex)
            {
                ThrowHelper.ThrowResult(ResultFs.PathNotFound, ex);
                throw;
            }
            catch (IOException ex) when (ex.HResult == ErrorDiskFull || ex.HResult == ErrorHandleDiskFull)
            {
                ThrowHelper.ThrowResult(ResultFs.InsufficientFreeSpace, ex);
                throw;
            }
            catch (Exception ex) when (ex is SecurityException || ex is UnauthorizedAccessException)
            {
                // todo: Should a HorizonResultException be thrown?
                throw;
            }
        }

        private static void RenameDirInternal(DirectoryInfo source, DirectoryInfo dest)
        {
            if (!source.Exists) ThrowHelper.ThrowResult(ResultFs.PathNotFound);
            if (dest.Exists) ThrowHelper.ThrowResult(ResultFs.PathAlreadyExists);

            try
            {
                source.MoveTo(dest.FullName);
            }
            catch (DirectoryNotFoundException ex)
            {
                ThrowHelper.ThrowResult(ResultFs.PathNotFound, ex);
                throw;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // todo: Should a HorizonResultException be thrown?
                throw;
            }
        }

        private static void RenameFileInternal(FileInfo source, FileInfo dest)
        {
            if (!source.Exists) ThrowHelper.ThrowResult(ResultFs.PathNotFound);
            if (dest.Exists) ThrowHelper.ThrowResult(ResultFs.PathAlreadyExists);

            try
            {
                source.MoveTo(dest.FullName);
            }
            catch (DirectoryNotFoundException ex)
            {
                ThrowHelper.ThrowResult(ResultFs.PathNotFound, ex);
                throw;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // todo: Should a HorizonResultException be thrown?
                throw;
            }
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
