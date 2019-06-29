using System;
using System.IO;

namespace LibHac.Fs
{
    public class LocalFileSystem : IAttributeFileSystem
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
            path = PathTools.Normalize(path);
            var info = new FileInfo(ResolveLocalPath(path));
            return info.Length;
        }

        public void CreateDirectory(string path)
        {
            path = PathTools.Normalize(path);
            Directory.CreateDirectory(ResolveLocalPath(path));
        }

        public void CreateFile(string path, long size, CreateFileOptions options)
        {
            path = PathTools.Normalize(path);
            string localPath = ResolveLocalPath(path);
            string localDir = ResolveLocalPath(PathTools.GetParentDirectory(path));

            if (localDir != null) Directory.CreateDirectory(localDir);

            using (FileStream stream = File.Create(localPath))
            {
                stream.SetLength(size);
            }
        }

        public void DeleteDirectory(string path)
        {
            path = PathTools.Normalize(path);

            Directory.Delete(ResolveLocalPath(path));
        }

        public void DeleteDirectoryRecursively(string path)
        {
            path = PathTools.Normalize(path);

            Directory.Delete(ResolveLocalPath(path), true);
        }

        public void CleanDirectoryRecursively(string path)
        {
            path = PathTools.Normalize(path);
            string localPath = ResolveLocalPath(path);

            foreach (string file in Directory.EnumerateFiles(localPath))
            {
                File.Delete(file);
            }

            foreach (string dir in Directory.EnumerateDirectories(localPath))
            {
                Directory.Delete(dir, true);
            }
        }

        public void DeleteFile(string path)
        {
            path = PathTools.Normalize(path);

            string resolveLocalPath = ResolveLocalPath(path);
            File.Delete(resolveLocalPath);
        }

        public IDirectory OpenDirectory(string path, OpenDirectoryMode mode)
        {
            path = PathTools.Normalize(path);

            return new LocalDirectory(this, path, mode);
        }

        public IFile OpenFile(string path, OpenMode mode)
        {
            path = PathTools.Normalize(path);

            string localPath = ResolveLocalPath(path);
            return new LocalFile(localPath, mode);
        }

        public void RenameDirectory(string srcPath, string dstPath)
        {
            srcPath = PathTools.Normalize(srcPath);
            dstPath = PathTools.Normalize(dstPath);

            string srcLocalPath = ResolveLocalPath(srcPath);
            string dstLocalPath = ResolveLocalPath(dstPath);

            string directoryName = ResolveLocalPath(PathTools.GetParentDirectory(dstPath));
            if (directoryName != null) Directory.CreateDirectory(directoryName);
            Directory.Move(srcLocalPath, dstLocalPath);
        }

        public void RenameFile(string srcPath, string dstPath)
        {
            srcPath = PathTools.Normalize(srcPath);
            dstPath = PathTools.Normalize(dstPath);

            string srcLocalPath = ResolveLocalPath(srcPath);
            string dstLocalPath = ResolveLocalPath(dstPath);
            string dstLocalDir = ResolveLocalPath(PathTools.GetParentDirectory(dstPath));

            if (dstLocalDir != null) Directory.CreateDirectory(dstLocalDir);
            File.Move(srcLocalPath, dstLocalPath);
        }

        public bool DirectoryExists(string path)
        {
            path = PathTools.Normalize(path);

            return Directory.Exists(ResolveLocalPath(path));
        }

        public bool FileExists(string path)
        {
            path = PathTools.Normalize(path);

            return File.Exists(ResolveLocalPath(path));
        }

        public DirectoryEntryType GetEntryType(string path)
        {
            path = PathTools.Normalize(path);
            string localPath = ResolveLocalPath(path);

            if (Directory.Exists(localPath))
            {
                return DirectoryEntryType.Directory;
            }

            if (File.Exists(localPath))
            {
                return DirectoryEntryType.File;
            }

            ThrowHelper.ThrowResult(ResultFs.PathNotFound);
            return DirectoryEntryType.NotFound;
        }

        public FileTimeStampRaw GetFileTimeStampRaw(string path)
        {
            path = PathTools.Normalize(path);
            string localPath = ResolveLocalPath(path);

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
    }
}
