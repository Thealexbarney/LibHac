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
                if (fs.DirectoryExists(path))
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
                if (fs.FileExists(path))
                {
                    return fs.OpenFile(path, mode);
                }
            }

            ThrowHelper.ThrowResult(ResultFs.PathNotFound);
            return default;
        }

        public bool DirectoryExists(string path)
        {
            path = PathTools.Normalize(path);

            foreach (IFileSystem fs in Sources)
            {
                if (fs.DirectoryExists(path))
                {
                    return true;
                }
            }

            return false;
        }

        public bool FileExists(string path)
        {
            path = PathTools.Normalize(path);

            foreach (IFileSystem fs in Sources)
            {
                if (fs.FileExists(path))
                {
                    return true;
                }
            }

            return false;
        }

        public DirectoryEntryType GetEntryType(string path)
        {
            path = PathTools.Normalize(path);

            foreach (IFileSystem fs in Sources)
            {
                if (fs.FileExists(path))
                {
                    return DirectoryEntryType.File;
                }

                if (fs.DirectoryExists(path))
                {
                    return DirectoryEntryType.Directory;
                }
            }

            ThrowHelper.ThrowResult(ResultFs.PathNotFound);
            return DirectoryEntryType.NotFound;
        }

        public FileTimeStampRaw GetFileTimeStampRaw(string path)
        {
            path = PathTools.Normalize(path);

            foreach (IFileSystem fs in Sources)
            {
                if (fs.FileExists(path) || fs.DirectoryExists(path))
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
                if (fs.FileExists(path) || fs.DirectoryExists(path))
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
