using System;
using System.Diagnostics;

namespace LibHac.Fs
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

        public Result CreateDirectory(string path)
        {
            return BaseFileSystem.CreateDirectory(path);
        }

        public Result CreateFile(string path, long size, CreateFileOptions options)
        {
            return CreateFile(path, size, options, new byte[0x20]);
        }

        /// <summary>
        /// Creates a new <see cref="AesXtsFile"/> using the provided key.
        /// </summary>
        /// <param name="path">The full path of the file to create.</param>
        /// <param name="size">The initial size of the created file.</param>
        /// <param name="options">Flags to control how the file is created.
        /// Should usually be <see cref="CreateFileOptions.None"/></param>
        /// <param name="key">The 256-bit key containing a 128-bit data key followed by a 128-bit tweak key.</param>
        public Result CreateFile(string path, long size, CreateFileOptions options, byte[] key)
        {
            long containerSize = AesXtsFile.HeaderLength + Util.AlignUp(size, 0x10);
            BaseFileSystem.CreateFile(path, containerSize, options);

            var header = new AesXtsFileHeader(key, size, path, KekSource, ValidationKey);

            Result rc = BaseFileSystem.OpenFile(out IFile baseFile, path, OpenMode.Write);
            if (rc.IsFailure()) return rc;

            using (baseFile)
            {
                rc = baseFile.Write(0, header.ToBytes(false));
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
        }

        public Result DeleteDirectory(string path)
        {
            return BaseFileSystem.DeleteDirectory(path);
        }

        public Result DeleteDirectoryRecursively(string path)
        {
            return BaseFileSystem.DeleteDirectoryRecursively(path);
        }

        public Result CleanDirectoryRecursively(string path)
        {
            return BaseFileSystem.CleanDirectoryRecursively(path);
        }

        public Result DeleteFile(string path)
        {
            return BaseFileSystem.DeleteFile(path);
        }

        public Result OpenDirectory(out IDirectory directory, string path, OpenDirectoryMode mode)
        {
            directory = default;
            path = PathTools.Normalize(path);

            Result rc = BaseFileSystem.OpenDirectory(out IDirectory baseDir, path, mode);
            if (rc.IsFailure()) return rc;

            directory = new AesXtsDirectory(BaseFileSystem, baseDir, path, mode);
            return Result.Success;
        }

        public Result OpenFile(out IFile file, string path, OpenMode mode)
        {
            file = default;
            path = PathTools.Normalize(path);

            Result rc = BaseFileSystem.OpenFile(out IFile baseFile, path, mode | OpenMode.Read);
            if (rc.IsFailure()) return rc;

            var xtsFile = new AesXtsFile(mode, baseFile, path, KekSource, ValidationKey, BlockSize);

            xtsFile.ToDispose.Add(baseFile);

            file = xtsFile;
            return Result.Success;
        }

        public Result RenameDirectory(string oldPath, string newPath)
        {
            oldPath = PathTools.Normalize(oldPath);
            newPath = PathTools.Normalize(newPath);

            // todo: Return proper result codes

            // Official code procedure:
            // Make sure all file headers can be decrypted
            // Rename directory to the new path
            // Reencrypt file headers with new path
            // If no errors, return
            // Reencrypt any modified file headers with the old path
            // Rename directory to the old path

            Result rc = BaseFileSystem.RenameDirectory(oldPath, newPath);
            if (rc.IsFailure()) return rc;

            try
            {
                RenameDirectoryImpl(oldPath, newPath, false);
            }
            catch (Exception)
            {
                RenameDirectoryImpl(oldPath, newPath, true);
                BaseFileSystem.RenameDirectory(oldPath, newPath);

                throw;
            }

            return Result.Success;
        }

        private void RenameDirectoryImpl(string srcDir, string dstDir, bool doRollback)
        {
            foreach (DirectoryEntryEx entry in this.EnumerateEntries(srcDir, "*"))
            {
                string subSrcPath = $"{srcDir}/{entry.Name}";
                string subDstPath = $"{dstDir}/{entry.Name}";

                if (entry.Type == DirectoryEntryType.Directory)
                {
                    RenameDirectoryImpl(subSrcPath, subDstPath, doRollback);
                }

                if (entry.Type == DirectoryEntryType.File)
                {
                    if (doRollback)
                    {
                        if (TryReadXtsHeader(subDstPath, subDstPath, out AesXtsFileHeader header))
                        {
                            WriteXtsHeader(header, subDstPath, subSrcPath);
                        }
                    }
                    else
                    {
                        AesXtsFileHeader header = ReadXtsHeader(subDstPath, subSrcPath);
                        WriteXtsHeader(header, subDstPath, subDstPath);
                    }
                }
            }
        }

        public Result RenameFile(string oldPath, string newPath)
        {
            oldPath = PathTools.Normalize(oldPath);
            newPath = PathTools.Normalize(newPath);

            // todo: Return proper result codes

            AesXtsFileHeader header = ReadXtsHeader(oldPath, oldPath);

            BaseFileSystem.RenameFile(oldPath, newPath);

            try
            {
                WriteXtsHeader(header, newPath, newPath);
            }
            catch (Exception)
            {
                BaseFileSystem.RenameFile(newPath, oldPath);
                WriteXtsHeader(header, oldPath, oldPath);

                throw;
            }

            return Result.Success;
        }

        public Result GetEntryType(out DirectoryEntryType entryType, string path)
        {
            return BaseFileSystem.GetEntryType(out entryType, path);
        }

        public Result GetFileTimeStampRaw(out FileTimeStampRaw timeStamp, string path)
        {
            return BaseFileSystem.GetFileTimeStampRaw(out timeStamp, path);
        }

        public Result GetFreeSpaceSize(out long freeSpace, string path)
        {
            return BaseFileSystem.GetFreeSpaceSize(out freeSpace, path);
        }

        public Result GetTotalSpaceSize(out long totalSpace, string path)
        {
            return BaseFileSystem.GetTotalSpaceSize(out totalSpace, path);
        }

        public Result Commit()
        {
            return BaseFileSystem.Commit();
        }

        public Result QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, string path)
        {
            return BaseFileSystem.QueryEntry(outBuffer, inBuffer, queryId, path);
        }

        private AesXtsFileHeader ReadXtsHeader(string filePath, string keyPath)
        {
            if (!TryReadXtsHeader(filePath, keyPath, out AesXtsFileHeader header))
            {
                ThrowHelper.ThrowResult(ResultFs.AesXtsFileHeaderInvalidKeysInRenameFile, "Could not decrypt AES-XTS keys");
            }

            return header;
        }

        private bool TryReadXtsHeader(string filePath, string keyPath, out AesXtsFileHeader header)
        {
            Debug.Assert(PathTools.IsNormalized(filePath.AsSpan()));
            Debug.Assert(PathTools.IsNormalized(keyPath.AsSpan()));

            header = null;

            Result rc = BaseFileSystem.OpenFile(out IFile file, filePath, OpenMode.Read);
            if (rc.IsFailure()) return false;

            using (file)
            {
                header = new AesXtsFileHeader(file);

                return header.TryDecryptHeader(keyPath, KekSource, ValidationKey);
            }
        }

        private void WriteXtsHeader(AesXtsFileHeader header, string filePath, string keyPath)
        {
            Debug.Assert(PathTools.IsNormalized(filePath.AsSpan()));
            Debug.Assert(PathTools.IsNormalized(keyPath.AsSpan()));

            header.EncryptHeader(keyPath, KekSource, ValidationKey);

            BaseFileSystem.OpenFile(out IFile file, filePath, OpenMode.ReadWrite);

            using (file)
            {
                file.Write(0, header.ToBytes(false), WriteOption.Flush).ThrowIfFailure();
            }
        }
    }
}
