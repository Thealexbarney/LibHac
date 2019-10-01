using System;
using LibHac.Fs;

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
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.CreateDirectory(fullPath);
        }

        public Result CreateFile(string path, long size, CreateFileOptions options)
        {
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.CreateFile(fullPath, size, options);
        }

        public Result DeleteDirectory(string path)
        {
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.DeleteDirectory(fullPath);
        }

        public Result DeleteDirectoryRecursively(string path)
        {
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.DeleteDirectoryRecursively(fullPath);
        }

        public Result CleanDirectoryRecursively(string path)
        {
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.CleanDirectoryRecursively(fullPath);
        }

        public Result DeleteFile(string path)
        {
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.DeleteFile(fullPath);
        }

        public Result OpenDirectory(out IDirectory directory, string path, OpenDirectoryMode mode)
        {
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.OpenDirectory(out directory, fullPath, mode);
        }

        public Result OpenFile(out IFile file, string path, OpenMode mode)
        {
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.OpenFile(out file, fullPath, mode);
        }

        public Result RenameDirectory(string oldPath, string newPath)
        {
            string fullOldPath = ResolveFullPath(PathTools.Normalize(oldPath));
            string fullNewPath = ResolveFullPath(PathTools.Normalize(newPath));

            return ParentFileSystem.RenameDirectory(fullOldPath, fullNewPath);
        }

        public Result RenameFile(string oldPath, string newPath)
        {
            string fullOldPath = ResolveFullPath(PathTools.Normalize(oldPath));
            string fullNewPath = ResolveFullPath(PathTools.Normalize(newPath));

            return ParentFileSystem.RenameFile(fullOldPath, fullNewPath);
        }

        public Result GetEntryType(out DirectoryEntryType entryType, string path)
        {
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.GetEntryType(out entryType, fullPath);
        }

        public Result Commit()
        {
            return ParentFileSystem.Commit();
        }

        public Result GetFreeSpaceSize(out long freeSpace, string path)
        {
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.GetFreeSpaceSize(out freeSpace, fullPath);
        }

        public Result GetTotalSpaceSize(out long totalSpace, string path)
        {
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.GetTotalSpaceSize(out totalSpace, fullPath);
        }

        public Result GetFileTimeStampRaw(out FileTimeStampRaw timeStamp, string path)
        {
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.GetFileTimeStampRaw(out timeStamp, fullPath);
        }

        public Result QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, string path)
        {
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.QueryEntry(outBuffer, inBuffer, queryId, fullPath);
        }
    }
}
