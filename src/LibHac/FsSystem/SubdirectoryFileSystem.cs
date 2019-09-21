using System;

namespace LibHac.FsSystem
{
    public class SubdirectoryFileSystem : IFileSystem
    {
        private string RootPath { get; }
        private IFileSystem ParentFileSystem { get; }

        private string ResolveFullPath(string path)
        {
            return PathTools.Combine(RootPath, path);
        }

        public SubdirectoryFileSystem(IFileSystem fs, string rootPath)
        {
            ParentFileSystem = fs;
            RootPath = PathTools.Normalize(rootPath);
        }

        public Result CreateDirectory(string path)
        {
            path = PathTools.Normalize(path);

            return ParentFileSystem.CreateDirectory(ResolveFullPath(path));
        }

        public Result CreateFile(string path, long size, CreateFileOptions options)
        {
            path = PathTools.Normalize(path);

            return ParentFileSystem.CreateFile(ResolveFullPath(path), size, options);
        }

        public Result DeleteDirectory(string path)
        {
            path = PathTools.Normalize(path);

            return ParentFileSystem.DeleteDirectory(ResolveFullPath(path));
        }

        public Result DeleteDirectoryRecursively(string path)
        {
            path = PathTools.Normalize(path);

            return ParentFileSystem.DeleteDirectoryRecursively(ResolveFullPath(path));
        }

        public Result CleanDirectoryRecursively(string path)
        {
            path = PathTools.Normalize(path);

            return ParentFileSystem.CleanDirectoryRecursively(ResolveFullPath(path));
        }

        public Result DeleteFile(string path)
        {
            path = PathTools.Normalize(path);

            return ParentFileSystem.DeleteFile(ResolveFullPath(path));
        }

        public Result OpenDirectory(out IDirectory directory, string path, OpenDirectoryMode mode)
        {
            path = PathTools.Normalize(path);

            return ParentFileSystem.OpenDirectory(out directory, ResolveFullPath(path), mode);
        }

        public Result OpenFile(out IFile file, string path, OpenMode mode)
        {
            path = PathTools.Normalize(path);

            return ParentFileSystem.OpenFile(out file, ResolveFullPath(path), mode);
        }

        public Result RenameDirectory(string oldPath, string newPath)
        {
            oldPath = PathTools.Normalize(oldPath);
            newPath = PathTools.Normalize(newPath);

            return ParentFileSystem.RenameDirectory(oldPath, newPath);
        }

        public Result RenameFile(string oldPath, string newPath)
        {
            oldPath = PathTools.Normalize(oldPath);
            newPath = PathTools.Normalize(newPath);

            return ParentFileSystem.RenameFile(oldPath, newPath);
        }

        public Result GetEntryType(out DirectoryEntryType entryType, string path)
        {
            path = PathTools.Normalize(path);

            return ParentFileSystem.GetEntryType(out entryType, ResolveFullPath(path));
        }

        public Result Commit()
        {
            return ParentFileSystem.Commit();
        }

        public Result GetFreeSpaceSize(out long freeSpace, string path)
        {
            path = PathTools.Normalize(path);

            return ParentFileSystem.GetFreeSpaceSize(out freeSpace, ResolveFullPath(path));
        }

        public Result GetTotalSpaceSize(out long totalSpace, string path)
        {
            path = PathTools.Normalize(path);

            return ParentFileSystem.GetTotalSpaceSize(out totalSpace, ResolveFullPath(path));
        }

        public Result GetFileTimeStampRaw(out FileTimeStampRaw timeStamp, string path)
        {
            path = PathTools.Normalize(path);

            return ParentFileSystem.GetFileTimeStampRaw(out timeStamp, ResolveFullPath(path));
        }

        public Result QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, string path)
        {
            path = PathTools.Normalize(path);

            return ParentFileSystem.QueryEntry(outBuffer, inBuffer, queryId, ResolveFullPath(path));
        }
    }
}
