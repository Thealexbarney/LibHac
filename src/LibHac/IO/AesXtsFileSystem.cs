using System;

namespace LibHac.IO
{
    public class AesXtsFileSystem : IFileSystem
    {
        public int BlockSize { get; }

        private IFileSystem BaseFileSystem { get; }
        private byte[] KekSource { get; }
        private byte[] ValidationKey { get; }

        public AesXtsFileSystem(IFileSystem fs, byte[] kekSource, byte[] validationKey, int blockSize)
        {
            BaseFileSystem = fs;
            KekSource = kekSource;
            ValidationKey = validationKey;
            BlockSize = blockSize;
        }

        public AesXtsFileSystem(IFileSystem fs, byte[] keys, int blockSize)
        {
            BaseFileSystem = fs;
            KekSource = keys.AsSpan(0, 0x10).ToArray();
            ValidationKey = keys.AsSpan(0x10, 0x10).ToArray();
            BlockSize = blockSize;
        }

        public void CreateDirectory(string path)
        {
            BaseFileSystem.CreateDirectory(path);
        }

        public void CreateFile(string path, long size, CreateFileOptions options)
        {
            throw new NotImplementedException();
        }

        public void DeleteDirectory(string path)
        {
            BaseFileSystem.DeleteDirectory(path);
        }

        public void DeleteFile(string path)
        {
            BaseFileSystem.DeleteFile(path);
        }

        public IDirectory OpenDirectory(string path, OpenDirectoryMode mode)
        {
            path = PathTools.Normalize(path);

            IDirectory baseDir = BaseFileSystem.OpenDirectory(path, mode);

            var dir = new AesXtsDirectory(this, baseDir, mode);
            return dir;
        }

        public IFile OpenFile(string path, OpenMode mode)
        {
            path = PathTools.Normalize(path);

            IFile baseFile = BaseFileSystem.OpenFile(path, mode);
            var file = new AesXtsFile(mode, baseFile, path, KekSource, ValidationKey, BlockSize);
            return file;
        }

        public void RenameDirectory(string srcPath, string dstPath)
        {
            throw new NotImplementedException();
        }

        public void RenameFile(string srcPath, string dstPath)
        {
            throw new NotImplementedException();
        }

        public bool DirectoryExists(string path)
        {
            return BaseFileSystem.DirectoryExists(path);
        }

        public bool FileExists(string path)
        {
            return BaseFileSystem.FileExists(path);
        }

        public DirectoryEntryType GetEntryType(string path)
        {
            return BaseFileSystem.GetEntryType(path);
        }

        public void Commit()
        {
            BaseFileSystem.Commit();
        }
    }
}
