using System;
using System.Collections.Generic;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class LayeredFileSystem : FileSystemBase
    {
        private List<IFileSystem> Sources { get; } = new List<IFileSystem>();

        public LayeredFileSystem(IList<IFileSystem> sourceFileSystems)
        {
            Sources.AddRange(sourceFileSystems);
        }

        protected override Result OpenDirectoryImpl(out IDirectory directory, string path, OpenDirectoryMode mode)
        {
            directory = default;
            path = PathTools.Normalize(path);

            var dirs = new List<IDirectory>();

            foreach (IFileSystem fs in Sources)
            {
                Result rc = fs.GetEntryType(out DirectoryEntryType entryType, path);
                if (rc.IsFailure()) return rc;

                if (entryType == DirectoryEntryType.File && dirs.Count == 0)
                {
                    ThrowHelper.ThrowResult(ResultFs.PathNotFound.Value);
                }

                if (entryType == DirectoryEntryType.Directory)
                {
                    rc = fs.OpenDirectory(out IDirectory subDirectory, path, mode);
                    if (rc.IsFailure()) return rc;

                    dirs.Add(subDirectory);
                }
            }

            directory = new LayeredFileSystemDirectory(dirs);
            return Result.Success;
        }

        protected override Result OpenFileImpl(out IFile file, string path, OpenMode mode)
        {
            file = default;
            path = PathTools.Normalize(path);

            foreach (IFileSystem fs in Sources)
            {
                Result rc = fs.GetEntryType(out DirectoryEntryType type, path);
                if (rc.IsFailure()) return rc;

                if (type == DirectoryEntryType.File)
                {
                    return fs.OpenFile(out file, path, mode);
                }

                if (type == DirectoryEntryType.Directory)
                {
                    return ResultFs.PathNotFound.Log();
                }
            }

            return ResultFs.PathNotFound.Log();
        }

        protected override Result GetEntryTypeImpl(out DirectoryEntryType entryType, string path)
        {
            path = PathTools.Normalize(path);

            foreach (IFileSystem fs in Sources)
            {
                Result getEntryResult = fs.GetEntryType(out DirectoryEntryType type, path);

                if (getEntryResult.IsSuccess())
                {
                    entryType = type;
                    return Result.Success;
                }
            }

            entryType = default;
            return ResultFs.PathNotFound.Log();
        }

        protected override Result GetFileTimeStampRawImpl(out FileTimeStampRaw timeStamp, string path)
        {
            path = PathTools.Normalize(path);

            foreach (IFileSystem fs in Sources)
            {
                Result getEntryResult = fs.GetEntryType(out DirectoryEntryType type, path);

                if (getEntryResult.IsSuccess())
                {
                    return fs.GetFileTimeStampRaw(out timeStamp, path);
                }
            }

            timeStamp = default;
            return ResultFs.PathNotFound.Log();
        }

        protected override Result QueryEntryImpl(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, string path)
        {
            path = PathTools.Normalize(path);

            foreach (IFileSystem fs in Sources)
            {
                Result getEntryResult = fs.GetEntryType(out DirectoryEntryType type, path);

                if (getEntryResult.IsSuccess())
                {
                    return fs.QueryEntry(outBuffer, inBuffer, queryId, path);
                }
            }

            return ResultFs.PathNotFound.Log();
        }

        protected override Result CommitImpl()
        {
            return Result.Success;
        }

        protected override Result CreateDirectoryImpl(string path) => ResultFs.UnsupportedOperation.Log();
        protected override Result CreateFileImpl(string path, long size, CreateFileOptions options) => ResultFs.UnsupportedOperation.Log();
        protected override Result DeleteDirectoryImpl(string path) => ResultFs.UnsupportedOperation.Log();
        protected override Result DeleteDirectoryRecursivelyImpl(string path) => ResultFs.UnsupportedOperation.Log();
        protected override Result CleanDirectoryRecursivelyImpl(string path) => ResultFs.UnsupportedOperation.Log();
        protected override Result DeleteFileImpl(string path) => ResultFs.UnsupportedOperation.Log();
        protected override Result RenameDirectoryImpl(string oldPath, string newPath) => ResultFs.UnsupportedOperation.Log();
        protected override Result RenameFileImpl(string oldPath, string newPath) => ResultFs.UnsupportedOperation.Log();
    }
}
