using System;
using System.Collections.Generic;
using System.Linq;

namespace LibHac.Fs.Accessors
{
    public class FileSystemAccessor
    {
        public string Name { get; }

        private IFileSystem FileSystem { get; }
        internal FileSystemManager FsManager { get; }

        private HashSet<FileAccessor> OpenFiles { get; } = new HashSet<FileAccessor>();
        private HashSet<DirectoryAccessor> OpenDirectories { get; } = new HashSet<DirectoryAccessor>();

        private readonly object _locker = new object();

        internal bool IsAccessLogEnabled { get; set; }

        public FileSystemAccessor(string name, IFileSystem baseFileSystem, FileSystemManager fsManager)
        {
            Name = name;
            FileSystem = baseFileSystem;
            FsManager = fsManager;
        }

        public void CreateDirectory(string path)
        {
            FileSystem.CreateDirectory(path);
        }

        public void CreateFile(string path, long size, CreateFileOptions options)
        {
            FileSystem.CreateFile(path, size, options);
        }

        public void DeleteDirectory(string path)
        {
            FileSystem.DeleteDirectory(path);
        }

        public void DeleteDirectoryRecursively(string path)
        {
            FileSystem.DeleteDirectoryRecursively(path);
        }

        public void CleanDirectoryRecursively(string path)
        {
            FileSystem.CleanDirectoryRecursively(path);
        }

        public void DeleteFile(string path)
        {
            FileSystem.DeleteFile(path);
        }

        public DirectoryAccessor OpenDirectory(string path, OpenDirectoryMode mode)
        {
            IDirectory dir = FileSystem.OpenDirectory(path, mode);

            var accessor = new DirectoryAccessor(dir, this);

            lock (_locker)
            {
                OpenDirectories.Add(accessor);
            }

            return accessor;
        }

        public FileAccessor OpenFile(string path, OpenMode mode)
        {
            IFile file = FileSystem.OpenFile(path, mode);

            var accessor = new FileAccessor(file, this, mode);

            lock (_locker)
            {
                OpenFiles.Add(accessor);
            }

            return accessor;
        }

        public void RenameDirectory(string srcPath, string dstPath)
        {
            FileSystem.RenameDirectory(srcPath, dstPath);
        }

        public void RenameFile(string srcPath, string dstPath)
        {
            FileSystem.RenameFile(srcPath, dstPath);
        }

        public void DirectoryExists(string path)
        {
            FileSystem.DirectoryExists(path);
        }

        public void FileExists(string path)
        {
            FileSystem.FileExists(path);
        }

        public DirectoryEntryType GetEntryType(string path)
        {
            return FileSystem.GetEntryType(path);
        }

        public long GetFreeSpaceSize(string path)
        {
            return FileSystem.GetFreeSpaceSize(path);
        }

        public long GetTotalSpaceSize(string path)
        {
            return FileSystem.GetTotalSpaceSize(path);
        }

        public FileTimeStampRaw GetFileTimeStampRaw(string path)
        {
            return FileSystem.GetFileTimeStampRaw(path);
        }

        public void Commit()
        {
            if (OpenFiles.Any(x => (x.OpenMode & OpenMode.Write) != 0))
            {
                ThrowHelper.ThrowResult(ResultFs.WritableFileOpen);
            }

            FileSystem.Commit();
        }

        public void QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, string path, QueryId queryId)
        {
            FileSystem.QueryEntry(outBuffer, inBuffer, path, queryId);
        }

        internal void NotifyCloseFile(FileAccessor file)
        {
            lock (_locker)
            {
                OpenFiles.Remove(file);
            }
        }

        internal void NotifyCloseDirectory(DirectoryAccessor directory)
        {
            lock (_locker)
            {
                OpenDirectories.Remove(directory);
            }
        }

        internal void Close()
        {
            // Todo: Possibly check for open files and directories
            // Nintendo checks for them in DumpUnclosedAccessorList in
            // FileSystemAccessor's destructor, but doesn't do anything with it
        }
    }
}
