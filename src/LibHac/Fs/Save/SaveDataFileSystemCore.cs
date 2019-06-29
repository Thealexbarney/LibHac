﻿using System;
using System.IO;

namespace LibHac.Fs.Save
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

        public void CreateDirectory(string path)
        {
            path = PathTools.Normalize(path);

            FileTable.AddDirectory(path);
        }

        public void CreateFile(string path, long size, CreateFileOptions options)
        {
            path = PathTools.Normalize(path);

            if (size == 0)
            {
                var emptyFileEntry = new SaveFileInfo { StartBlock = int.MinValue, Length = size };
                FileTable.AddFile(path, ref emptyFileEntry);

                return;
            }

            int blockCount = (int)Util.DivideByRoundUp(size, AllocationTable.Header.BlockSize);
            int startBlock = AllocationTable.Allocate(blockCount);

            if (startBlock == -1)
            {
                ThrowHelper.ThrowResult(ResultFs.AllocationTableInsufficientFreeBlocks,
                    "Not enough available space to create file.");
            }

            var fileEntry = new SaveFileInfo { StartBlock = startBlock, Length = size };

            FileTable.AddFile(path, ref fileEntry);
        }

        public void DeleteDirectory(string path)
        {
            path = PathTools.Normalize(path);

            FileTable.DeleteDirectory(path);
        }

        public void DeleteDirectoryRecursively(string path)
        {
            path = PathTools.Normalize(path);

            CleanDirectoryRecursively(path);
            DeleteDirectory(path);
        }

        public void CleanDirectoryRecursively(string path)
        {
            path = PathTools.Normalize(path);

            IDirectory dir = OpenDirectory(path, OpenDirectoryMode.All);
            FileSystemExtensions.CleanDirectoryRecursivelyGeneric(dir);
        }

        public void DeleteFile(string path)
        {
            path = PathTools.Normalize(path);

            if (!FileTable.TryOpenFile(path, out SaveFileInfo fileInfo))
            {
                ThrowHelper.ThrowResult(ResultFs.PathNotFound);
            }

            if (fileInfo.StartBlock != int.MinValue)
            {
                AllocationTable.Free(fileInfo.StartBlock);
            }

            FileTable.DeleteFile(path);
        }

        public IDirectory OpenDirectory(string path, OpenDirectoryMode mode)
        {
            path = PathTools.Normalize(path);

            if (!FileTable.TryOpenDirectory(path, out SaveFindPosition position))
            {
                ThrowHelper.ThrowResult(ResultFs.PathNotFound);
            }

            return new SaveDataDirectory(this, path, position, mode);
        }

        public IFile OpenFile(string path, OpenMode mode)
        {
            path = PathTools.Normalize(path);

            if (!FileTable.TryOpenFile(path, out SaveFileInfo file))
            {
                ThrowHelper.ThrowResult(ResultFs.PathNotFound);
            }

            AllocationTableStorage storage = OpenFatStorage(file.StartBlock);

            return new SaveDataFile(storage, path, FileTable, file.Length, mode);
        }

        public void RenameDirectory(string srcPath, string dstPath)
        {
            srcPath = PathTools.Normalize(srcPath);
            dstPath = PathTools.Normalize(dstPath);

            FileTable.RenameDirectory(srcPath, dstPath);
        }

        public void RenameFile(string srcPath, string dstPath)
        {
            srcPath = PathTools.Normalize(srcPath);
            dstPath = PathTools.Normalize(dstPath);

            FileTable.RenameFile(srcPath, dstPath);
        }

        public DirectoryEntryType GetEntryType(string path)
        {
            path = PathTools.Normalize(path);

            if (FileTable.TryOpenFile(path, out SaveFileInfo _))
            {
                return DirectoryEntryType.File;
            }

            if (FileTable.TryOpenDirectory(path, out SaveFindPosition _))
            {
                return DirectoryEntryType.Directory;
            }

            return DirectoryEntryType.NotFound;
        }

        public long GetFreeSpaceSize(string path)
        {
            int freeBlockCount = AllocationTable.GetFreeListLength();
            return Header.BlockSize * freeBlockCount;
        }

        public long GetTotalSpaceSize(string path)
        {
            return Header.BlockSize * Header.BlockCount;
        }

        public void Commit()
        {

        }

        public FileTimeStampRaw GetFileTimeStampRaw(string path)
        {
            ThrowHelper.ThrowResult(ResultFs.NotImplemented);
            return default;
        }

        public void QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, string path, QueryId queryId) =>
            ThrowHelper.ThrowResult(ResultFs.NotImplemented);

        public IStorage GetBaseStorage() => BaseStorage.AsReadOnly();
        public IStorage GetHeaderStorage() => HeaderStorage.AsReadOnly();

        public void FsTrim()
        {
            AllocationTable.FsTrim();

            foreach (DirectoryEntry file in this.EnumerateEntries("*", SearchOptions.RecurseSubdirectories))
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
