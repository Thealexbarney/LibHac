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

        public void DeleteDirectoryRecursively(string path)
        {
            path = ToDiscUtilsPath(PathTools.Normalize(path));

            Fs.DeleteDirectory(path, true);
        }

        public void CleanDirectoryRecursively(string path)
        {
            path = ToDiscUtilsPath(PathTools.Normalize(path));

            foreach (string file in Fs.GetFiles(path))
            {
                Fs.DeleteFile(file);
            }

            foreach (string file in Fs.GetDirectories(path))
            {
                Fs.DeleteDirectory(file, true);
            }
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

            if (path == @"\") return true;

            return Fs.DirectoryExists(path);
        }

        public bool FileExists(string path)
        {
            path = ToDiscUtilsPath(PathTools.Normalize(path));

            return Fs.FileExists(path);
        }

        public DirectoryEntryType GetEntryType(string path)
        {
            path = PathTools.Normalize(path);
            string discUtilsPath = ToDiscUtilsPath(path);

            if (Fs.FileExists(discUtilsPath)) return DirectoryEntryType.File;
            if (Fs.DirectoryExists(discUtilsPath)) return DirectoryEntryType.Directory;

            throw new FileNotFoundException(path);
        }

        public NxFileAttributes GetFileAttributes(string path)
        {
            path = ToDiscUtilsPath(PathTools.Normalize(path));

            return Fs.GetAttributes(path).ToNxAttributes();
        }

        public void SetFileAttributes(string path, NxFileAttributes attributes)
        {
            path = ToDiscUtilsPath(PathTools.Normalize(path));

            FileAttributes attributesOld = File.GetAttributes(path);
            FileAttributes attributesNew = attributesOld.ApplyNxAttributes(attributes);

            Fs.SetAttributes(path, attributesNew);
        }

        public long GetFileSize(string path)
        {
            path = ToDiscUtilsPath(PathTools.Normalize(path));

            return Fs.GetFileInfo(path).Length;
        }

        public FileTimeStampRaw GetFileTimeStampRaw(string path)
        {
            path = PathTools.Normalize(path);
            string localPath = ToDiscUtilsPath(path);

            FileTimeStampRaw timeStamp = default;

            timeStamp.Created = new DateTimeOffset(Fs.GetCreationTime(localPath)).ToUnixTimeSeconds();
            timeStamp.Accessed = new DateTimeOffset(Fs.GetLastAccessTime(localPath)).ToUnixTimeSeconds();
            timeStamp.Modified = new DateTimeOffset(Fs.GetLastWriteTime(localPath)).ToUnixTimeSeconds();

            return timeStamp;
        }

        public long GetFreeSpaceSize(string path)
        {
            return Fs.AvailableSpace;
        }

        public long GetTotalSpaceSize(string path)
        {
            return Fs.Size;
        }

        public void Commit() { }

        public void CreateDirectory(string path) => throw new NotSupportedException();
        public void CreateFile(string path, long size, CreateFileOptions options) => throw new NotSupportedException();
        public void RenameDirectory(string srcPath, string dstPath) => throw new NotSupportedException();
        public void RenameFile(string srcPath, string dstPath) => throw new NotSupportedException();
        public void QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, string path, QueryId queryId) => throw new NotSupportedException();

        private static FileAccess GetFileAccess(OpenMode mode)
        {
            // FileAccess and OpenMode have the same flags
            return (FileAccess)(mode & OpenMode.ReadWrite);
        }

        internal static string ToDiscUtilsPath(string path)
        {
            return path.Replace("/", @"\");
        }
    }
}
