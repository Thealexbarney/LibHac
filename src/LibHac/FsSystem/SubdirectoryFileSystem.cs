using System;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class SubdirectoryFileSystem : FileSystemBase
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

        protected override Result CreateDirectoryImpl(string path)
        {
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.CreateDirectory(fullPath);
        }

        protected override Result CreateFileImpl(string path, long size, CreateFileOptions options)
        {
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.CreateFile(fullPath, size, options);
        }

        protected override Result DeleteDirectoryImpl(string path)
        {
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.DeleteDirectory(fullPath);
        }

        protected override Result DeleteDirectoryRecursivelyImpl(string path)
        {
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.DeleteDirectoryRecursively(fullPath);
        }

        protected override Result CleanDirectoryRecursivelyImpl(string path)
        {
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.CleanDirectoryRecursively(fullPath);
        }

        protected override Result DeleteFileImpl(string path)
        {
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.DeleteFile(fullPath);
        }

        protected override Result OpenDirectoryImpl(out IDirectory directory, string path, OpenDirectoryMode mode)
        {
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.OpenDirectory(out directory, fullPath, mode);
        }

        protected override Result OpenFileImpl(out IFile file, string path, OpenMode mode)
        {
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.OpenFile(out file, fullPath, mode);
        }

        protected override Result RenameDirectoryImpl(string oldPath, string newPath)
        {
            string fullOldPath = ResolveFullPath(PathTools.Normalize(oldPath));
            string fullNewPath = ResolveFullPath(PathTools.Normalize(newPath));

            return ParentFileSystem.RenameDirectory(fullOldPath, fullNewPath);
        }

        protected override Result RenameFileImpl(string oldPath, string newPath)
        {
            string fullOldPath = ResolveFullPath(PathTools.Normalize(oldPath));
            string fullNewPath = ResolveFullPath(PathTools.Normalize(newPath));

            return ParentFileSystem.RenameFile(fullOldPath, fullNewPath);
        }

        protected override Result GetEntryTypeImpl(out DirectoryEntryType entryType, string path)
        {
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.GetEntryType(out entryType, fullPath);
        }

        protected override Result CommitImpl()
        {
            return ParentFileSystem.Commit();
        }

        protected override Result GetFreeSpaceSizeImpl(out long freeSpace, string path)
        {
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.GetFreeSpaceSize(out freeSpace, fullPath);
        }

        protected override Result GetTotalSpaceSizeImpl(out long totalSpace, string path)
        {
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.GetTotalSpaceSize(out totalSpace, fullPath);
        }

        protected override Result GetFileTimeStampRawImpl(out FileTimeStampRaw timeStamp, string path)
        {
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.GetFileTimeStampRaw(out timeStamp, fullPath);
        }

        protected override Result QueryEntryImpl(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, string path)
        {
            string fullPath = ResolveFullPath(PathTools.Normalize(path));

            return ParentFileSystem.QueryEntry(outBuffer, inBuffer, queryId, fullPath);
        }
    }
}
