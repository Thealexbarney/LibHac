using System;
using System.Collections.Generic;
using System.IO;

namespace LibHac.IO
{
    public class ConcatenationFileSystem : IFileSystem
    {
        private const long DefaultSplitFileSize = 0xFFFF0000; // Hard-coded value used by FS
        private IAttributeFileSystem BaseFileSystem { get; }
        private long SplitFileSize { get; }

        public ConcatenationFileSystem(IAttributeFileSystem baseFileSystem) : this(baseFileSystem, DefaultSplitFileSize) { }

        public ConcatenationFileSystem(IAttributeFileSystem baseFileSystem, long splitFileSize)
        {
            BaseFileSystem = baseFileSystem;
            SplitFileSize = splitFileSize;
        }

        internal bool IsSplitFile(string path)
        {
            FileAttributes attributes = BaseFileSystem.GetFileAttributes(path);

            return (attributes & FileAttributes.Directory) != 0 && (attributes & FileAttributes.Archive) != 0;
        }

        public void CreateDirectory(string path)
        {
            path = PathTools.Normalize(path);

            if (FileExists(path))
            {
                throw new IOException("Cannot create directory because a file with this name already exists.");
            }

            BaseFileSystem.CreateDirectory(path);
        }

        public void CreateFile(string path, long size, CreateFileOptions options)
        {
            path = PathTools.Normalize(path);

            CreateFileOptions newOptions = options & ~CreateFileOptions.CreateConcatenationFile;

            if (!options.HasFlag(CreateFileOptions.CreateConcatenationFile))
            {
                BaseFileSystem.CreateFile(path, size, newOptions);
                return;
            }

            // A concatenation file directory can't contain normal files
            string parentDir = PathTools.GetParentDirectory(path);
            if (IsSplitFile(parentDir)) throw new IOException("Cannot create files inside of a concatenation file");

            BaseFileSystem.CreateDirectory(path);
            FileAttributes attributes = BaseFileSystem.GetFileAttributes(path) | FileAttributes.Archive;
            BaseFileSystem.SetFileAttributes(path, attributes);

            long remaining = size;

            for (int i = 0; remaining > 0; i++)
            {
                long fileSize = Math.Min(SplitFileSize, remaining);
                string fileName = GetSplitFilePath(path, i);

                BaseFileSystem.CreateFile(fileName, fileSize, CreateFileOptions.None);

                remaining -= fileSize;
            }
        }

        public void DeleteDirectory(string path)
        {
            path = PathTools.Normalize(path);

            if (IsSplitFile(path))
            {
                throw new DirectoryNotFoundException(path);
            }

            BaseFileSystem.DeleteDirectory(path);
        }

        public void DeleteFile(string path)
        {
            path = PathTools.Normalize(path);

            if (!IsSplitFile(path))
            {
                BaseFileSystem.DeleteFile(path);
            }

            int count = GetSplitFileCount(path);

            for (int i = 0; i < count; i++)
            {
                BaseFileSystem.DeleteFile(GetSplitFilePath(path, i));
            }

            BaseFileSystem.DeleteDirectory(path);
        }

        public IDirectory OpenDirectory(string path, OpenDirectoryMode mode)
        {
            path = PathTools.Normalize(path);

            if (IsSplitFile(path))
            {
                throw new DirectoryNotFoundException(path);
            }

            IDirectory parentDir = BaseFileSystem.OpenDirectory(path, OpenDirectoryMode.All);
            var dir = new ConcatenationDirectory(this, parentDir, mode);
            return dir;
        }

        public IFile OpenFile(string path, OpenMode mode)
        {
            path = PathTools.Normalize(path);

            if (!IsSplitFile(path))
            {
                return BaseFileSystem.OpenFile(path, mode);
            }

            int fileCount = GetSplitFileCount(path);

            var files = new List<IFile>();

            for (int i = 0; i < fileCount; i++)
            {
                string filePath = GetSplitFilePath(path, i);
                IFile file = BaseFileSystem.OpenFile(filePath, mode);
                files.Add(file);
            }

            return new ConcatenationFile(files, SplitFileSize, mode);
        }

        public void RenameDirectory(string srcPath, string dstPath)
        {
            srcPath = PathTools.Normalize(srcPath);
            dstPath = PathTools.Normalize(dstPath);

            if (IsSplitFile(srcPath))
            {
                throw new DirectoryNotFoundException();
            }

            BaseFileSystem.RenameDirectory(srcPath, dstPath);
        }

        public void RenameFile(string srcPath, string dstPath)
        {
            srcPath = PathTools.Normalize(srcPath);
            dstPath = PathTools.Normalize(dstPath);

            if (IsSplitFile(srcPath))
            {
                BaseFileSystem.RenameDirectory(srcPath, dstPath);
            }
            else
            {
                BaseFileSystem.RenameFile(srcPath, dstPath);
            }
        }

        public bool DirectoryExists(string path)
        {
            path = PathTools.Normalize(path);

            return BaseFileSystem.DirectoryExists(path) && !IsSplitFile(path);
        }

        public bool FileExists(string path)
        {
            path = PathTools.Normalize(path);

            return BaseFileSystem.FileExists(path) || BaseFileSystem.DirectoryExists(path) && IsSplitFile(path);
        }

        public DirectoryEntryType GetEntryType(string path)
        {
            path = PathTools.Normalize(path);

            if (IsSplitFile(path)) return DirectoryEntryType.File;

            return BaseFileSystem.GetEntryType(path);
        }

        public void Commit()
        {
            BaseFileSystem.Commit();
        }

        private int GetSplitFileCount(string dirPath)
        {
            int count = 0;

            while (BaseFileSystem.FileExists(GetSplitFilePath(dirPath, count)))
            {
                count++;
            }

            return count;
        }

        private static string GetSplitFilePath(string dirPath, int index)
        {
            return $"{dirPath}/{index:D2}";
        }

        internal long GetConcatenationFileSize(string path)
        {
            int fileCount = GetSplitFileCount(path);
            long size = 0;

            for (int i = 0; i < fileCount; i++)
            {
                size += BaseFileSystem.GetFileSize(GetSplitFilePath(path, i));
            }

            return size;
        }
    }
}
