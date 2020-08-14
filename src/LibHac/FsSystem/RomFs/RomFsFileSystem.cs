using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem.RomFs
{
    public class RomFsFileSystem : IFileSystem
    {
        public RomfsHeader Header { get; }

        public HierarchicalRomFileTable<RomFileInfo> FileTable { get; }
        private IStorage BaseStorage { get; }

        public RomFsFileSystem(IStorage storage)
        {
            BaseStorage = storage;
            Header = new RomfsHeader(storage.AsFile(OpenMode.Read));

            IStorage dirHashTable = storage.Slice(Header.DirHashTableOffset, Header.DirHashTableSize);
            IStorage dirEntryTable = storage.Slice(Header.DirMetaTableOffset, Header.DirMetaTableSize);
            IStorage fileHashTable = storage.Slice(Header.FileHashTableOffset, Header.FileHashTableSize);
            IStorage fileEntryTable = storage.Slice(Header.FileMetaTableOffset, Header.FileMetaTableSize);

            FileTable = new HierarchicalRomFileTable<RomFileInfo>(dirHashTable, dirEntryTable, fileHashTable, fileEntryTable);
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, U8Span path)
        {
            entryType = default;

            if (FileTable.TryOpenFile(path.ToString(), out RomFileInfo _))
            {
                entryType = DirectoryEntryType.File;
                return Result.Success;
            }

            if (FileTable.TryOpenDirectory(path.ToString(), out FindPosition _))
            {
                entryType = DirectoryEntryType.Directory;
                return Result.Success;
            }

            return ResultFs.PathNotFound.Log();
        }

        protected override Result DoCommit()
        {
            return Result.Success;
        }

        protected override Result DoOpenDirectory(out IDirectory directory, U8Span path, OpenDirectoryMode mode)
        {
            directory = default;

            if (!FileTable.TryOpenDirectory(path.ToString(), out FindPosition position))
            {
                return ResultFs.PathNotFound.Log();
            }

            directory = new RomFsDirectory(this, position, mode);
            return Result.Success;
        }

        protected override Result DoOpenFile(out IFile file, U8Span path, OpenMode mode)
        {
            file = default;

            if (!FileTable.TryOpenFile(path.ToString(), out RomFileInfo info))
            {
                return ResultFs.PathNotFound.Log();
            }

            if (mode != OpenMode.Read)
            {
                // RomFs files must be opened read-only.
                return ResultFs.InvalidArgument.Log();
            }

            file = new RomFsFile(BaseStorage, Header.DataOffset + info.Offset, info.Length);
            return Result.Success;
        }

        public IStorage GetBaseStorage()
        {
            return BaseStorage;
        }

        protected override Result DoCreateDirectory(U8Span path) => ResultFs.UnsupportedOperationModifyRomFsFileSystem.Log();
        protected override Result DoCreateFile(U8Span path, long size, CreateFileOptions options) => ResultFs.UnsupportedOperationModifyRomFsFileSystem.Log();
        protected override Result DoDeleteDirectory(U8Span path) => ResultFs.UnsupportedOperationModifyRomFsFileSystem.Log();
        protected override Result DoDeleteDirectoryRecursively(U8Span path) => ResultFs.UnsupportedOperationModifyRomFsFileSystem.Log();
        protected override Result DoCleanDirectoryRecursively(U8Span path) => ResultFs.UnsupportedOperationModifyRomFsFileSystem.Log();
        protected override Result DoDeleteFile(U8Span path) => ResultFs.UnsupportedOperationModifyRomFsFileSystem.Log();
        protected override Result DoRenameDirectory(U8Span oldPath, U8Span newPath) => ResultFs.UnsupportedOperationModifyRomFsFileSystem.Log();
        protected override Result DoRenameFile(U8Span oldPath, U8Span newPath) => ResultFs.UnsupportedOperationModifyRomFsFileSystem.Log();
        protected override Result DoCommitProvisionally(long counter) => ResultFs.UnsupportedOperationInRomFsFileSystem.Log();

        protected override Result DoGetFreeSpaceSize(out long freeSpace, U8Span path)
        {
            freeSpace = default;
            return ResultFs.UnsupportedOperationRomFsFileSystemGetSpace.Log();
        }

        protected override Result DoGetTotalSpaceSize(out long totalSpace, U8Span path)
        {
            totalSpace = default;
            return ResultFs.UnsupportedOperationRomFsFileSystemGetSpace.Log();
        }
    }

    public class RomfsHeader
    {
        public long HeaderSize { get; }
        public long DirHashTableOffset { get; }
        public long DirHashTableSize { get; }
        public long DirMetaTableOffset { get; }
        public long DirMetaTableSize { get; }
        public long FileHashTableOffset { get; }
        public long FileHashTableSize { get; }
        public long FileMetaTableOffset { get; }
        public long FileMetaTableSize { get; }
        public long DataOffset { get; }

        public RomfsHeader(IFile file)
        {
            var reader = new FileReader(file);

            HeaderSize = reader.ReadInt32();

            Func<long> func;

            // Old pre-release romfs is exactly the same except the fields in the header are 32-bit instead of 64-bit
            if (HeaderSize == 0x28)
            {
                func = () => reader.ReadInt32();
            }
            else
            {
                func = reader.ReadInt64;
                reader.Position += 4;
            }

            DirHashTableOffset = func();
            DirHashTableSize = func();
            DirMetaTableOffset = func();
            DirMetaTableSize = func();
            FileHashTableOffset = func();
            FileHashTableSize = func();
            FileMetaTableOffset = func();
            FileMetaTableSize = func();
            DataOffset = func();
        }
    }
}
