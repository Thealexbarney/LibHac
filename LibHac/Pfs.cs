using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LibHac.Streams;

namespace LibHac
{
    public class Pfs
    {
        public PfsHeader Header { get; }
        public int HeaderSize { get; }
        public PfsFileEntry[] Files { get; }

        private Dictionary<string, PfsFileEntry> FileDict { get; }
        private SharedStreamSource StreamSource { get; }

        public Pfs(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.Default, true))
            {
                Header = new PfsHeader(reader);
            }

            HeaderSize = Header.HeaderSize;
            Files = Header.Files;
            FileDict = Header.Files.ToDictionary(x => x.Name, x => x);
            StreamSource = new SharedStreamSource(stream);
        }

        public Stream OpenFile(string filename)
        {
            if (!FileDict.TryGetValue(filename, out PfsFileEntry file))
            {
                throw new FileNotFoundException();
            }

            return OpenFile(file);
        }

        public bool TryOpenFile(string filename, out Stream stream)
        {
            if (!FileDict.TryGetValue(filename, out PfsFileEntry file))
            {
                stream = null;
                return false;
            }

            stream = OpenFile(file);
            return true;
        }

        public Stream OpenFile(PfsFileEntry file)
        {
            return StreamSource.CreateStream(HeaderSize + file.Offset, file.Size);
        }

        public bool FileExists(string filename)
        {
            return FileDict.ContainsKey(filename);
        }
    }

    public enum PfsType
    {
        Pfs0,
        Hfs0
    }

    public class PfsSuperblock
    {
        public byte[] MasterHash; /* SHA-256 hash of the hash table. */
        public uint BlockSize; /* In bytes. */
        public uint Always2;
        public long HashTableOffset; /* Normally zero. */
        public long HashTableSize;
        public long Pfs0Offset;
        public long Pfs0Size;

        public PfsSuperblock(BinaryReader reader)
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

    public class PfsHeader
    {
        public string Magic;
        public int NumFiles;
        public int StringTableSize;
        public long Reserved;
        public PfsType Type;
        public int HeaderSize;
        public PfsFileEntry[] Files;

        public PfsHeader(BinaryReader reader)
        {
            Magic = reader.ReadAscii(4);
            NumFiles = reader.ReadInt32();
            StringTableSize = reader.ReadInt32();
            Reserved = reader.ReadInt32();

            switch (Magic)
            {
                case "PFS0":
                    Type = PfsType.Pfs0;
                    break;
                case "HFS0":
                    Type = PfsType.Hfs0;
                    break;
                default:
                    throw new InvalidDataException($"Invalid Partition FS type \"{Magic}\"");
            }

            int entrysize = GetFileEntrySize(Type);
            int stringTableOffset = 16 + entrysize * NumFiles;
            HeaderSize = stringTableOffset + StringTableSize;

            Files = new PfsFileEntry[NumFiles];
            for (int i = 0; i < NumFiles; i++)
            {
                Files[i] = new PfsFileEntry(reader, Type) { Index = i };
            }

            for (int i = 0; i < NumFiles; i++)
            {
                reader.BaseStream.Position = stringTableOffset + Files[i].StringTableOffset;
                Files[i].Name = reader.ReadAsciiZ();
            }
        }

        private static int GetFileEntrySize(PfsType type)
        {
            switch (type)
            {
                case PfsType.Pfs0:
                    return 24;
                case PfsType.Hfs0:
                    return 0x40;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }

    public class PfsFileEntry
    {
        public int Index;
        public long Offset;
        public long Size;
        public uint StringTableOffset;
        public long Reserved;
        public int HashedRegionSize;
        public byte[] Hash;
        public string Name;

        public PfsFileEntry(BinaryReader reader, PfsType type)
        {
            Offset = reader.ReadInt64();
            Size = reader.ReadInt64();
            StringTableOffset = reader.ReadUInt32();
            if (type == PfsType.Hfs0)
            {
                HashedRegionSize = reader.ReadInt32();
                Reserved = reader.ReadInt64();
                Hash = reader.ReadBytes(Crypto.Sha256DigestSize);
            }
            else
            {
                Reserved = reader.ReadUInt32();
            }
        }
    }

    public static class PfsExtensions
    {
        public static void Extract(this Pfs pfs, string outDir, IProgressReport logger = null)
        {
            foreach (var file in pfs.Header.Files)
            {
                var stream = pfs.OpenFile(file);
                var outName = Path.Combine(outDir, file.Name);
                var dir = Path.GetDirectoryName(outName);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

                using (var outFile = new FileStream(outName, FileMode.Create, FileAccess.ReadWrite))
                {
                    logger?.LogMessage(file.Name);
                    stream.CopyStream(outFile, stream.Length, logger);
                }
            }
        }
    }
}
