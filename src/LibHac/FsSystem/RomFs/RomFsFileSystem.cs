using LibHac.Fs;

namespace LibHac.FsSystem.RomFs
{
    public class RomFsFileSystem : FileSystemBase
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

        protected override Result GetEntryTypeImpl(out DirectoryEntryType entryType, string path)
        {
            entryType = default;
            path = PathTools.Normalize(path);

            if (FileTable.TryOpenFile(path, out RomFileInfo _))
            {
                entryType = DirectoryEntryType.File;
                return Result.Success;
            }

            if (FileTable.TryOpenDirectory(path, out FindPosition _))
            {
                entryType = DirectoryEntryType.Directory;
                return Result.Success;
            }

            return ResultFs.PathNotFound.Log();
        }

        protected override Result CommitImpl()
        {
            return Result.Success;
        }

        protected override Result OpenDirectoryImpl(out IDirectory directory, string path, OpenDirectoryMode mode)
        {
            directory = default;
            path = PathTools.Normalize(path);

            if (!FileTable.TryOpenDirectory(path, out FindPosition position))
            {
                return ResultFs.PathNotFound.Log();
            }

            directory = new RomFsDirectory(this, position, mode);
            return Result.Success;
        }

        protected override Result OpenFileImpl(out IFile file, string path, OpenMode mode)
        {
            file = default;
            path = PathTools.Normalize(path);

            if (!FileTable.TryOpenFile(path, out RomFileInfo info))
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

        protected override Result CreateDirectoryImpl(string path) => ResultFs.UnsupportedOperationModifyRomFsFileSystem.Log();
        protected override Result CreateFileImpl(string path, long size, CreateFileOptions options) => ResultFs.UnsupportedOperationModifyRomFsFileSystem.Log();
        protected override Result DeleteDirectoryImpl(string path) => ResultFs.UnsupportedOperationModifyRomFsFileSystem.Log();
        protected override Result DeleteDirectoryRecursivelyImpl(string path) => ResultFs.UnsupportedOperationModifyRomFsFileSystem.Log();
        protected override Result CleanDirectoryRecursivelyImpl(string path) => ResultFs.UnsupportedOperationModifyRomFsFileSystem.Log();
        protected override Result DeleteFileImpl(string path) => ResultFs.UnsupportedOperationModifyRomFsFileSystem.Log();
        protected override Result RenameDirectoryImpl(string oldPath, string newPath) => ResultFs.UnsupportedOperationModifyRomFsFileSystem.Log();
        protected override Result RenameFileImpl(string oldPath, string newPath) => ResultFs.UnsupportedOperationModifyRomFsFileSystem.Log();

        protected override Result GetFreeSpaceSizeImpl(out long freeSpace, string path)
        {
            freeSpace = default;
            return ResultFs.UnsupportedOperationRomFsFileSystemGetSpace.Log();
        }

        protected override Result GetTotalSpaceSizeImpl(out long totalSpace, string path)
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

            HeaderSize = reader.ReadInt64();
            DirHashTableOffset = reader.ReadInt64();
            DirHashTableSize = reader.ReadInt64();
            DirMetaTableOffset = reader.ReadInt64();
            DirMetaTableSize = reader.ReadInt64();
            FileHashTableOffset = reader.ReadInt64();
            FileHashTableSize = reader.ReadInt64();
            FileMetaTableOffset = reader.ReadInt64();
            FileMetaTableSize = reader.ReadInt64();
            DataOffset = reader.ReadInt64();
        }
    }
}
