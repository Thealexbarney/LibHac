using System.Collections.Generic;
using System.IO;
using LibHac.Streams;

namespace LibHac.Save
{
    public class SaveFs
    {
        private AllocationTable AllocationTable { get; }
        private SharedStreamSource StreamSource { get; }
        private SaveHeader Header { get; }

        public DirectoryEntry RootDirectory { get; private set; }
        public FileEntry[] Files { get; private set; }
        public DirectoryEntry[] Directories { get; private set; }
        public Dictionary<string, FileEntry> FileDictionary { get; }

        public SaveFs(Stream storage, Stream allocationTable, SaveHeader header)
        {
            StreamSource = new SharedStreamSource(storage);
            AllocationTable = new AllocationTable(allocationTable);
            Header = header;

            ReadFileInfo();
            var dictionary = new Dictionary<string, FileEntry>();
            foreach (FileEntry entry in Files)
            {
                dictionary[entry.FullPath] = entry;
            }

            FileDictionary = dictionary;
        }

        public Stream OpenFile(string filename)
        {
            if (!FileDictionary.TryGetValue(filename, out FileEntry file))
            {
                throw new FileNotFoundException();
            }

            return OpenFile(file);
        }

        public Stream OpenFile(FileEntry file)
        {
            if (file.BlockIndex < 0)
            {
                return Stream.Null;
            }

            return OpenFatBlock(file.BlockIndex, file.FileSize);
        }

        public bool FileExists(string filename) => FileDictionary.ContainsKey(filename);

        public Stream OpenRawSaveFs() => StreamSource.CreateStream();

        private void ReadFileInfo()
        {
            // todo: Query the FAT for the file size when none is given
            AllocationTableStream dirTableStream = OpenFatBlock(Header.DirectoryTableBlock, 1000000);
            AllocationTableStream fileTableStream = OpenFatBlock(Header.FileTableBlock, 1000000);

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

        private FileEntry[] ReadFileEntries(Stream stream)
        {
            var reader = new BinaryReader(stream);
            int count = reader.ReadInt32();

            reader.BaseStream.Position -= 4;

            var entries = new FileEntry[count];
            for (int i = 0; i < count; i++)
            {
                entries[i] = new FileEntry(reader);
            }

            return entries;
        }

        private DirectoryEntry[] ReadDirEntries(Stream stream)
        {
            var reader = new BinaryReader(stream);
            int count = reader.ReadInt32();

            reader.BaseStream.Position -= 4;

            var entries = new DirectoryEntry[count];
            for (int i = 0; i < count; i++)
            {
                entries[i] = new DirectoryEntry(reader);
            }

            return entries;
        }

        private AllocationTableStream OpenFatBlock(int blockIndex, long size)
        {
            return new AllocationTableStream(StreamSource.CreateStream(), AllocationTable, (int)Header.BlockSize, blockIndex, size);
        }
    }
}
