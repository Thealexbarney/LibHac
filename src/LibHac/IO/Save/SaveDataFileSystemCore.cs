﻿using System.IO;

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
            
            // todo: Query the FAT for the file size when none is given
            AllocationTableStorage dirTableStorage = OpenFatBlock(AllocationTable.Header.DirectoryTableBlock, 1000000);
            AllocationTableStorage fileTableStorage = OpenFatBlock(AllocationTable.Header.FileTableBlock, 1000000);

            FileTable = new HierarchicalSaveFileTable(dirTableStorage, fileTableStorage);
        }

        public void CreateDirectory(string path)
        {
            throw new System.NotImplementedException();
        }

        public void CreateFile(string path, long size, CreateFileOptions options)
        {
            throw new System.NotImplementedException();
        }

        public void DeleteDirectory(string path)
        {
            throw new System.NotImplementedException();
        }

        public void DeleteFile(string path)
        {
            throw new System.NotImplementedException();
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

            if (file.StartBlock < 0)
            {
                return new NullFile();
            }

            AllocationTableStorage storage = OpenFatBlock(file.StartBlock, file.Length);

            return new SaveDataFile(storage, 0, file.Length, mode);
        }

        public void RenameDirectory(string srcPath, string dstPath)
        {
            throw new System.NotImplementedException();
        }

        public void RenameFile(string srcPath, string dstPath)
        {
            throw new System.NotImplementedException();
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
            throw new System.NotImplementedException();
        }

        public IStorage GetBaseStorage() => BaseStorage.AsReadOnly();
        public IStorage GetHeaderStorage() => HeaderStorage.AsReadOnly();

        private AllocationTableStorage OpenFatBlock(int blockIndex, long size)
        {
            return new AllocationTableStorage(BaseStorage, AllocationTable, (int)Header.BlockSize, blockIndex, size);
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
