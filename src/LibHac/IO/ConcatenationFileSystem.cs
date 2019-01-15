using System;
using System.Collections.Generic;
using System.IO;

namespace LibHac.IO
{
    public class ConcatenationFileSystem : IFileSystem
    {
        private IAttributeFileSystem BaseFileSystem { get; }
        private long SplitFileSize { get; } = 0xFFFF0000; // Hard-coded value used by FS

        public ConcatenationFileSystem(IAttributeFileSystem baseFileSystem)
        {
            BaseFileSystem = baseFileSystem;
        }

        internal bool IsSplitFile(string path)
        {
            FileAttributes attributes = BaseFileSystem.GetFileAttributes(path);

            return (attributes & FileAttributes.Directory) != 0 && (attributes & FileAttributes.Archive) != 0;
        }

        public void CreateDirectory(string path)
        {
            throw new NotImplementedException();
        }

        public void CreateFile(string path, long size)
        {
            throw new NotImplementedException();
        }

        public void DeleteDirectory(string path)
        {
            throw new NotImplementedException();
        }

        public void DeleteFile(string path)
        {
            throw new NotImplementedException();
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

        internal long GetSplitFileSize(string path)
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
