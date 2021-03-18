using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;

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
        internal bool IsConcatenationFile(U8Span path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Result rc = BaseFileSystem.GetFileAttributes(out NxFileAttributes attributes, path);
                if (rc.IsFailure()) return false;

                return HasConcatenationFileAttribute(attributes);
            }
            else
            {
                return IsConcatenationFileHeuristic(path);
            }
        }

        private bool IsConcatenationFileHeuristic(U8Span path)
        {
            // Check if the path is a directory
            Result getTypeResult = BaseFileSystem.GetEntryType(out DirectoryEntryType pathType, path);
            if (getTypeResult.IsFailure() || pathType != DirectoryEntryType.Directory) return false;

            // Check if the directory contains at least one subfile
            getTypeResult = BaseFileSystem.GetEntryType(out DirectoryEntryType subFileType, PathTools.Combine(path.ToString(), "00").ToU8Span());
            if (getTypeResult.IsFailure() || subFileType != DirectoryEntryType.File) return false;

            // Make sure the directory contains no subdirectories
            Result rc = BaseFileSystem.OpenDirectory(out IDirectory dir, path, OpenDirectoryMode.Directory);
            if (rc.IsFailure()) return false;

            rc = dir.GetEntryCount(out long subDirCount);
            if (rc.IsFailure() || subDirCount > 0) return false;

            // Should be enough checks to avoid most false positives. Maybe
            return true;
        }

        internal static bool HasConcatenationFileAttribute(NxFileAttributes attributes)
        {
            return (attributes & NxFileAttributes.Directory) != 0 && (attributes & NxFileAttributes.Archive) != 0;
        }

        private Result SetConcatenationFileAttribute(U8Span path)
        {
            return BaseFileSystem.SetFileAttributes(path, NxFileAttributes.Archive);
        }

        protected override Result DoCreateDirectory(U8Span path)
        {
            var parent = new U8Span(PathTools.GetParentDirectory(path));

            if (IsConcatenationFile(parent))
            {
                // Cannot create a directory inside of a concatenation file
                return ResultFs.PathNotFound.Log();
            }

            return BaseFileSystem.CreateDirectory(path);
        }

        protected override Result DoCreateFile(U8Span path, long size, CreateFileOptions options)
        {
            CreateFileOptions newOptions = options & ~CreateFileOptions.CreateConcatenationFile;

            if (!options.HasFlag(CreateFileOptions.CreateConcatenationFile))
            {
                return BaseFileSystem.CreateFile(path, size, newOptions);
            }

            // A concatenation file directory can't contain normal files
            ReadOnlySpan<byte> parentDir = PathTools.GetParentDirectory(path);

            if (IsConcatenationFile(new U8Span(parentDir)))
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

                Unsafe.SkipInit(out FsPath fileName);

                rc = GetSubFilePath(fileName.Str, path, i);
                if (rc.IsFailure()) return rc;

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

        protected override Result DoDeleteDirectory(U8Span path)
        {
            if (IsConcatenationFile(path))
            {
                return ResultFs.PathNotFound.Log();
            }

            return BaseFileSystem.DeleteDirectory(path);
        }

        protected override Result DoDeleteDirectoryRecursively(U8Span path)
        {
            if (IsConcatenationFile(path)) return ResultFs.PathNotFound.Log();

            return BaseFileSystem.DeleteDirectoryRecursively(path);
        }

        protected override Result DoCleanDirectoryRecursively(U8Span path)
        {
            if (IsConcatenationFile(path)) return ResultFs.PathNotFound.Log();

            return BaseFileSystem.CleanDirectoryRecursively(path);
        }

        protected override Result DoDeleteFile(U8Span path)
        {
            if (!IsConcatenationFile(path))
            {
                return BaseFileSystem.DeleteFile(path);
            }

            Result rc = GetSubFileCount(out int count, path);
            if (rc.IsFailure()) return rc;

            for (int i = 0; i < count; i++)
            {
                Unsafe.SkipInit(out FsPath subFilePath);

                rc = GetSubFilePath(subFilePath.Str, path, i);
                if (rc.IsFailure()) return rc;

                rc = BaseFileSystem.DeleteFile(subFilePath);
                if (rc.IsFailure()) return rc;
            }

            return BaseFileSystem.DeleteDirectory(path);
        }

        protected override Result DoOpenDirectory(out IDirectory directory, U8Span path, OpenDirectoryMode mode)
        {
            UnsafeHelpers.SkipParamInit(out directory);

            if (IsConcatenationFile(path))
            {
                return ResultFs.PathNotFound.Log();
            }

            Result rc = BaseFileSystem.OpenDirectory(out IDirectory parentDir, path, OpenDirectoryMode.All);
            if (rc.IsFailure()) return rc;

            directory = new ConcatenationDirectory(this, BaseFileSystem, parentDir, mode, path);
            return Result.Success;
        }

        protected override Result DoOpenFile(out IFile file, U8Span path, OpenMode mode)
        {
            UnsafeHelpers.SkipParamInit(out file);

            if (!IsConcatenationFile(path))
            {
                return BaseFileSystem.OpenFile(out file, path, mode);
            }

            Result rc = GetSubFileCount(out int fileCount, path);
            if (rc.IsFailure()) return rc;

            var files = new List<IFile>(fileCount);

            for (int i = 0; i < fileCount; i++)
            {
                Unsafe.SkipInit(out FsPath subFilePath);

                rc = GetSubFilePath(subFilePath.Str, path, i);
                if (rc.IsFailure()) return rc;

                rc = BaseFileSystem.OpenFile(out IFile subFile, subFilePath, mode);
                if (rc.IsFailure()) return rc;

                files.Add(subFile);
            }

            file = new ConcatenationFile(BaseFileSystem, path, files, SubFileSize, mode);
            return Result.Success;
        }

        protected override Result DoRenameDirectory(U8Span oldPath, U8Span newPath)
        {
            if (IsConcatenationFile(oldPath))
            {
                return ResultFs.PathNotFound.Log();
            }

            return BaseFileSystem.RenameDirectory(oldPath, newPath);
        }

        protected override Result DoRenameFile(U8Span oldPath, U8Span newPath)
        {
            if (IsConcatenationFile(oldPath))
            {
                return BaseFileSystem.RenameDirectory(oldPath, newPath);
            }
            else
            {
                return BaseFileSystem.RenameFile(oldPath, newPath);
            }
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, U8Span path)
        {
            if (IsConcatenationFile(path))
            {
                entryType = DirectoryEntryType.File;
                return Result.Success;
            }

            return BaseFileSystem.GetEntryType(out entryType, path);
        }

        protected override Result DoGetFreeSpaceSize(out long freeSpace, U8Span path)
        {
            return BaseFileSystem.GetFreeSpaceSize(out freeSpace, path);
        }

        protected override Result DoGetTotalSpaceSize(out long totalSpace, U8Span path)
        {
            return BaseFileSystem.GetTotalSpaceSize(out totalSpace, path);
        }

        protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, U8Span path)
        {
            return BaseFileSystem.GetFileTimeStampRaw(out timeStamp, path);
        }

        protected override Result DoCommit()
        {
            return BaseFileSystem.Commit();
        }

        protected override Result DoCommitProvisionally(long counter)
        {
            return BaseFileSystem.CommitProvisionally(counter);
        }

        protected override Result DoFlush()
        {
            return BaseFileSystem.Flush();
        }

        protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
            U8Span path)
        {
            if (queryId != QueryId.MakeConcatFile) return ResultFs.UnsupportedQueryEntryForConcatenationFileSystem.Log();

            return SetConcatenationFileAttribute(path);
        }

        private Result GetSubFileCount(out int fileCount, U8Span dirPath)
        {
            UnsafeHelpers.SkipParamInit(out fileCount);

            Unsafe.SkipInit(out FsPath buffer);

            int pathLen = StringUtils.Copy(buffer.Str, dirPath);

            // Make sure we have at least 3 bytes for the sub file name
            if (pathLen + 3 > PathTools.MaxPathLength)
                return ResultFs.TooLongPath.Log();

            buffer.Str[pathLen] = StringTraits.DirectorySeparator;
            Span<byte> subFileName = buffer.Str.Slice(pathLen + 1);

            Result rc;
            int count;

            for (count = 0; ; count++)
            {
                Utf8Formatter.TryFormat(count, subFileName, out _, new StandardFormat('D', 2));

                rc = BaseFileSystem.GetEntryType(out _, buffer);
                if (rc.IsFailure()) break;
            }

            if (!ResultFs.PathNotFound.Includes(rc))
            {
                return rc;
            }

            fileCount = count;
            return Result.Success;
        }

        internal static Result GetSubFilePath(Span<byte> subFilePathBuffer, ReadOnlySpan<byte> basePath, int index)
        {
            int basePathLen = StringUtils.Copy(subFilePathBuffer, basePath);

            // Make sure we have at least 3 bytes for the sub file name
            if (basePathLen + 3 > PathTools.MaxPathLength)
                return ResultFs.TooLongPath.Log();

            subFilePathBuffer[basePathLen] = StringTraits.DirectorySeparator;

            Utf8Formatter.TryFormat(index, subFilePathBuffer.Slice(basePathLen + 1), out _, new StandardFormat('D', 2));

            return Result.Success;
        }

        internal Result GetConcatenationFileSize(out long size, ReadOnlySpan<byte> path)
        {
            UnsafeHelpers.SkipParamInit(out size);
            Unsafe.SkipInit(out FsPath buffer);

            int pathLen = StringUtils.Copy(buffer.Str, path);

            // Make sure we have at least 3 bytes for the sub file name
            if (pathLen + 3 > PathTools.MaxPathLength)
                return ResultFs.TooLongPath.Log();

            buffer.Str[pathLen] = StringTraits.DirectorySeparator;
            Span<byte> subFileName = buffer.Str.Slice(pathLen + 1);

            Result rc;
            long totalSize = 0;

            for (int i = 0; ; i++)
            {
                Utf8Formatter.TryFormat(i, subFileName, out _, new StandardFormat('D', 2));

                rc = BaseFileSystem.GetFileSize(out long fileSize, buffer);
                if (rc.IsFailure()) break;

                totalSize += fileSize;
            }

            if (!ResultFs.PathNotFound.Includes(rc))
            {
                return rc;
            }

            size = totalSize;
            return Result.Success;
        }
    }
}
