﻿using System;

namespace LibHac.Fs
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

        public void DeleteDirectoryRecursively(string path)
        {
            path = PathTools.Normalize(path);

            ParentFileSystem.DeleteDirectoryRecursively(ResolveFullPath(path));
        }

        public void CleanDirectoryRecursively(string path)
        {
            path = PathTools.Normalize(path);

            ParentFileSystem.CleanDirectoryRecursively(ResolveFullPath(path));
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

        public DirectoryEntryType GetEntryType(string path)
        {
            path = PathTools.Normalize(path);

            return ParentFileSystem.GetEntryType(ResolveFullPath(path));
        }

        public void Commit()
        {
            ParentFileSystem.Commit();
        }

        public long GetFreeSpaceSize(string path)
        {
            path = PathTools.Normalize(path);

            return ParentFileSystem.GetFreeSpaceSize(ResolveFullPath(path));
        }

        public long GetTotalSpaceSize(string path)
        {
            path = PathTools.Normalize(path);

            return ParentFileSystem.GetTotalSpaceSize(ResolveFullPath(path));
        }

        public FileTimeStampRaw GetFileTimeStampRaw(string path)
        {
            path = PathTools.Normalize(path);

            return ParentFileSystem.GetFileTimeStampRaw(ResolveFullPath(path));
        }

        public void QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, string path, QueryId queryId)
        {
            path = PathTools.Normalize(path);

            ParentFileSystem.QueryEntry(outBuffer, inBuffer, ResolveFullPath(path), queryId);
        }
    }
}
