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

        public DirectoryEntryType GetEntryType(string path)
        {
            path = PathTools.Normalize(path);

            if (FileTable.TryOpenFile(path, out RomFileInfo _))
            {
                return DirectoryEntryType.File;
            }

            if (FileTable.TryOpenDirectory(path, out FindPosition _))
            {
                return DirectoryEntryType.Directory;
            }

            return DirectoryEntryType.NotFound;
        }

        public void Commit() { }

        public IDirectory OpenDirectory(string path, OpenDirectoryMode mode)
        {
            path = PathTools.Normalize(path);

            if (!FileTable.TryOpenDirectory(path, out FindPosition position))
            {
                ThrowHelper.ThrowResult(ResultFs.PathNotFound);
            }

            return new RomFsDirectory(this, path, position, mode);
        }

        public IFile OpenFile(string path, OpenMode mode)
        {
            path = PathTools.Normalize(path);

            if (!FileTable.TryOpenFile(path, out RomFileInfo info))
            {
                ThrowHelper.ThrowResult(ResultFs.PathNotFound);
            }

            if (mode != OpenMode.Read)
            {
                ThrowHelper.ThrowResult(ResultFs.InvalidArgument, "RomFs files must be opened read-only.");
            }

            return new RomFsFile(BaseStorage, Header.DataOffset + info.Offset, info.Length);
        }

        public IStorage GetBaseStorage()
        {
            return BaseStorage;
        }

        public void CreateDirectory(string path) =>
            ThrowHelper.ThrowResult(ResultFs.UnsupportedOperationModifyRomFsFileSystem);

        public void CreateFile(string path, long size, CreateFileOptions options) =>
            ThrowHelper.ThrowResult(ResultFs.UnsupportedOperationModifyRomFsFileSystem);

        public void DeleteDirectory(string path) =>
            ThrowHelper.ThrowResult(ResultFs.UnsupportedOperationModifyRomFsFileSystem);

        public void DeleteDirectoryRecursively(string path) =>
            ThrowHelper.ThrowResult(ResultFs.UnsupportedOperationModifyRomFsFileSystem);

        public void CleanDirectoryRecursively(string path) =>
            ThrowHelper.ThrowResult(ResultFs.UnsupportedOperationModifyRomFsFileSystem);

        public void DeleteFile(string path) =>
            ThrowHelper.ThrowResult(ResultFs.UnsupportedOperationModifyRomFsFileSystem);

        public void RenameDirectory(string srcPath, string dstPath) =>
            ThrowHelper.ThrowResult(ResultFs.UnsupportedOperationModifyRomFsFileSystem);

        public void RenameFile(string srcPath, string dstPath) =>
            ThrowHelper.ThrowResult(ResultFs.UnsupportedOperationModifyRomFsFileSystem);

        public long GetFreeSpaceSize(string path)
        {
            ThrowHelper.ThrowResult(ResultFs.UnsupportedOperationRomFsFileSystemGetSpace);
            return default;
        }

        public long GetTotalSpaceSize(string path)
        {
            ThrowHelper.ThrowResult(ResultFs.UnsupportedOperationRomFsFileSystemGetSpace);
            return default;
        }

        public FileTimeStampRaw GetFileTimeStampRaw(string path)
        {
            ThrowHelper.ThrowResult(ResultFs.NotImplemented);
            return default;
        }

        public void QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, string path, QueryId queryId) =>
            ThrowHelper.ThrowResult(ResultFs.NotImplemented);
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
