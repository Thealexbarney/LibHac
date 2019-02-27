using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LibHac.IO
{
    public class PartitionFileSystemBuilder
    {
        private const int HeaderSize = 0x10;
        private const int MetaDataAlignment = 0x20;

        private List<Entry> Entries { get; } = new List<Entry>();
        private long CurrentOffset { get; set; }

        public PartitionFileSystemBuilder() { }

        /// <summary>
        /// Creates a new <see cref="PartitionFileSystemBuilder"/> and populates it with all
        /// the files in the root directory.
        /// </summary>
        public PartitionFileSystemBuilder(IFileSystem input)
        {
            IDirectory rootDir = input.OpenDirectory("/", OpenDirectoryMode.Files);

            foreach (DirectoryEntry file in rootDir.Read().OrderBy(x => x.FullPath, StringComparer.Ordinal))
            {
                AddFile(file.FullPath.TrimStart('/'), input.OpenFile(file.FullPath, OpenMode.Read));
            }
        }

        public void AddFile(string filename, IFile file)
        {
            var entry = new Entry
            {
                Name = filename,
                File = file,
                Length = file.GetSize(),
                Offset = CurrentOffset,
                NameLength = Encoding.UTF8.GetByteCount(filename)
            };

            CurrentOffset += entry.Length;

            Entries.Add(entry);
        }

        public IStorage Build(PartitionFileSystemType type)
        {
            byte[] meta = BuildMetaData(type);

            var sources = new List<IStorage>();
            sources.Add(new MemoryStorage(meta));

            sources.AddRange(Entries.Select(x => new FileStorage(x.File)));

            return new ConcatenationStorage(sources, true);
        }

        private byte[] BuildMetaData(PartitionFileSystemType type)
        {
            int entryTableSize = Entries.Count * PartitionFileEntry.GetEntrySize(type);
            int stringTableSize = CalcStringTableSize(HeaderSize + entryTableSize);
            int metaDataSize = HeaderSize + entryTableSize + stringTableSize;

            var metaData = new byte[metaDataSize];
            var writer = new BinaryWriter(new MemoryStream(metaData));

            writer.WriteUTF8(GetMagicValue(type));
            writer.Write(Entries.Count);
            writer.Write(stringTableSize);
            writer.Write(0);

            int stringOffset = 0;

            foreach (Entry entry in Entries)
            {
                writer.Write(entry.Offset);
                writer.Write(entry.Length);
                writer.Write(stringOffset);
                writer.Write(0);

                stringOffset += entry.NameLength + 1;
            }

            foreach (Entry entry in Entries)
            {
                writer.WriteUTF8Z(entry.Name);
            }

            return metaData;
        }

        private int CalcStringTableSize(int startOffset)
        {
            int size = 0;

            foreach (Entry entry in Entries)
            {
                size += entry.NameLength + 1;
            }

            int endOffset = Util.AlignUp(startOffset + size, MetaDataAlignment);
            return endOffset - startOffset;
        }

        private string GetMagicValue(PartitionFileSystemType type)
        {
            switch (type)
            {
                case PartitionFileSystemType.Standard: return "PFS0";
                case PartitionFileSystemType.Hashed: return "HFS0";
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private class Entry
        {
            public string Name;
            public IFile File;
            public long Length;
            public long Offset;
            public int NameLength;
        }
    }
}
