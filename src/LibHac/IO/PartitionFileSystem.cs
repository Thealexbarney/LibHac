using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LibHac.IO
{
    public class PartitionFileSystem : IFileSystem
    {
        // todo Re-add way of checking a file hash
        public PartitionFileSystemHeader Header { get; }
        public int HeaderSize { get; }
        public PartitionFileEntry[] Files { get; }

        private Dictionary<string, PartitionFileEntry> FileDict { get; }
        private IStorage BaseStorage { get; }

        public PartitionFileSystem(IStorage storage)
        {
            using (var reader = new BinaryReader(storage.AsStream(), Encoding.Default, true))
            {
                Header = new PartitionFileSystemHeader(reader);
            }

            HeaderSize = Header.HeaderSize;
            Files = Header.Files;
            FileDict = Header.Files.ToDictionary(x => x.Name, x => x);
            BaseStorage = storage;
        }

        public IDirectory OpenDirectory(string path, OpenDirectoryMode mode)
        {
            return new PartitionDirectory(this, path, mode);
        }

        public IFile OpenFile(string path, OpenMode mode)
        {
            path = PathTools.Normalize(path).TrimStart('/');

            if (!FileDict.TryGetValue(path, out PartitionFileEntry entry))
            {
                throw new FileNotFoundException();
            }

            return OpenFile(entry, mode);
        }

        public IFile OpenFile(PartitionFileEntry entry, OpenMode mode)
        {
            return new PartitionFile(BaseStorage, HeaderSize + entry.Offset, entry.Size, mode);
        }

        public bool DirectoryExists(string path)
        {
            path = PathTools.Normalize(path);
            return path == "/";
        }

        public bool FileExists(string path)
        {
            path = PathTools.Normalize(path).TrimStart('/');

            return FileDict.ContainsKey(path);
        }

        public DirectoryEntryType GetEntryType(string path)
        {
            path = PathTools.Normalize(path);

            if (path == "/") return DirectoryEntryType.Directory;

            if (FileDict.ContainsKey(path)) return DirectoryEntryType.File;

            throw new FileNotFoundException(path);
        }

        public void CreateDirectory(string path) => throw new NotSupportedException();
        public void CreateFile(string path, long size, CreateFileOptions options) => throw new NotSupportedException();
        public void DeleteDirectory(string path) => throw new NotSupportedException();
        public void DeleteFile(string path) => throw new NotSupportedException();
        public void RenameDirectory(string srcPath, string dstPath) => throw new NotSupportedException();
        public void RenameFile(string srcPath, string dstPath) => throw new NotSupportedException();
        public long GetFreeSpaceSize(string path) => throw new NotSupportedException();
        public long GetTotalSpaceSize(string path) => throw new NotSupportedException();
        public FileTimeStampRaw GetFileTimeStampRaw(string path) => throw new NotSupportedException();
        public void Commit() { }
        public void QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, string path, QueryId queryId) => throw new NotSupportedException();
    }

    public enum PartitionFileSystemType
    {
        Standard,
        Hashed
    }

    public class PartitionFileSystemHeader
    {
        public string Magic;
        public int NumFiles;
        public int StringTableSize;
        public long Reserved;
        public PartitionFileSystemType Type;
        public int HeaderSize;
        public PartitionFileEntry[] Files;

        public PartitionFileSystemHeader(BinaryReader reader)
        {
            Magic = reader.ReadAscii(4);
            NumFiles = reader.ReadInt32();
            StringTableSize = reader.ReadInt32();
            Reserved = reader.ReadInt32();

            switch (Magic)
            {
                case "PFS0":
                    Type = PartitionFileSystemType.Standard;
                    break;
                case "HFS0":
                    Type = PartitionFileSystemType.Hashed;
                    break;
                default:
                    throw new InvalidDataException($"Invalid Partition FS type \"{Magic}\"");
            }

            int entrySize = PartitionFileEntry.GetEntrySize(Type);
            int stringTableOffset = 16 + entrySize * NumFiles;
            HeaderSize = stringTableOffset + StringTableSize;

            Files = new PartitionFileEntry[NumFiles];
            for (int i = 0; i < NumFiles; i++)
            {
                Files[i] = new PartitionFileEntry(reader, Type) { Index = i };
            }

            for (int i = 0; i < NumFiles; i++)
            {
                reader.BaseStream.Position = stringTableOffset + Files[i].StringTableOffset;
                Files[i].Name = reader.ReadAsciiZ();
            }
        }
    }

    public class PartitionFileEntry
    {
        public int Index;
        public long Offset;
        public long Size;
        public uint StringTableOffset;
        public long HashedRegionOffset;
        public int HashedRegionSize;
        public byte[] Hash;
        public string Name;
        public Validity HashValidity = Validity.Unchecked;

        public PartitionFileEntry(BinaryReader reader, PartitionFileSystemType type)
        {
            Offset = reader.ReadInt64();
            Size = reader.ReadInt64();
            StringTableOffset = reader.ReadUInt32();
            if (type == PartitionFileSystemType.Hashed)
            {
                HashedRegionSize = reader.ReadInt32();
                HashedRegionOffset = reader.ReadInt64();
                Hash = reader.ReadBytes(Crypto.Sha256DigestSize);
            }
            else
            {
                reader.BaseStream.Position += 4;
            }
        }

        public static int GetEntrySize(PartitionFileSystemType type)
        {
            switch (type)
            {
                case PartitionFileSystemType.Standard:
                    return 0x18;
                case PartitionFileSystemType.Hashed:
                    return 0x40;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}
