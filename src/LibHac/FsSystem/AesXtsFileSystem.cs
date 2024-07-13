using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem.Impl
{
    public class AesXtsFileSystemDirectory<THeader> : IDirectory where THeader : IAesXtsFileHeader
    {
        private IFileSystem _baseFileSystem;
        private UniqueRef<IDirectory> _baseDirectory;
        private OpenDirectoryMode _openMode;
        private Path.Stored _path;

        public AesXtsFileSystemDirectory(IFileSystem fileSystem, ref UniqueRef<IDirectory> baseDirectory,
            OpenDirectoryMode mode)
        {
            throw new NotImplementedException();
        }

        public override void Dispose()
        {
            throw new NotImplementedException();
        }

        public Result Initialize(ref readonly Path path)
        {
            throw new NotImplementedException();
        }

        protected override Result DoGetEntryCount(out long entryCount)
        {
            throw new NotImplementedException();
        }

        protected override Result DoRead(out long entriesRead, Span<DirectoryEntry> entryBuffer)
        {
            throw new NotImplementedException();
        }
    }
}

namespace LibHac.FsSystem
{
    public class AesXtsFileSystemImpl<THeader, TFileContext, TFsContext> : IFileSystem
        where THeader : IAesXtsFileHeader
        where TFileContext : IAesXtsFileHeader.IFileContext
        where TFsContext : IAesXtsFileHeader.IFileSystemContext
    {
        private SharedRef<IFileSystem> _baseFileSystem;
        private TFsContext _context;
        private int _xtsBlockSize;

        public AesXtsFileSystemImpl(ref readonly SharedRef<IFileSystem> baseFileSystem, ref TFsContext context,
            int xtsBlockSize)
        {
            throw new NotImplementedException();
        }

        public override void Dispose()
        {
            throw new NotImplementedException();
        }

        private Result CheckPathFormat(ref readonly Path path)
        {
            throw new NotImplementedException();
        }

        protected override Result DoCreateFile(ref readonly Path path, long size, CreateFileOptions option)
        {
            throw new NotImplementedException();
        }

        protected override Result DoDeleteFile(ref readonly Path path)
        {
            throw new NotImplementedException();
        }

        protected override Result DoCreateDirectory(ref readonly Path path)
        {
            throw new NotImplementedException();
        }

        protected override Result DoDeleteDirectory(ref readonly Path path)
        {
            throw new NotImplementedException();
        }

        protected override Result DoDeleteDirectoryRecursively(ref readonly Path path)
        {
            throw new NotImplementedException();
        }

        protected override Result DoCleanDirectoryRecursively(ref readonly Path path)
        {
            throw new NotImplementedException();
        }

        protected override Result DoRenameFile(ref readonly Path currentPath, ref readonly Path newPath)
        {
            throw new NotImplementedException();
        }

        protected override Result DoRenameDirectory(ref readonly Path currentPath, ref readonly Path newPath)
        {
            throw new NotImplementedException();
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, ref readonly Path path)
        {
            throw new NotImplementedException();
        }

        protected override Result DoGetFreeSpaceSize(out long freeSpace, ref readonly Path path)
        {
            throw new NotImplementedException();
        }

        protected override Result DoGetTotalSpaceSize(out long totalSpace, ref readonly Path path)
        {
            throw new NotImplementedException();
        }

        protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, ref readonly Path path, OpenMode mode)
        {
            throw new NotImplementedException();
        }

        protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, ref readonly Path path,
            OpenDirectoryMode mode)
        {
            throw new NotImplementedException();
        }

        protected override Result DoCommit()
        {
            throw new NotImplementedException();
        }

        protected override Result DoCommitProvisionally(long counter)
        {
            throw new NotImplementedException();
        }

        protected override Result DoRollback()
        {
            throw new NotImplementedException();
        }

        protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
            ref readonly Path path)
        {
            throw new NotImplementedException();
        }

        private Result DecryptHeader(ref THeader header, ref readonly Path path, ReadOnlySpan<byte> keyPath)
        {
            throw new NotImplementedException();
        }

        private Result EncryptHeader(ref THeader header, ref readonly Path path, ReadOnlySpan<byte> keyPath)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class AesXtsFileSystemV0 : AesXtsFileSystemImpl<AesXtsFileHeaderV0, AesXtsFileHeaderV0.FileContext,
        AesXtsFileHeaderV0.FileSystemContext>
    {
        public AesXtsFileSystemV0(ref readonly SharedRef<IFileSystem> baseFileSystem,
            ref AesXtsFileHeaderV0.FileSystemContext context, int xtsBlockSize) : base(in baseFileSystem, ref context,
            xtsBlockSize)
        {
        }
    }
}