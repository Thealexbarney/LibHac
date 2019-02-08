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
            CreateFile(path, size, options, new byte[0x20]);
        }

        /// <summary>
        /// Creates a new <see cref="AesXtsFile"/> using the provided key.
        /// </summary>
        /// <param name="path">The full path of the file to create.</param>
        /// <param name="size">The initial size of the created file.</param>
        /// <param name="options">Flags to control how the file is created.
        /// Should usually be <see cref="CreateFileOptions.None"/></param>
        /// <param name="key">The 256-bit key containing a 128-bit data key followed by a 128-bit tweak key.</param>
        public void CreateFile(string path, long size, CreateFileOptions options, byte[] key)
        {
            long containerSize = AesXtsFile.HeaderLength + Util.AlignUp(size, 0x10);
            BaseFileSystem.CreateFile(path, containerSize, options);

            var header = new AesXtsFileHeader(key, size, path, KekSource, ValidationKey);

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
