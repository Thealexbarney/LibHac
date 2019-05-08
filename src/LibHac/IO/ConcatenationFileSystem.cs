using System;
using System.Collections.Generic;
using System.IO;

namespace LibHac.IO
{
    public class ConcatenationFileSystem : IFileSystem
    {
        private const long DefaultSubFileSize = 0xFFFF0000; // Hard-coded value used by FS
        private IAttributeFileSystem BaseFileSystem { get; }
        private long SubFileSize { get; }

        public ConcatenationFileSystem(IAttributeFileSystem baseFileSystem) : this(baseFileSystem, DefaultSubFileSize) { }

        public ConcatenationFileSystem(IAttributeFileSystem baseFileSystem, long subFileSize)
        {
            BaseFileSystem = baseFileSystem;
            SubFileSize = subFileSize;
        }

        internal bool IsConcatenationFile(string path)
        {
            return HasConcatenationFileAttribute(BaseFileSystem.GetFileAttributes(path));
        }

        internal static bool HasConcatenationFileAttribute(NxFileAttributes attributes)
        {
            return (attributes & NxFileAttributes.Directory) != 0 && (attributes & NxFileAttributes.Archive) != 0;
        }

        private void SetConcatenationFileAttribute(string path)
        {
            NxFileAttributes attributes = BaseFileSystem.GetFileAttributes(path);
            attributes |= NxFileAttributes.Archive;
            BaseFileSystem.SetFileAttributes(path, attributes);
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
            if (IsConcatenationFile(parentDir)) throw new IOException("Cannot create files inside of a concatenation file");

            BaseFileSystem.CreateDirectory(path);
            SetConcatenationFileAttribute(path);

            long remaining = size;

            for (int i = 0; remaining > 0; i++)
            {
                long fileSize = Math.Min(SubFileSize, remaining);
                string fileName = GetSubFilePath(path, i);

                BaseFileSystem.CreateFile(fileName, fileSize, CreateFileOptions.None);

                remaining -= fileSize;
            }
        }

        public void DeleteDirectory(string path)
        {
            path = PathTools.Normalize(path);

            if (IsConcatenationFile(path))
            {
                throw new DirectoryNotFoundException(path);
            }

            BaseFileSystem.DeleteDirectory(path);
        }

        public void DeleteFile(string path)
        {
            path = PathTools.Normalize(path);

            if (!IsConcatenationFile(path))
            {
                BaseFileSystem.DeleteFile(path);
            }

            int count = GetSubFileCount(path);

            for (int i = 0; i < count; i++)
            {
                BaseFileSystem.DeleteFile(GetSubFilePath(path, i));
            }

            BaseFileSystem.DeleteDirectory(path);
        }

        public IDirectory OpenDirectory(string path, OpenDirectoryMode mode)
        {
            path = PathTools.Normalize(path);

            if (IsConcatenationFile(path))
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

            if (!IsConcatenationFile(path))
            {
                return BaseFileSystem.OpenFile(path, mode);
            }

            int fileCount = GetSubFileCount(path);

            var files = new List<IFile>();

            for (int i = 0; i < fileCount; i++)
            {
                string filePath = GetSubFilePath(path, i);
                IFile file = BaseFileSystem.OpenFile(filePath, mode);
                files.Add(file);
            }

            return new ConcatenationFile(BaseFileSystem, path, files, SubFileSize, mode);
        }

        public void RenameDirectory(string srcPath, string dstPath)
        {
            srcPath = PathTools.Normalize(srcPath);
            dstPath = PathTools.Normalize(dstPath);

            if (IsConcatenationFile(srcPath))
            {
                throw new DirectoryNotFoundException();
            }

            BaseFileSystem.RenameDirectory(srcPath, dstPath);
        }

        public void RenameFile(string srcPath, string dstPath)
        {
            srcPath = PathTools.Normalize(srcPath);
            dstPath = PathTools.Normalize(dstPath);

            if (IsConcatenationFile(srcPath))
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

            return BaseFileSystem.DirectoryExists(path) && !IsConcatenationFile(path);
        }

        public bool FileExists(string path)
        {
            path = PathTools.Normalize(path);

            return BaseFileSystem.FileExists(path) || BaseFileSystem.DirectoryExists(path) && IsConcatenationFile(path);
        }

        public DirectoryEntryType GetEntryType(string path)
        {
            path = PathTools.Normalize(path);

            if (IsConcatenationFile(path)) return DirectoryEntryType.File;

            return BaseFileSystem.GetEntryType(path);
        }

        public long GetFreeSpaceSize(string path)
        {
            return BaseFileSystem.GetFreeSpaceSize(path);
        }

        public long GetTotalSpaceSize(string path)
        {
            return BaseFileSystem.GetTotalSpaceSize(path);
        }

        public FileTimeStampRaw GetFileTimeStampRaw(string path)
        {
            return BaseFileSystem.GetFileTimeStampRaw(path);
        }

        public void Commit()
        {
            BaseFileSystem.Commit();
        }

        public void QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, string path, QueryId queryId)
        {
            if(queryId != QueryId.MakeConcatFile) throw new NotSupportedException();

            SetConcatenationFileAttribute(path);
        }

        private int GetSubFileCount(string dirPath)
        {
            int count = 0;

            while (BaseFileSystem.FileExists(GetSubFilePath(dirPath, count)))
            {
                count++;
            }

            return count;
        }

        internal static string GetSubFilePath(string dirPath, int index)
        {
            return $"{dirPath}/{index:D2}";
        }

        internal long GetConcatenationFileSize(string path)
        {
            int fileCount = GetSubFileCount(path);
            long size = 0;

            for (int i = 0; i < fileCount; i++)
            {
                size += BaseFileSystem.GetFileSize(GetSubFilePath(path, i));
            }

            return size;
        }
    }
}
