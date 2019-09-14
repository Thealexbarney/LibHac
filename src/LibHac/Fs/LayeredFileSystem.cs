using System;
using System.Collections.Generic;

namespace LibHac.Fs
{
    public class LayeredFileSystem : IFileSystem
    {
        private List<IFileSystem> Sources { get; } = new List<IFileSystem>();

        public LayeredFileSystem(IList<IFileSystem> sourceFileSystems)
        {
            Sources.AddRange(sourceFileSystems);
        }

        public Result OpenDirectory(out IDirectory directory, string path, OpenDirectoryMode mode)
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
                    ThrowHelper.ThrowResult(ResultFs.PathNotFound);
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

        public Result OpenFile(out IFile file, string path, OpenMode mode)
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

        public Result GetEntryType(out DirectoryEntryType entryType, string path)
        {
            path = PathTools.Normalize(path);

            foreach (IFileSystem fs in Sources)
            {
                Result getEntryResult = fs.GetEntryType(out DirectoryEntryType type, path);

                if (getEntryResult.IsSuccess() && type != DirectoryEntryType.NotFound)
                {
                    entryType = type;
                    return Result.Success;
                }
            }

            entryType = DirectoryEntryType.NotFound;
            return ResultFs.PathNotFound.Log();
        }

        public Result GetFileTimeStampRaw(out FileTimeStampRaw timeStamp, string path)
        {
            path = PathTools.Normalize(path);

            foreach (IFileSystem fs in Sources)
            {
                Result getEntryResult = fs.GetEntryType(out DirectoryEntryType type, path);

                if (getEntryResult.IsSuccess() && type != DirectoryEntryType.NotFound)
                {
                    return fs.GetFileTimeStampRaw(out timeStamp, path);
                }
            }

            timeStamp = default;
            return ResultFs.PathNotFound.Log();
        }

        public Result QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, string path)
        {
            path = PathTools.Normalize(path);

            foreach (IFileSystem fs in Sources)
            {
                Result getEntryResult = fs.GetEntryType(out DirectoryEntryType type, path);

                if (getEntryResult.IsSuccess() && type != DirectoryEntryType.NotFound)
                {
                    return fs.QueryEntry(outBuffer, inBuffer, queryId, path);
                }
            }

            return ResultFs.PathNotFound.Log();
        }

        public Result Commit()
        {
            return Result.Success;
        }

        public Result CreateDirectory(string path) => ResultFs.UnsupportedOperation.Log();
        public Result CreateFile(string path, long size, CreateFileOptions options) => ResultFs.UnsupportedOperation.Log();
        public Result DeleteDirectory(string path) => ResultFs.UnsupportedOperation.Log();
        public Result DeleteDirectoryRecursively(string path) => ResultFs.UnsupportedOperation.Log();
        public Result CleanDirectoryRecursively(string path) => ResultFs.UnsupportedOperation.Log();
        public Result DeleteFile(string path) => ResultFs.UnsupportedOperation.Log();
        public Result RenameDirectory(string oldPath, string newPath) => ResultFs.UnsupportedOperation.Log();
        public Result RenameFile(string oldPath, string newPath) => ResultFs.UnsupportedOperation.Log();

        public Result GetFreeSpaceSize(out long freeSpace, string path)
        {
            freeSpace = default;
            return ResultFs.UnsupportedOperation.Log();
        }

        public Result GetTotalSpaceSize(out long totalSpace, string path)
        {
            totalSpace = default;
            return ResultFs.UnsupportedOperation.Log();
        }
    }
}
