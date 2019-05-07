using System;

namespace LibHac.IO
{
    public class SubdirectoryFileSystem : IFileSystem
    {
        private string RootPath { get; }
        private IFileSystem ParentFileSystem { get; }

        private string ResolveFullPath(string path)
        {
            //todo
            return RootPath + path;
        }

        public SubdirectoryFileSystem(IFileSystem fs, string rootPath)
        {
            ParentFileSystem = fs;
            RootPath = PathTools.Normalize(rootPath);
        }

        public void CreateDirectory(string path)
        {
            path = PathTools.Normalize(path);

            ParentFileSystem.CreateDirectory(ResolveFullPath(path));
        }

        public void CreateFile(string path, long size, CreateFileOptions options)
        {
            path = PathTools.Normalize(path);

            ParentFileSystem.CreateFile(ResolveFullPath(path), size, options);
        }

        public void DeleteDirectory(string path)
        {
            path = PathTools.Normalize(path);

            ParentFileSystem.DeleteDirectory(ResolveFullPath(path));
        }

        public void DeleteFile(string path)
        {
            path = PathTools.Normalize(path);

            ParentFileSystem.DeleteFile(ResolveFullPath(path));
        }

        public IDirectory OpenDirectory(string path, OpenDirectoryMode mode)
        {
            path = PathTools.Normalize(path);

            IDirectory baseDir = ParentFileSystem.OpenDirectory(ResolveFullPath(path), mode);

            return new SubdirectoryFileSystemDirectory(this, baseDir, path, mode);
        }

        public IFile OpenFile(string path, OpenMode mode)
        {
            path = PathTools.Normalize(path);

            return ParentFileSystem.OpenFile(ResolveFullPath(path), mode);
        }

        public void RenameDirectory(string srcPath, string dstPath)
        {
            srcPath = PathTools.Normalize(srcPath);
            dstPath = PathTools.Normalize(dstPath);

            ParentFileSystem.RenameDirectory(ResolveFullPath(srcPath), ResolveFullPath(dstPath));
        }

        public void RenameFile(string srcPath, string dstPath)
        {
            srcPath = PathTools.Normalize(srcPath);
            dstPath = PathTools.Normalize(dstPath);

            ParentFileSystem.RenameFile(ResolveFullPath(srcPath), ResolveFullPath(dstPath));
        }

        public bool DirectoryExists(string path)
        {
            path = PathTools.Normalize(path);

            return ParentFileSystem.DirectoryExists(ResolveFullPath(path));
        }

        public bool FileExists(string path)
        {
            path = PathTools.Normalize(path);

            return ParentFileSystem.FileExists(ResolveFullPath(path));
        }

        public DirectoryEntryType GetEntryType(string path)
        {
            path = PathTools.Normalize(path);

            return ParentFileSystem.GetEntryType(ResolveFullPath(path));
        }

        public void Commit()
        {
            ParentFileSystem.Commit();
        }

        public FileTimeStampRaw GetFileTimeStampRaw(string path)
        {
            path = PathTools.Normalize(path);

            return ParentFileSystem.GetFileTimeStampRaw(ResolveFullPath(path));
        }

        public void QueryEntry(Span<byte> outBuffer, Span<byte> inBuffer, string path, QueryId queryId)
        {
            path = PathTools.Normalize(path);

            ParentFileSystem.QueryEntry(outBuffer, inBuffer, ResolveFullPath(path), queryId);
        }
    }
}
