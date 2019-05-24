using System;

namespace LibHac.Fs
{
    public class ReadOnlyFileSystem : IFileSystem
    {
        private IFileSystem BaseFs { get; }

        public ReadOnlyFileSystem(IFileSystem baseFileSystem)
        {
            BaseFs = baseFileSystem;
        }

        public IDirectory OpenDirectory(string path, OpenDirectoryMode mode)
        {
            IDirectory baseDir = BaseFs.OpenDirectory(path, mode);
            return new ReadOnlyDirectory(this, baseDir);
        }

        public IFile OpenFile(string path, OpenMode mode)
        {
            IFile baseFile = BaseFs.OpenFile(path, mode);
            return new ReadOnlyFile(baseFile);
        }

        public bool DirectoryExists(string path)
        {
            return BaseFs.DirectoryExists(path);
        }

        public bool FileExists(string path)
        {
            return BaseFs.FileExists(path);
        }

        public DirectoryEntryType GetEntryType(string path)
        {
            return BaseFs.GetEntryType(path);
        }

        public long GetFreeSpaceSize(string path)
        {
            return 0;
        }

        public long GetTotalSpaceSize(string path)
        {
            return BaseFs.GetTotalSpaceSize(path);
        }

        public FileTimeStampRaw GetFileTimeStampRaw(string path)
        {
            return BaseFs.GetFileTimeStampRaw(path);
        }

        public void Commit()
        {

        }

        public void QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, string path, QueryId queryId)
        {
            BaseFs.QueryEntry(outBuffer, inBuffer, path, queryId);
        }

        public void CreateDirectory(string path) => throw new NotSupportedException();
        public void CreateFile(string path, long size, CreateFileOptions options) => throw new NotSupportedException();
        public void DeleteDirectory(string path) => throw new NotSupportedException();
        public void DeleteDirectoryRecursively(string path) => throw new NotSupportedException();
        public void CleanDirectoryRecursively(string path) => throw new NotSupportedException();
        public void DeleteFile(string path) => throw new NotSupportedException();
        public void RenameDirectory(string srcPath, string dstPath) => throw new NotSupportedException();
        public void RenameFile(string srcPath, string dstPath) => throw new NotSupportedException();
    }
}
