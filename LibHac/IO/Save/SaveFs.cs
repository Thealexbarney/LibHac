using System.Collections.Generic;
using System.IO;

namespace LibHac.IO.Save
{
    public class SaveFs
    {
        private IStorage BaseStorage { get; }
        private IStorage HeaderStorage { get; }

        public AllocationTable AllocationTable { get; }
        private SaveHeader Header { get; }

        public DirectoryEntry RootDirectory { get; private set; }
        public FileEntry[] Files { get; private set; }
        public DirectoryEntry[] Directories { get; private set; }
        public Dictionary<string, FileEntry> FileDictionary { get; }

        public SaveFs(IStorage storage, IStorage allocationTable, IStorage header)
        {
            HeaderStorage = header;
            BaseStorage = storage;
            AllocationTable = new AllocationTable(allocationTable, header.Slice(0x18, 0x30));

            Header = new SaveHeader(HeaderStorage);

            ReadFileInfo();
            var dictionary = new Dictionary<string, FileEntry>();
            foreach (FileEntry entry in Files)
            {
                dictionary[entry.FullPath] = entry;
            }

            FileDictionary = dictionary;
        }

        public IStorage OpenFile(string filename)
        {
            if (!FileDictionary.TryGetValue(filename, out FileEntry file))
            {
                throw new FileNotFoundException();
            }

            return OpenFile(file);
        }

        public IStorage OpenFile(FileEntry file)
        {
            if (file.BlockIndex < 0)
            {
                // todo
                return new MemoryStorage(new byte[0]);
            }

            return OpenFatBlock(file.BlockIndex, file.FileSize);
        }

        public bool FileExists(string filename) => FileDictionary.ContainsKey(filename);

        public IStorage GetBaseStorage() => BaseStorage.WithAccess(FileAccess.Read);
        public IStorage GetHeaderStorage() => HeaderStorage.WithAccess(FileAccess.Read);

        private void ReadFileInfo()
        {
            // todo: Query the FAT for the file size when none is given
            AllocationTableStorage dirTableStream = OpenFatBlock(AllocationTable.Header.DirectoryTableBlock, 1000000);
            AllocationTableStorage fileTableStream = OpenFatBlock(AllocationTable.Header.FileTableBlock, 1000000);

            DirectoryEntry[] dirEntries = ReadDirEntries(dirTableStream);
            FileEntry[] fileEntries = ReadFileEntries(fileTableStream);

            foreach (DirectoryEntry dir in dirEntries)
            {
                if (dir.NextSiblingIndex != 0) dir.NextSibling = dirEntries[dir.NextSiblingIndex];
                if (dir.FirstChildIndex != 0) dir.FirstChild = dirEntries[dir.FirstChildIndex];
                if (dir.FirstFileIndex != 0) dir.FirstFile = fileEntries[dir.FirstFileIndex];
                if (dir.NextInChainIndex != 0) dir.NextInChain = dirEntries[dir.NextInChainIndex];
                if (dir.ParentDirIndex != 0 && dir.ParentDirIndex < dirEntries.Length)
                    dir.ParentDir = dirEntries[dir.ParentDirIndex];
            }

            foreach (FileEntry file in fileEntries)
            {
                if (file.NextSiblingIndex != 0) file.NextSibling = fileEntries[file.NextSiblingIndex];
                if (file.NextInChainIndex != 0) file.NextInChain = fileEntries[file.NextInChainIndex];
                if (file.ParentDirIndex != 0 && file.ParentDirIndex < dirEntries.Length)
                    file.ParentDir = dirEntries[file.ParentDirIndex];
            }

            RootDirectory = dirEntries[2];

            FileEntry fileChain = fileEntries[1].NextInChain;
            var files = new List<FileEntry>();
            while (fileChain != null)
            {
                files.Add(fileChain);
                fileChain = fileChain.NextInChain;
            }

            DirectoryEntry dirChain = dirEntries[1].NextInChain;
            var dirs = new List<DirectoryEntry>();
            while (dirChain != null)
            {
                dirs.Add(dirChain);
                dirChain = dirChain.NextInChain;
            }

            Files = files.ToArray();
            Directories = dirs.ToArray();

            FsEntry.ResolveFilenames(Files);
            FsEntry.ResolveFilenames(Directories);
        }

        private FileEntry[] ReadFileEntries(IStorage storage)
        {
            var reader = new BinaryReader(storage.AsStream());
            int count = reader.ReadInt32();

            reader.BaseStream.Position -= 4;

            var entries = new FileEntry[count];
            for (int i = 0; i < count; i++)
            {
                entries[i] = new FileEntry(reader);
            }

            return entries;
        }

        private DirectoryEntry[] ReadDirEntries(IStorage storage)
        {
            var reader = new BinaryReader(storage.AsStream());
            int count = reader.ReadInt32();

            reader.BaseStream.Position -= 4;

            var entries = new DirectoryEntry[count];
            for (int i = 0; i < count; i++)
            {
                entries[i] = new DirectoryEntry(reader);
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
