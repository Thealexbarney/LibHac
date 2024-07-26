// ReSharper disable UnusedMember.Local NotAccessedField.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using System;
using LibHac.Common;
using LibHac.Fs.Fsa;

namespace LibHac.Fs.Impl
{
    public class RomFsFile : IFile
    {
        private RomFsFileSystem _parent;
        private long _startOffset;
        private long _emdOffset;

        public RomFsFile(RomFsFileSystem parent, long startOffset, long emdOffset)
        {
            throw new NotImplementedException();
        }

        public long GetOffset()
        {
            throw new NotImplementedException();
        }

        public long GetSize()
        {
            throw new NotImplementedException();
        }

        public IStorage GetStorage()
        {
            throw new NotImplementedException();
        }

        protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination, in ReadOption option)
        {
            throw new NotImplementedException();
        }

        protected override Result DoGetSize(out long size)
        {
            throw new NotImplementedException();
        }

        protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            throw new NotImplementedException();
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
        {
            throw new NotImplementedException();
        }

        protected override Result DoSetSize(long size)
        {
            throw new NotImplementedException();
        }

        protected override Result DoFlush()
        {
            throw new NotImplementedException();
        }
    }
}

namespace LibHac.Fs
{
    file static class Anonymous
    {
        public static long CalculateRequiredWorkingMemorySize(in RomFileSystemInformation fsInfo)
        {
            return fsInfo.DirectoryBucketSize + fsInfo.DirectoryEntrySize + fsInfo.FileBucketSize + fsInfo.FileEntrySize;
        }

        public static Result ReadFile(IStorage storage, long offset, Span<byte> buffer)
        {
            throw new NotImplementedException();
        }

        public static Result ReadFileHeader(IStorage storage, out RomFileSystemInformation outHeader)
        {
            throw new NotImplementedException();
        }
    }

    file class RomFsDirectory : IDirectory
    {
        private RomFsFileSystem _parent;
        private HierarchicalRomFileTable.FindPosition _currentPosition;
        private HierarchicalRomFileTable.FindPosition _initialPosition;
        private OpenDirectoryMode _mode;

        public RomFsDirectory(RomFsFileSystem parent, in HierarchicalRomFileTable.FindPosition initialFindPosition,
            OpenDirectoryMode mode)
        {
            throw new NotImplementedException();
        }

        public override void Dispose()
        {
            throw new NotImplementedException();
        }

        protected override Result DoRead(out long entriesRead, Span<DirectoryEntry> entryBuffer)
        {
            throw new NotImplementedException();
        }

        protected override Result DoGetEntryCount(out long entryCount)
        {
            throw new NotImplementedException();
        }

        private Result ReadInternal(out long outReadCount, ref HierarchicalRomFileTable.FindPosition findPosition,
            Span<DirectoryEntry> entryBuffer)
        {
            throw new NotImplementedException();
        }
    }

    public class RomFsFileSystem : IFileSystem
    {
        private HierarchicalRomFileTable _romFileTable;
        private IStorage _baseStorage;
        private UniqueRef<IStorage> _uniqueBaseStorage;
        private UniqueRef<IStorage> _directoryBucketStorage;
        private UniqueRef<IStorage> _directoryEntryStorage;
        private UniqueRef<IStorage> _fileBucketStorage;
        private UniqueRef<IStorage> _fileEntryStorage;
        private long _entrySize;

        public RomFsFileSystem()
        {
            throw new NotImplementedException();
        }

        public override void Dispose()
        {
            throw new NotImplementedException();
        }

        public IStorage GetBaseStorage() => _baseStorage;
        public HierarchicalRomFileTable GetRomFileTable() => _romFileTable;

        public static Result GetRequiredWorkingMemorySize(out long outValue, IStorage storage)
        {
            throw new NotImplementedException();
        }

        public Result Initialize(ref UniqueRef<IStorage> baseStorage, Memory<byte> workingMemory,
            bool isFileSystemCacheUsed)
        {
            throw new NotImplementedException();
        }

        public Result Initialize(IStorage baseStorage, Memory<byte> workingMemory, bool isFileSystemCacheUsed)
        {
            throw new NotImplementedException();
        }

        protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, ref readonly Path path, OpenMode mode)
        {
            throw new NotImplementedException();
        }

        protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, ref readonly Path path, OpenDirectoryMode mode)
        {
            throw new NotImplementedException();
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, ref readonly Path path)
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

        protected override Result DoGetFreeSpaceSize(out long freeSpace, ref readonly Path path)
        {
            throw new NotImplementedException();
        }

        protected override Result DoGetTotalSpaceSize(out long totalSpace, ref readonly Path path)
        {
            throw new NotImplementedException();
        }

        protected override Result DoGetFileSystemAttribute(out FileSystemAttribute outAttribute)
        {
            throw new NotImplementedException();
        }

        public Result GetFileBaseOffset(out long outOffset, ReadOnlySpan<byte> path)
        {
            throw new NotImplementedException();
        }

        private Result GetFileInfo(out RomFileInfo outInfo, ReadOnlySpan<byte> path)
        {
            throw new NotImplementedException();
        }
    }
}