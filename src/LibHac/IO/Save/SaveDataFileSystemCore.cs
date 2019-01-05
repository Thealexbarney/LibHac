using System.Collections.Generic;
using System.IO;

namespace LibHac.IO.Save
{
    public class SaveDataFileSystemCore : IFileSystem
    {
        private IStorage BaseStorage { get; }
        private IStorage HeaderStorage { get; }

        public AllocationTable AllocationTable { get; }
        private SaveHeader Header { get; }

        public SaveDirectoryEntry RootDirectory { get; private set; }
        private SaveFileEntry[] Files { get; set; }
        private SaveDirectoryEntry[] Directories { get; set; }
        private Dictionary<string, SaveFileEntry> FileDictionary { get; }
        private Dictionary<string, SaveDirectoryEntry> DirDictionary { get; }

        public SaveDataFileSystemCore(IStorage storage, IStorage allocationTable, IStorage header)
        {
            HeaderStorage = header;
            BaseStorage = storage;
            AllocationTable = new AllocationTable(allocationTable, header.Slice(0x18, 0x30));

            Header = new SaveHeader(HeaderStorage);

            ReadFileInfo();

            FileDictionary = new Dictionary<string, SaveFileEntry>();
            foreach (SaveFileEntry entry in Files)
            {
                FileDictionary[entry.FullPath] = entry;
            }

            DirDictionary = new Dictionary<string, SaveDirectoryEntry>();
            foreach (SaveDirectoryEntry entry in Directories)
            {
                DirDictionary[entry.FullPath] = entry;
            }
        }

        public IStorage OpenFile(string filename)
        {
            if (!FileDictionary.TryGetValue(filename, out SaveFileEntry file))
            {
                throw new FileNotFoundException();
            }

            return OpenFile(file);
        }

        public IStorage OpenFile(SaveFileEntry file)
        {
            if (file.BlockIndex < 0)
            {
                // todo
                return new MemoryStorage(new byte[0]);
            }

            return OpenFatBlock(file.BlockIndex, file.FileSize);
        }

        public void CreateDirectory(string path)
        {
            throw new System.NotImplementedException();
        }

        public void CreateFile(string path, long size)
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

            if (!DirDictionary.TryGetValue(path, out SaveDirectoryEntry dir))
            {
                throw new DirectoryNotFoundException(path);
            }

            return new SaveDataDirectory(this, path, dir, mode);
        }

        public IFile OpenFile(string path, OpenMode mode)
        {
            if (!FileDictionary.TryGetValue(path, out SaveFileEntry file))
            {
                throw new FileNotFoundException();
            }

            if (file.BlockIndex < 0)
            {
                // todo
                return new StorageFile(new MemoryStorage(new byte[0]), OpenMode.ReadWrite);
            }

            AllocationTableStorage storage = OpenFatBlock(file.BlockIndex, file.FileSize);

            return new SaveDataFile(storage, 0, file.FileSize, mode);
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
            throw new System.NotImplementedException();
        }

        public bool FileExists(string filename) => FileDictionary.ContainsKey(filename);
        public void Commit()
        {
            throw new System.NotImplementedException();
        }

        public IStorage GetBaseStorage() => BaseStorage.WithAccess(FileAccess.Read);
        public IStorage GetHeaderStorage() => HeaderStorage.WithAccess(FileAccess.Read);

        private void ReadFileInfo()
        {
            // todo: Query the FAT for the file size when none is given
            AllocationTableStorage dirTableStream = OpenFatBlock(AllocationTable.Header.DirectoryTableBlock, 1000000);
            AllocationTableStorage fileTableStream = OpenFatBlock(AllocationTable.Header.FileTableBlock, 1000000);

            SaveDirectoryEntry[] dirEntries = ReadDirEntries(dirTableStream);
            SaveFileEntry[] fileEntries = ReadFileEntries(fileTableStream);

            foreach (SaveDirectoryEntry dir in dirEntries)
            {
                if (dir.NextSiblingIndex != 0) dir.NextSibling = dirEntries[dir.NextSiblingIndex];
                if (dir.FirstChildIndex != 0) dir.FirstChild = dirEntries[dir.FirstChildIndex];
                if (dir.FirstFileIndex != 0) dir.FirstFile = fileEntries[dir.FirstFileIndex];
                if (dir.NextInChainIndex != 0) dir.NextInChain = dirEntries[dir.NextInChainIndex];
                if (dir.ParentDirIndex != 0 && dir.ParentDirIndex < dirEntries.Length)
                    dir.ParentDir = dirEntries[dir.ParentDirIndex];
            }

            foreach (SaveFileEntry file in fileEntries)
            {
                if (file.NextSiblingIndex != 0) file.NextSibling = fileEntries[file.NextSiblingIndex];
                if (file.NextInChainIndex != 0) file.NextInChain = fileEntries[file.NextInChainIndex];
                if (file.ParentDirIndex != 0 && file.ParentDirIndex < dirEntries.Length)
                    file.ParentDir = dirEntries[file.ParentDirIndex];
            }

            RootDirectory = dirEntries[2];

            SaveFileEntry fileChain = fileEntries[1].NextInChain;
            var files = new List<SaveFileEntry>();
            while (fileChain != null)
            {
                files.Add(fileChain);
                fileChain = fileChain.NextInChain;
            }

            SaveDirectoryEntry dirChain = dirEntries[1].NextInChain;
            var dirs = new List<SaveDirectoryEntry>();
            while (dirChain != null)
            {
                dirs.Add(dirChain);
                dirChain = dirChain.NextInChain;
            }

            Files = files.ToArray();
            Directories = dirs.ToArray();

            SaveFsEntry.ResolveFilenames(Files);
            SaveFsEntry.ResolveFilenames(Directories);
        }

        private SaveFileEntry[] ReadFileEntries(IStorage storage)
        {
            var reader = new BinaryReader(storage.AsStream());
            int count = reader.ReadInt32();

            reader.BaseStream.Position -= 4;

            var entries = new SaveFileEntry[count];
            for (int i = 0; i < count; i++)
            {
                entries[i] = new SaveFileEntry(reader);
            }

            return entries;
        }

        private SaveDirectoryEntry[] ReadDirEntries(IStorage storage)
        {
            var reader = new BinaryReader(storage.AsStream());
            int count = reader.ReadInt32();

            reader.BaseStream.Position -= 4;

            var entries = new SaveDirectoryEntry[count];
            for (int i = 0; i < count; i++)
            {
                entries[i] = new SaveDirectoryEntry(reader);
            }

            return entries;
        }

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
