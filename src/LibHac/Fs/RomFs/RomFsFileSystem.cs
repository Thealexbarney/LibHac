using System;

namespace LibHac.Fs.RomFs
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

        public Result GetEntryType(out DirectoryEntryType entryType, string path)
        {
            entryType = DirectoryEntryType.NotFound;
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

        public Result Commit()
        {
            return Result.Success;
        }

        public Result OpenDirectory(out IDirectory directory, string path, OpenDirectoryMode mode)
        {
            directory = default;
            path = PathTools.Normalize(path);

            if (!FileTable.TryOpenDirectory(path, out FindPosition position))
            {
                return ResultFs.PathNotFound.Log();
            }

            directory = new RomFsDirectory(this, path, position, mode);
            return Result.Success;
        }

        public Result OpenFile(out IFile file, string path, OpenMode mode)
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

        public Result CreateDirectory(string path) => ResultFs.UnsupportedOperationModifyRomFsFileSystem.Log();
        public Result CreateFile(string path, long size, CreateFileOptions options) => ResultFs.UnsupportedOperationModifyRomFsFileSystem.Log();
        public Result DeleteDirectory(string path) => ResultFs.UnsupportedOperationModifyRomFsFileSystem.Log();
        public Result DeleteDirectoryRecursively(string path) => ResultFs.UnsupportedOperationModifyRomFsFileSystem.Log();
        public Result CleanDirectoryRecursively(string path) => ResultFs.UnsupportedOperationModifyRomFsFileSystem.Log();
        public Result DeleteFile(string path) => ResultFs.UnsupportedOperationModifyRomFsFileSystem.Log();
        public Result RenameDirectory(string oldPath, string newPath) => ResultFs.UnsupportedOperationModifyRomFsFileSystem.Log();
        public Result RenameFile(string oldPath, string newPath) => ResultFs.UnsupportedOperationModifyRomFsFileSystem.Log();

        public Result GetFreeSpaceSize(out long freeSpace, string path)
        {
            freeSpace = default;
            return ResultFs.UnsupportedOperationRomFsFileSystemGetSpace.Log();
        }

        public Result GetTotalSpaceSize(out long totalSpace, string path)
        {
            totalSpace = default;
            return ResultFs.UnsupportedOperationRomFsFileSystemGetSpace.Log();
        }

        public Result GetFileTimeStampRaw(out FileTimeStampRaw timeStamp, string path)
        {
            timeStamp = default;
            return ResultFs.NotImplemented.Log();
        }

        public Result QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, string path)
        {
            return ResultFs.NotImplemented.Log();
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
