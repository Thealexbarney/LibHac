using System;
using System.IO;
using LibHac.Fs;

namespace LibHac.FsSystem.Save
{
    public class SaveDataFileSystemCore : IFileSystem
    {
        private IStorage BaseStorage { get; }
        private IStorage HeaderStorage { get; }

        public AllocationTable AllocationTable { get; }
        private SaveHeader Header { get; }

        public HierarchicalSaveFileTable FileTable { get; }

        public SaveDataFileSystemCore(IStorage storage, IStorage allocationTable, IStorage header)
        {
            HeaderStorage = header;
            BaseStorage = storage;
            AllocationTable = new AllocationTable(allocationTable, header.Slice(0x18, 0x30));

            Header = new SaveHeader(HeaderStorage);

            AllocationTableStorage dirTableStorage = OpenFatStorage(AllocationTable.Header.DirectoryTableBlock);
            AllocationTableStorage fileTableStorage = OpenFatStorage(AllocationTable.Header.FileTableBlock);

            FileTable = new HierarchicalSaveFileTable(dirTableStorage, fileTableStorage);
        }

        public Result CreateDirectory(string path)
        {
            path = PathTools.Normalize(path);

            FileTable.AddDirectory(path);

            return Result.Success;
        }

        public Result CreateFile(string path, long size, CreateFileOptions options)
        {
            path = PathTools.Normalize(path);

            if (size == 0)
            {
                var emptyFileEntry = new SaveFileInfo { StartBlock = int.MinValue, Length = size };
                FileTable.AddFile(path, ref emptyFileEntry);

                return Result.Success;
            }

            int blockCount = (int)Util.DivideByRoundUp(size, AllocationTable.Header.BlockSize);
            int startBlock = AllocationTable.Allocate(blockCount);

            if (startBlock == -1)
            {
                return ResultFs.AllocationTableInsufficientFreeBlocks.Log();
            }

            var fileEntry = new SaveFileInfo { StartBlock = startBlock, Length = size };

            FileTable.AddFile(path, ref fileEntry);

            return Result.Success;
        }

        public Result DeleteDirectory(string path)
        {
            path = PathTools.Normalize(path);

            FileTable.DeleteDirectory(path);

            return Result.Success;
        }

        public Result DeleteDirectoryRecursively(string path)
        {
            path = PathTools.Normalize(path);

            Result rc = CleanDirectoryRecursively(path);
            if (rc.IsFailure()) return rc;

            DeleteDirectory(path);

            return Result.Success;
        }

        public Result CleanDirectoryRecursively(string path)
        {
            path = PathTools.Normalize(path);

            FileSystemExtensions.CleanDirectoryRecursivelyGeneric(this, path);

            return Result.Success;
        }

        public Result DeleteFile(string path)
        {
            path = PathTools.Normalize(path);

            if (!FileTable.TryOpenFile(path, out SaveFileInfo fileInfo))
            {
                return ResultFs.PathNotFound.Log();
            }

            if (fileInfo.StartBlock != int.MinValue)
            {
                AllocationTable.Free(fileInfo.StartBlock);
            }

            FileTable.DeleteFile(path);

            return Result.Success;
        }

        public Result OpenDirectory(out IDirectory directory, string path, OpenDirectoryMode mode)
        {
            directory = default;
            path = PathTools.Normalize(path);

            if (!FileTable.TryOpenDirectory(path, out SaveFindPosition position))
            {
                return ResultFs.PathNotFound.Log();
            }

            directory = new SaveDataDirectory(this, position, mode);

            return Result.Success;
        }

        public Result OpenFile(out IFile file, string path, OpenMode mode)
        {
            file = default;
            path = PathTools.Normalize(path);

            if (!FileTable.TryOpenFile(path, out SaveFileInfo fileInfo))
            {
                return ResultFs.PathNotFound.Log();
            }

            AllocationTableStorage storage = OpenFatStorage(fileInfo.StartBlock);

            file = new SaveDataFile(storage, path, FileTable, fileInfo.Length, mode);

            return Result.Success;
        }

        public Result RenameDirectory(string oldPath, string newPath)
        {
            oldPath = PathTools.Normalize(oldPath);
            newPath = PathTools.Normalize(newPath);

            return FileTable.RenameDirectory(oldPath, newPath);
        }

        public Result RenameFile(string oldPath, string newPath)
        {
            oldPath = PathTools.Normalize(oldPath);
            newPath = PathTools.Normalize(newPath);

            FileTable.RenameFile(oldPath, newPath);

            return Result.Success;
        }

        public Result GetEntryType(out DirectoryEntryType entryType, string path)
        {
            path = PathTools.Normalize(path);

            if (FileTable.TryOpenFile(path, out SaveFileInfo _))
            {
                entryType = DirectoryEntryType.File;
                return Result.Success;
            }

            if (FileTable.TryOpenDirectory(path, out SaveFindPosition _))
            {
                entryType = DirectoryEntryType.Directory;
                return Result.Success;
            }

            entryType = DirectoryEntryType.NotFound;
            return ResultFs.PathNotFound.Log();
        }

        public Result GetFreeSpaceSize(out long freeSpace, string path)
        {
            int freeBlockCount = AllocationTable.GetFreeListLength();
            freeSpace = Header.BlockSize * freeBlockCount;

            return Result.Success;
        }

        public Result GetTotalSpaceSize(out long totalSpace, string path)
        {
            totalSpace = Header.BlockSize * Header.BlockCount;

            return Result.Success;
        }

        public Result Commit()
        {
            return Result.Success;
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

        public IStorage GetBaseStorage() => BaseStorage.AsReadOnly();
        public IStorage GetHeaderStorage() => HeaderStorage.AsReadOnly();

        public void FsTrim()
        {
            AllocationTable.FsTrim();

            foreach (DirectoryEntryEx file in this.EnumerateEntries("*", SearchOptions.RecurseSubdirectories))
            {
                if (FileTable.TryOpenFile(file.FullPath, out SaveFileInfo fileInfo) && fileInfo.StartBlock >= 0)
                {
                    AllocationTable.FsTrimList(fileInfo.StartBlock);

                    OpenFatStorage(fileInfo.StartBlock).Slice(fileInfo.Length).Fill(SaveDataFileSystem.TrimFillValue);
                }
            }

            int freeIndex = AllocationTable.GetFreeListBlockIndex();
            if (freeIndex == 0) return;

            AllocationTable.FsTrimList(freeIndex);

            OpenFatStorage(freeIndex).Fill(SaveDataFileSystem.TrimFillValue);

            FileTable.TrimFreeEntries();
        }

        private AllocationTableStorage OpenFatStorage(int blockIndex)
        {
            return new AllocationTableStorage(BaseStorage, AllocationTable, (int)Header.BlockSize, blockIndex);
        }
    }

    public class SaveHeader
    {
        public string Magic { get; }
        public uint Version { get; }
        public long BlockCount { get; }
        public long BlockSize { get; }


        public SaveHeader(IStorage storage)
        {
            var reader = new BinaryReader(storage.AsStream());

            Magic = reader.ReadAscii(4);
            Version = reader.ReadUInt32();
            BlockCount = reader.ReadInt64();
            BlockSize = reader.ReadInt64();
        }
    }
}
