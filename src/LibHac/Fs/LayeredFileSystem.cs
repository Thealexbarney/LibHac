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

        public IDirectory OpenDirectory(string path, OpenDirectoryMode mode)
        {
            path = PathTools.Normalize(path);

            var dirs = new List<IDirectory>();

            foreach (IFileSystem fs in Sources)
            {
                DirectoryEntryType type = fs.GetEntryType(path);

                if (type == DirectoryEntryType.File && dirs.Count == 0)
                {
                    ThrowHelper.ThrowResult(ResultFs.PathNotFound);
                }

                if (fs.GetEntryType(path) == DirectoryEntryType.Directory)
                {
                    dirs.Add(fs.OpenDirectory(path, mode));
                }
            }

            var dir = new LayeredFileSystemDirectory(this, dirs, path, mode);

            return dir;
        }

        public IFile OpenFile(string path, OpenMode mode)
        {
            path = PathTools.Normalize(path);

            foreach (IFileSystem fs in Sources)
            {
                DirectoryEntryType type = fs.GetEntryType(path);

                if (type == DirectoryEntryType.File)
                {
                    return fs.OpenFile(path, mode);
                }

                if (type == DirectoryEntryType.Directory)
                {
                    ThrowHelper.ThrowResult(ResultFs.PathNotFound);
                }
            }

            ThrowHelper.ThrowResult(ResultFs.PathNotFound);
            return default;
        }

        public DirectoryEntryType GetEntryType(string path)
        {
            path = PathTools.Normalize(path);

            foreach (IFileSystem fs in Sources)
            {
                DirectoryEntryType type = fs.GetEntryType(path);

                if (type != DirectoryEntryType.NotFound) return type;
            }

            return DirectoryEntryType.NotFound;
        }

        public FileTimeStampRaw GetFileTimeStampRaw(string path)
        {
            path = PathTools.Normalize(path);

            foreach (IFileSystem fs in Sources)
            {
                if (fs.GetEntryType(path) != DirectoryEntryType.NotFound)
                {
                    return fs.GetFileTimeStampRaw(path);
                }
            }

            ThrowHelper.ThrowResult(ResultFs.PathNotFound);
            return default;
        }

        public void QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, string path, QueryId queryId)
        {
            path = PathTools.Normalize(path);

            foreach (IFileSystem fs in Sources)
            {
                if (fs.GetEntryType(path) != DirectoryEntryType.NotFound)
                {
                    fs.QueryEntry(outBuffer, inBuffer, path, queryId);
                    return;
                }
            }

            ThrowHelper.ThrowResult(ResultFs.PathNotFound);
        }

        public void Commit() { }

        public void CreateDirectory(string path) => ThrowHelper.ThrowResult(ResultFs.UnsupportedOperation);
        public void CreateFile(string path, long size, CreateFileOptions options) => ThrowHelper.ThrowResult(ResultFs.UnsupportedOperation);
        public void DeleteDirectory(string path) => ThrowHelper.ThrowResult(ResultFs.UnsupportedOperation);
        public void DeleteDirectoryRecursively(string path) => ThrowHelper.ThrowResult(ResultFs.UnsupportedOperation);
        public void CleanDirectoryRecursively(string path) => ThrowHelper.ThrowResult(ResultFs.UnsupportedOperation);
        public void DeleteFile(string path) => ThrowHelper.ThrowResult(ResultFs.UnsupportedOperation);
        public void RenameDirectory(string srcPath, string dstPath) => ThrowHelper.ThrowResult(ResultFs.UnsupportedOperation);
        public void RenameFile(string srcPath, string dstPath) => ThrowHelper.ThrowResult(ResultFs.UnsupportedOperation);

        public long GetFreeSpaceSize(string path)
        {
            ThrowHelper.ThrowResult(ResultFs.UnsupportedOperation);
            return default;
        }

        public long GetTotalSpaceSize(string path)
        {
            ThrowHelper.ThrowResult(ResultFs.UnsupportedOperation);
            return default;
        }
    }
}
