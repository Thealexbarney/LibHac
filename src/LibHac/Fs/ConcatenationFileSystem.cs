using System;
using System.Collections.Generic;

#if NETCOREAPP
using System.Runtime.InteropServices;
#endif

namespace LibHac.Fs
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

        // .NET Core on platforms other than Windows doesn't support getting the
        // archive flag in FAT file systems. Try to work around that for now for reading, 
        // but writing still won't work properly on those platforms
        internal bool IsConcatenationFile(string path)
        {
#if NETCOREAPP
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return HasConcatenationFileAttribute(BaseFileSystem.GetFileAttributes(path));
            }
            else
            {
                return IsConcatenationFileHeuristic(path);
            }
#else
            return HasConcatenationFileAttribute(BaseFileSystem.GetFileAttributes(path));
#endif
        }

#if NETCOREAPP
        private bool IsConcatenationFileHeuristic(string path)
        {
            if (BaseFileSystem.GetEntryType(path) != DirectoryEntryType.Directory) return false;

            if (BaseFileSystem.GetEntryType(PathTools.Combine(path, "00")) != DirectoryEntryType.File) return false;

            if (BaseFileSystem.OpenDirectory(path, OpenDirectoryMode.Directories).GetEntryCount() > 0) return false;

            // Should be enough checks to avoid most false positives. Maybe
            return true;
        }
#endif

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
            string parent = PathTools.GetParentDirectory(path);

            if (IsConcatenationFile(parent))
            {
                ThrowHelper.ThrowResult(ResultFs.PathNotFound,
                    "Cannot create a directory inside of a concatenation file");
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

            if (IsConcatenationFile(parentDir))
            {
                ThrowHelper.ThrowResult(ResultFs.PathNotFound,
                    "Cannot create a concatenation file inside of a concatenation file");
            }

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
                ThrowHelper.ThrowResult(ResultFs.PathNotFound);
            }

            BaseFileSystem.DeleteDirectory(path);
        }

        public void DeleteDirectoryRecursively(string path)
        {
            path = PathTools.Normalize(path);

            if (IsConcatenationFile(path)) ThrowHelper.ThrowResult(ResultFs.PathNotFound);

            BaseFileSystem.DeleteDirectoryRecursively(path);
        }

        public void CleanDirectoryRecursively(string path)
        {
            path = PathTools.Normalize(path);

            if (IsConcatenationFile(path)) ThrowHelper.ThrowResult(ResultFs.PathNotFound);

            BaseFileSystem.CleanDirectoryRecursively(path);
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
                ThrowHelper.ThrowResult(ResultFs.PathNotFound);
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
                ThrowHelper.ThrowResult(ResultFs.PathNotFound);
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
            if (queryId != QueryId.MakeConcatFile) ThrowHelper.ThrowResult(ResultFs.UnsupportedOperationInConcatFsQueryEntry);

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
