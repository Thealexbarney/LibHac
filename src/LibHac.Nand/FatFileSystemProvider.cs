using System;
using System.IO;
using DiscUtils.Fat;
using LibHac.IO;

namespace LibHac.Nand
{
    public class FatFileSystemProvider : IAttributeFileSystem
    {
        public FatFileSystem Fs { get; }

        public FatFileSystemProvider(FatFileSystem fileSystem)
        {
            Fs = fileSystem;
        }

        public void DeleteDirectory(string path)
        {
            path = ToDiscUtilsPath(PathTools.Normalize(path));
            Fs.DeleteDirectory(path);
        }

        public void DeleteFile(string path)
        {
            path = ToDiscUtilsPath(PathTools.Normalize(path));
            Fs.DeleteFile(path);
        }

        public IDirectory OpenDirectory(string path, OpenDirectoryMode mode)
        {
            path = PathTools.Normalize(path);

            return new FatFileSystemDirectory(this, path, mode);
        }

        public IFile OpenFile(string path, OpenMode mode)
        {
            path = ToDiscUtilsPath(PathTools.Normalize(path));

            Stream stream = Fs.OpenFile(path, FileMode.Open, GetFileAccess(mode));
            return stream.AsIFile(mode);
        }

        public bool DirectoryExists(string path)
        {
            path = ToDiscUtilsPath(PathTools.Normalize(path));

            if (path == @"\\") return true;

            return Fs.DirectoryExists(path);
        }

        public bool FileExists(string path)
        {
            path = ToDiscUtilsPath(PathTools.Normalize(path));

            return Fs.FileExists(path);
        }

        public FileAttributes GetFileAttributes(string path)
        {
            path = ToDiscUtilsPath(PathTools.Normalize(path));

            return Fs.GetAttributes(path);
        }

        public long GetFileSize(string path)
        {
            path = ToDiscUtilsPath(PathTools.Normalize(path));

            return Fs.GetFileInfo(path).Length;
        }

        public void Commit() { }
        
        public void CreateDirectory(string path) => throw new NotSupportedException();
        public void CreateFile(string path, long size) => throw new NotSupportedException();
        public void RenameDirectory(string srcPath, string dstPath) => throw new NotSupportedException();
        public void RenameFile(string srcPath, string dstPath) => throw new NotSupportedException();

        private static FileAccess GetFileAccess(OpenMode mode)
        {
            // FileAccess and OpenMode have the same flags
            return (FileAccess)(mode & OpenMode.ReadWrite);
        }

        internal static string ToDiscUtilsPath(string path)
        {
            return path.Replace("/", @"\\");
        }
    }
}
