using System;
using System.Collections.Generic;
using LibHac.Fs;
#if CROSS_PLATFORM
using System.Runtime.InteropServices;
#endif

namespace LibHac.FsSystem
{
    /// <summary>
    /// An <see cref="IFileSystem"/> that stores large files as smaller, separate sub-files.
    /// </summary>
    /// <remarks>
    /// This filesystem is mainly used to allow storing large files on filesystems that have low
    /// limits on file size such as FAT filesystems. The underlying base filesystem must have
    /// support for the "Archive" file attribute found in FAT or NTFS filesystems.
    ///
    /// A <see cref="ConcatenationFileSystem"/> may contain both standard files or Concatenation files.
    /// If a directory has the archive attribute set, its contents will be concatenated and treated
    /// as a single file. These sub-files must follow the naming scheme "00", "01", "02", ...
    /// Each sub-file except the final one must have the size <see cref="SubFileSize"/> that was specified
    /// at the creation of the <see cref="ConcatenationFileSystem"/>.
    /// </remarks>
    public class ConcatenationFileSystem : IFileSystem
    {
        private const long DefaultSubFileSize = 0xFFFF0000; // Hard-coded value used by FS
        private IAttributeFileSystem BaseFileSystem { get; }
        private long SubFileSize { get; }

        /// <summary>
        /// Initializes a new <see cref="ConcatenationFileSystem"/>.
        /// </summary>
        /// <param name="baseFileSystem">The base <see cref="IAttributeFileSystem"/> for the
        /// new <see cref="ConcatenationFileSystem"/>.</param>
        public ConcatenationFileSystem(IAttributeFileSystem baseFileSystem) : this(baseFileSystem, DefaultSubFileSize) { }

        /// <summary>
        /// Initializes a new <see cref="ConcatenationFileSystem"/>.
        /// </summary>
        /// <param name="baseFileSystem">The base <see cref="IAttributeFileSystem"/> for the
        /// new <see cref="ConcatenationFileSystem"/>.</param>
        /// <param name="subFileSize">The size of each sub-file. Once a file exceeds this size, a new sub-file will be created</param>
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
#if CROSS_PLATFORM
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Result rc = BaseFileSystem.GetFileAttributes(path, out NxFileAttributes attributes);
                if (rc.IsFailure()) return false;

                return HasConcatenationFileAttribute(attributes);
            }
            else
            {
                return IsConcatenationFileHeuristic(path);
            }
#else
            Result rc = BaseFileSystem.GetFileAttributes(path, out NxFileAttributes attributes);
            if (rc.IsFailure()) return false;

            return HasConcatenationFileAttribute(attributes);
#endif
        }

#if CROSS_PLATFORM
        private bool IsConcatenationFileHeuristic(string path)
        {
            // Check if the path is a directory
            Result getTypeResult = BaseFileSystem.GetEntryType(out DirectoryEntryType pathType, path);
            if (getTypeResult.IsFailure() || pathType != DirectoryEntryType.Directory) return false;

            // Check if the directory contains at least one subfile
            getTypeResult = BaseFileSystem.GetEntryType(out DirectoryEntryType subFileType, PathTools.Combine(path, "00"));
            if (getTypeResult.IsFailure() || subFileType != DirectoryEntryType.File) return false;

            // Make sure the directory contains no subdirectories
            Result rc = BaseFileSystem.OpenDirectory(out IDirectory dir, path, OpenDirectoryMode.Directory);
            if (rc.IsFailure()) return false;

            rc = dir.GetEntryCount(out long subDirCount);
            if (rc.IsFailure() || subDirCount > 0) return false;

            // Should be enough checks to avoid most false positives. Maybe
            return true;
        }
#endif

        internal static bool HasConcatenationFileAttribute(NxFileAttributes attributes)
        {
            return (attributes & NxFileAttributes.Directory) != 0 && (attributes & NxFileAttributes.Archive) != 0;
        }

        private Result SetConcatenationFileAttribute(string path)
        {
            return BaseFileSystem.SetFileAttributes(path, NxFileAttributes.Archive);
        }

        public Result CreateDirectory(string path)
        {
            path = PathTools.Normalize(path);
            string parent = PathTools.GetParentDirectory(path);

            if (IsConcatenationFile(parent))
            {
                // Cannot create a directory inside of a concatenation file
                return ResultFs.PathNotFound.Log();
            }

            return BaseFileSystem.CreateDirectory(path);
        }

        public Result CreateFile(string path, long size, CreateFileOptions options)
        {
            path = PathTools.Normalize(path);

            CreateFileOptions newOptions = options & ~CreateFileOptions.CreateConcatenationFile;

            if (!options.HasFlag(CreateFileOptions.CreateConcatenationFile))
            {
                return BaseFileSystem.CreateFile(path, size, newOptions);
            }

            // A concatenation file directory can't contain normal files
            string parentDir = PathTools.GetParentDirectory(path);

            if (IsConcatenationFile(parentDir))
            {
                // Cannot create a file inside of a concatenation file
                return ResultFs.PathNotFound.Log();
            }

            Result rc = BaseFileSystem.CreateDirectory(path, NxFileAttributes.Archive);
            if (rc.IsFailure()) return rc;

            long remaining = size;

            for (int i = 0; remaining > 0; i++)
            {
                long fileSize = Math.Min(SubFileSize, remaining);
                string fileName = GetSubFilePath(path, i);

                Result createSubFileResult = BaseFileSystem.CreateFile(fileName, fileSize, CreateFileOptions.None);

                if (createSubFileResult.IsFailure())
                {
                    BaseFileSystem.DeleteDirectoryRecursively(path);
                    return createSubFileResult;
                }

                remaining -= fileSize;
            }

            return Result.Success;
        }

        public Result DeleteDirectory(string path)
        {
            path = PathTools.Normalize(path);

            if (IsConcatenationFile(path))
            {
                return ResultFs.PathNotFound.Log();
            }

            return BaseFileSystem.DeleteDirectory(path);
        }

        public Result DeleteDirectoryRecursively(string path)
        {
            path = PathTools.Normalize(path);

            if (IsConcatenationFile(path)) return ResultFs.PathNotFound.Log();

            return BaseFileSystem.DeleteDirectoryRecursively(path);
        }

        public Result CleanDirectoryRecursively(string path)
        {
            path = PathTools.Normalize(path);

            if (IsConcatenationFile(path)) return ResultFs.PathNotFound.Log();

            return BaseFileSystem.CleanDirectoryRecursively(path);
        }

        public Result DeleteFile(string path)
        {
            path = PathTools.Normalize(path);

            if (!IsConcatenationFile(path))
            {
                return BaseFileSystem.DeleteFile(path);
            }

            int count = GetSubFileCount(path);

            for (int i = 0; i < count; i++)
            {
                Result rc = BaseFileSystem.DeleteFile(GetSubFilePath(path, i));
                if (rc.IsFailure()) return rc;
            }

            return BaseFileSystem.DeleteDirectory(path);
        }

        public Result OpenDirectory(out IDirectory directory, string path, OpenDirectoryMode mode)
        {
            directory = default;
            path = PathTools.Normalize(path);

            if (IsConcatenationFile(path))
            {
                return ResultFs.PathNotFound.Log();
            }

            Result rc = BaseFileSystem.OpenDirectory(out IDirectory parentDir, path, OpenDirectoryMode.All);
            if (rc.IsFailure()) return rc;

            directory = new ConcatenationDirectory(this, BaseFileSystem, parentDir, mode, path);
            return Result.Success;
        }

        public Result OpenFile(out IFile file, string path, OpenMode mode)
        {
            file = default;
            path = PathTools.Normalize(path);

            if (!IsConcatenationFile(path))
            {
                return BaseFileSystem.OpenFile(out file, path, mode);
            }

            int fileCount = GetSubFileCount(path);

            var files = new List<IFile>();

            for (int i = 0; i < fileCount; i++)
            {
                string filePath = GetSubFilePath(path, i);

                Result rc = BaseFileSystem.OpenFile(out IFile subFile, filePath, mode);
                if (rc.IsFailure()) return rc;

                files.Add(subFile);
            }

            file = new ConcatenationFile(BaseFileSystem, path, files, SubFileSize, mode);
            return Result.Success;
        }

        public Result RenameDirectory(string oldPath, string newPath)
        {
            oldPath = PathTools.Normalize(oldPath);
            newPath = PathTools.Normalize(newPath);

            if (IsConcatenationFile(oldPath))
            {
                return ResultFs.PathNotFound.Log();
            }

            return BaseFileSystem.RenameDirectory(oldPath, newPath);
        }

        public Result RenameFile(string oldPath, string newPath)
        {
            oldPath = PathTools.Normalize(oldPath);
            newPath = PathTools.Normalize(newPath);

            if (IsConcatenationFile(oldPath))
            {
                return BaseFileSystem.RenameDirectory(oldPath, newPath);
            }
            else
            {
                return BaseFileSystem.RenameFile(oldPath, newPath);
            }
        }

        public Result GetEntryType(out DirectoryEntryType entryType, string path)
        {
            path = PathTools.Normalize(path);

            if (IsConcatenationFile(path))
            {
                entryType = DirectoryEntryType.File;
                return Result.Success;
            }

            return BaseFileSystem.GetEntryType(out entryType, path);
        }

        public Result GetFreeSpaceSize(out long freeSpace, string path)
        {
            return BaseFileSystem.GetFreeSpaceSize(out freeSpace, path);
        }

        public Result GetTotalSpaceSize(out long totalSpace, string path)
        {
            return BaseFileSystem.GetTotalSpaceSize(out totalSpace, path);
        }

        public Result GetFileTimeStampRaw(out FileTimeStampRaw timeStamp, string path)
        {
            return BaseFileSystem.GetFileTimeStampRaw(out timeStamp, path);
        }

        public Result Commit()
        {
            return BaseFileSystem.Commit();
        }

        public Result QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, string path)
        {
            if (queryId != QueryId.MakeConcatFile) return ResultFs.UnsupportedOperationInConcatFsQueryEntry.Log();

            return SetConcatenationFileAttribute(path);
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
