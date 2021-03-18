using System;
using System.Diagnostics;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;

namespace LibHac.FsSystem
{
    public class AesXtsFileSystem : IFileSystem
    {
        public int BlockSize { get; }

        private IFileSystem BaseFileSystem { get; }
        private ReferenceCountedDisposable<IFileSystem> SharedBaseFileSystem { get; }
        private byte[] KekSource { get; }
        private byte[] ValidationKey { get; }

        public AesXtsFileSystem(ReferenceCountedDisposable<IFileSystem> fs, byte[] keys, int blockSize)
        {
            SharedBaseFileSystem = fs.AddReference();
            BaseFileSystem = SharedBaseFileSystem.Target;
            KekSource = keys.AsSpan(0, 0x10).ToArray();
            ValidationKey = keys.AsSpan(0x10, 0x10).ToArray();
            BlockSize = blockSize;
        }

        public AesXtsFileSystem(IFileSystem fs, byte[] keys, int blockSize)
        {
            BaseFileSystem = fs;
            KekSource = keys.AsSpan(0, 0x10).ToArray();
            ValidationKey = keys.AsSpan(0x10, 0x10).ToArray();
            BlockSize = blockSize;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SharedBaseFileSystem?.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override Result DoCreateDirectory(U8Span path)
        {
            return BaseFileSystem.CreateDirectory(path);
        }

        protected override Result DoCreateFile(U8Span path, long size, CreateFileOptions options)
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
        public Result CreateFile(U8Span path, long size, CreateFileOptions options, byte[] key)
        {
            long containerSize = AesXtsFile.HeaderLength + Alignment.AlignUp(size, 0x10);

            Result rc = BaseFileSystem.CreateFile(path, containerSize, options);
            if (rc.IsFailure()) return rc;

            var header = new AesXtsFileHeader(key, size, path.ToString(), KekSource, ValidationKey);

            rc = BaseFileSystem.OpenFile(out IFile baseFile, path, OpenMode.Write);
            if (rc.IsFailure()) return rc;

            using (baseFile)
            {
                rc = baseFile.Write(0, header.ToBytes(false));
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
        }

        protected override Result DoDeleteDirectory(U8Span path)
        {
            return BaseFileSystem.DeleteDirectory(path);
        }

        protected override Result DoDeleteDirectoryRecursively(U8Span path)
        {
            return BaseFileSystem.DeleteDirectoryRecursively(path);
        }

        protected override Result DoCleanDirectoryRecursively(U8Span path)
        {
            return BaseFileSystem.CleanDirectoryRecursively(path);
        }

        protected override Result DoDeleteFile(U8Span path)
        {
            return BaseFileSystem.DeleteFile(path);
        }

        protected override Result DoOpenDirectory(out IDirectory directory, U8Span path, OpenDirectoryMode mode)
        {
            UnsafeHelpers.SkipParamInit(out directory);

            Result rc = BaseFileSystem.OpenDirectory(out IDirectory baseDir, path, mode);
            if (rc.IsFailure()) return rc;

            directory = new AesXtsDirectory(BaseFileSystem, baseDir, path.ToU8String(), mode);
            return Result.Success;
        }

        protected override Result DoOpenFile(out IFile file, U8Span path, OpenMode mode)
        {
            UnsafeHelpers.SkipParamInit(out file);

            Result rc = BaseFileSystem.OpenFile(out IFile baseFile, path, mode | OpenMode.Read);
            if (rc.IsFailure()) return rc;

            var xtsFile = new AesXtsFile(mode, baseFile, path.ToU8String(), KekSource, ValidationKey, BlockSize);

            file = xtsFile;
            return Result.Success;
        }

        protected override Result DoRenameDirectory(U8Span oldPath, U8Span newPath)
        {
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
                RenameDirectoryImpl(oldPath.ToString(), newPath.ToString(), false);
            }
            catch (Exception)
            {
                RenameDirectoryImpl(oldPath.ToString(), newPath.ToString(), true);
                BaseFileSystem.RenameDirectory(oldPath, newPath);

                throw;
            }

            return Result.Success;
        }

        private void RenameDirectoryImpl(string srcDir, string dstDir, bool doRollback)
        {
            foreach (DirectoryEntryEx entry in this.EnumerateEntries(dstDir, "*"))
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

        protected override Result DoRenameFile(U8Span oldPath, U8Span newPath)
        {
            // todo: Return proper result codes

            AesXtsFileHeader header = ReadXtsHeader(oldPath.ToString(), oldPath.ToString());

            Result rc = BaseFileSystem.RenameFile(oldPath, newPath);
            if (rc.IsFailure()) return rc;

            try
            {
                WriteXtsHeader(header, newPath.ToString(), newPath.ToString());
            }
            catch (Exception)
            {
                BaseFileSystem.RenameFile(newPath, oldPath);
                WriteXtsHeader(header, oldPath.ToString(), oldPath.ToString());

                throw;
            }

            return Result.Success;
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, U8Span path)
        {
            return BaseFileSystem.GetEntryType(out entryType, path);
        }

        protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, U8Span path)
        {
            return BaseFileSystem.GetFileTimeStampRaw(out timeStamp, path);
        }

        protected override Result DoGetFreeSpaceSize(out long freeSpace, U8Span path)
        {
            return BaseFileSystem.GetFreeSpaceSize(out freeSpace, path);
        }

        protected override Result DoGetTotalSpaceSize(out long totalSpace, U8Span path)
        {
            return BaseFileSystem.GetTotalSpaceSize(out totalSpace, path);
        }

        protected override Result DoCommit()
        {
            return BaseFileSystem.Commit();
        }

        protected override Result DoCommitProvisionally(long counter)
        {
            return BaseFileSystem.CommitProvisionally(counter);
        }

        protected override Result DoRollback()
        {
            return BaseFileSystem.Rollback();
        }

        protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
            U8Span path)
        {
            return BaseFileSystem.QueryEntry(outBuffer, inBuffer, queryId, path);
        }

        private AesXtsFileHeader ReadXtsHeader(string filePath, string keyPath)
        {
            if (!TryReadXtsHeader(filePath, keyPath, out AesXtsFileHeader header))
            {
                ThrowHelper.ThrowResult(ResultFs.AesXtsFileHeaderInvalidKeysInRenameFile.Value, "Could not decrypt AES-XTS keys");
            }

            return header;
        }

        private bool TryReadXtsHeader(string filePath, string keyPath, out AesXtsFileHeader header)
        {
            Debug.Assert(PathTools.IsNormalized(filePath.AsSpan()));
            Debug.Assert(PathTools.IsNormalized(keyPath.AsSpan()));

            header = null;

            Result rc = BaseFileSystem.OpenFile(out IFile file, filePath.ToU8Span(), OpenMode.Read);
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

            BaseFileSystem.OpenFile(out IFile file, filePath.ToU8Span(), OpenMode.ReadWrite);

            using (file)
            {
                file.Write(0, header.ToBytes(false), WriteOption.Flush).ThrowIfFailure();
            }
        }
    }
}
