using System;
using System.Collections.Generic;
using System.IO;

namespace LibHac.IO
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

            throw new FileNotFoundException();
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

            throw new FileNotFoundException(path);
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

            throw new FileNotFoundException(path);
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

            throw new FileNotFoundException(path);
        }

        public void Commit() { }

        public void CreateDirectory(string path) => throw new NotSupportedException();
        public void CreateFile(string path, long size, CreateFileOptions options) => throw new NotSupportedException();
        public void DeleteDirectory(string path) => throw new NotSupportedException();
        public void DeleteDirectoryRecursively(string path) => throw new NotSupportedException();
        public void CleanDirectoryRecursively(string path) => throw new NotSupportedException();
        public void DeleteFile(string path) => throw new NotSupportedException();
        public void RenameDirectory(string srcPath, string dstPath) => throw new NotSupportedException();
        public void RenameFile(string srcPath, string dstPath) => throw new NotSupportedException();
        public long GetFreeSpaceSize(string path) => throw new NotSupportedException();
        public long GetTotalSpaceSize(string path) => throw new NotSupportedException();
    }
}
