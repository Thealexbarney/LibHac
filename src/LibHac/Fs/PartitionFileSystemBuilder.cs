using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace LibHac.Fs
{
    public class PartitionFileSystemBuilder
    {
        private const int HeaderSize = 0x10;

        private List<Entry> Entries { get; } = new List<Entry>();
        private long CurrentOffset { get; set; }

        public PartitionFileSystemBuilder() { }

        /// <summary>
        /// Creates a new <see cref="PartitionFileSystemBuilder"/> and populates it with all
        /// the files in the root directory.
        /// </summary>
        public PartitionFileSystemBuilder(IFileSystem input)
        {
            IDirectory rootDir = input.OpenDirectory("/", OpenDirectoryMode.File);

            foreach (DirectoryEntry file in rootDir.Read().OrderBy(x => x.FullPath, StringComparer.Ordinal))
            {
                AddFile(file.FullPath.TrimStart('/'), input.OpenFile(file.FullPath, OpenMode.Read));
            }
        }

        public void AddFile(string filename, IFile file)
        {
            file.GetSize(out long fileSize).ThrowIfFailure();

            var entry = new Entry
            {
                Name = filename,
                File = file,
                Length = fileSize,
                Offset = CurrentOffset,
                NameLength = Encoding.UTF8.GetByteCount(filename),
                HashOffset = 0,
                HashLength = 0x200
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
            if (type == PartitionFileSystemType.Hashed) CalculateHashes();

            int entryTableSize = Entries.Count * PartitionFileEntry.GetEntrySize(type);
            int stringTableSize = CalcStringTableSize(HeaderSize + entryTableSize, type);
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

                if (type == PartitionFileSystemType.Standard)
                {
                    writer.Write(0);
                }
                else
                {
                    writer.Write(entry.HashLength);
                    writer.Write(entry.HashOffset);
                    writer.Write(entry.Hash);
                }

                stringOffset += entry.NameLength + 1;
            }

            foreach (Entry entry in Entries)
            {
                writer.WriteUTF8Z(entry.Name);
            }

            return metaData;
        }

        private int CalcStringTableSize(int startOffset, PartitionFileSystemType type)
        {
            int size = 0;

            foreach (Entry entry in Entries)
            {
                size += entry.NameLength + 1;
            }

            int endOffset = Util.AlignUp(startOffset + size, GetMetaDataAlignment(type));
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

        private int GetMetaDataAlignment(PartitionFileSystemType type)
        {
            switch (type)
            {
                case PartitionFileSystemType.Standard: return 0x20;
                case PartitionFileSystemType.Hashed: return 0x200;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private void CalculateHashes()
        {
            using (SHA256 sha = SHA256.Create())
            {
                foreach (Entry entry in Entries)
                {
                    if (entry.HashLength == 0) entry.HashLength = 0x200;

                    var data = new byte[entry.HashLength];
                    entry.File.Read(out long bytesRead, entry.HashOffset, data);

                    if (bytesRead != entry.HashLength)
                    {
                        throw new ArgumentOutOfRangeException();
                    }

                    entry.Hash = sha.ComputeHash(data);
                }
            }
        }

        private class Entry
        {
            public string Name;
            public IFile File;
            public long Length;
            public long Offset;
            public int NameLength;

            public int HashLength;
            public long HashOffset;
            public byte[] Hash;
        }
    }
}
