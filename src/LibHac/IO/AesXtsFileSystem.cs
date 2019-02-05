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
            long containerSize = AesXtsFile.HeaderLength + Util.AlignUp(size, 0x16);
            BaseFileSystem.CreateFile(path, containerSize, options);

            var header = new AesXtsFileHeader(new byte[0x10], new byte[0x10], size, path, KekSource, ValidationKey);

            using (IFile baseFile = BaseFileSystem.OpenFile(path, OpenMode.Write))
            {
                baseFile.Write(header.ToBytes(false), 0);
            }
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

            IFile baseFile = BaseFileSystem.OpenFile(path, mode | OpenMode.Read);
            var file = new AesXtsFile(mode, baseFile, path, KekSource, ValidationKey, BlockSize);

            file.ToDispose.Add(baseFile);
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
