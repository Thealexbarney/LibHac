using System.IO;
using System.Text;

namespace libhac
{
    public class Pfs0
    {
        public Pfs0Header Header { get; set; }
        public int HeaderSize { get; set; }
        public Pfs0FileEntry[] Entries { get; set; }
        private Stream Stream { get; set; }

        public Pfs0(Stream stream)
        {
            byte[] headerBytes;
            using (var reader = new BinaryReader(stream, Encoding.Default, true))
            {
                Header = new Pfs0Header(reader);
                HeaderSize = (int)(16 + 24 * Header.NumFiles + Header.StringTableSize);
                stream.Position = 0;
                headerBytes = reader.ReadBytes(HeaderSize);
            }

            using (var reader = new BinaryReader(new MemoryStream(headerBytes)))
            {
                reader.BaseStream.Position = 16;

                Entries = new Pfs0FileEntry[Header.NumFiles];
                for (int i = 0; i < Header.NumFiles; i++)
                {
                    Entries[i] = new Pfs0FileEntry(reader) { Index = i };
                }

                int stringTableOffset = 16 + 24 * Header.NumFiles;
                for (int i = 0; i < Header.NumFiles; i++)
                {
                    reader.BaseStream.Position = stringTableOffset + Entries[i].StringTableOffset;
                    Entries[i].Name = reader.ReadAsciiZ();
                }
            }

            Stream = stream;
        }

        public byte[] GetFile(int index)
        {
            var entry = Entries[index];
            var file = new byte[entry.Size];
            Stream.Position = HeaderSize + entry.Offset;
            Stream.Read(file, 0, file.Length);
            return file;
        }
    }

    public class Pfs0Superblock
    {
        public byte[] MasterHash; /* SHA-256 hash of the hash table. */
        public uint BlockSize; /* In bytes. */
        public uint Always2;
        public long HashTableOffset; /* Normally zero. */
        public long HashTableSize;
        public long Pfs0Offset;
        public long Pfs0Size;

        public Pfs0Superblock(BinaryReader reader)
        {
            MasterHash = reader.ReadBytes(0x20);
            BlockSize = reader.ReadUInt32();
            Always2 = reader.ReadUInt32();
            HashTableOffset = reader.ReadInt64();
            HashTableSize = reader.ReadInt64();
            Pfs0Offset = reader.ReadInt64();
            Pfs0Size = reader.ReadInt64();
            reader.BaseStream.Position += 0xF0;
        }
    }

    public class Pfs0Header
    {
        public string Magic;
        public int NumFiles;
        public uint StringTableSize;
        public long Reserved;

        public Pfs0Header(BinaryReader reader)
        {
            Magic = reader.ReadAscii(4);
            NumFiles = reader.ReadInt32();
            StringTableSize = reader.ReadUInt32();
            Reserved = reader.ReadInt32();
        }
    }

    public class Pfs0FileEntry
    {
        public int Index;
        public long Offset;
        public long Size;
        public uint StringTableOffset;
        public uint Reserved;
        public string Name;

        public Pfs0FileEntry(BinaryReader reader)
        {
            Offset = reader.ReadInt64();
            Size = reader.ReadInt64();
            StringTableOffset = reader.ReadUInt32();
            Reserved = reader.ReadUInt32();
        }
    }
}
