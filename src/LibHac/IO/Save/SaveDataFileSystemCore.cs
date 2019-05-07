using System;
using System.IO;

namespace LibHac.IO.Save
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
            throw new NotImplementedException();
        }

        public void CreateFile(string path, long size, CreateFileOptions options)
        {
            path = PathTools.Normalize(path);

            int blockCount = (int)Util.DivideByRoundUp(size, AllocationTable.Header.BlockSize);
            int startBlock = AllocationTable.Allocate(blockCount);

            var fileEntry = new SaveFileInfo { StartBlock = startBlock, Length = size };

            FileTable.AddFile(path, ref fileEntry);
        }

        public void DeleteDirectory(string path)
        {
            throw new NotImplementedException();
        }

        public void DeleteFile(string path)
        {
            path = PathTools.Normalize(path);

            if (!FileTable.TryOpenFile(path, out SaveFileInfo fileInfo))
            {
                throw new FileNotFoundException();
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
                throw new DirectoryNotFoundException();
            }

            return new SaveDataDirectory(this, path, position, mode);
        }

        public IFile OpenFile(string path, OpenMode mode)
        {
            path = PathTools.Normalize(path);

            if (!FileTable.TryOpenFile(path, out SaveFileInfo file))
            {
                throw new FileNotFoundException();
            }

            AllocationTableStorage storage = OpenFatStorage(file.StartBlock);

            return new SaveDataFile(storage, path, FileTable, file.Length, mode);
        }

        public void RenameDirectory(string srcPath, string dstPath)
        {
            throw new NotImplementedException();
        }

        public void RenameFile(string srcPath, string dstPath)
        {
            throw new NotImplementedException();
        }

        public bool DirectoryExists(string path)
        {
            path = PathTools.Normalize(path);

            return FileTable.TryOpenDirectory(path, out SaveFindPosition _);
        }

        public bool FileExists(string path)
        {
            path = PathTools.Normalize(path);

            return FileTable.TryOpenFile(path, out SaveFileInfo _);
        }

        public DirectoryEntryType GetEntryType(string path)
        {
            path = PathTools.Normalize(path);

            if (FileExists(path)) return DirectoryEntryType.File;
            if (DirectoryExists(path)) return DirectoryEntryType.Directory;

            throw new FileNotFoundException(path);
        }

        public void Commit()
        {

        }

        public void QueryEntry(Span<byte> outBuffer, Span<byte> inBuffer, string path, QueryId queryId) => throw new NotSupportedException();

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
