using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LibHac.FsSystem
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

        public Result OpenDirectory(out IDirectory directory, string path, OpenDirectoryMode mode)
        {
            directory = new PartitionDirectory(this, path, mode);
            return Result.Success;
        }

        public Result OpenFile(out IFile file, string path, OpenMode mode)
        {
            path = PathTools.Normalize(path).TrimStart('/');

            if (!FileDict.TryGetValue(path, out PartitionFileEntry entry))
            {
                ThrowHelper.ThrowResult(ResultFs.PathNotFound);
            }

            file = OpenFile(entry, mode);
            return Result.Success;
        }

        public IFile OpenFile(PartitionFileEntry entry, OpenMode mode)
        {
            return new PartitionFile(BaseStorage, HeaderSize + entry.Offset, entry.Size, mode);
        }

        public Result GetEntryType(out DirectoryEntryType entryType, string path)
        {
            entryType = DirectoryEntryType.NotFound;
            path = PathTools.Normalize(path);

            if (path == "/")
            {
                entryType = DirectoryEntryType.Directory;
                return Result.Success;
            }

            if (FileDict.ContainsKey(path.TrimStart('/')))
            {
                entryType = DirectoryEntryType.File;
                return Result.Success;
            }

            return ResultFs.PathNotFound.Log();
        }

        public Result CreateDirectory(string path) => ResultFs.UnsupportedOperationModifyPartitionFileSystem.Log();
        public Result CreateFile(string path, long size, CreateFileOptions options) => ResultFs.UnsupportedOperationModifyPartitionFileSystem.Log();
        public Result DeleteDirectory(string path) => ResultFs.UnsupportedOperationModifyPartitionFileSystem.Log();
        public Result DeleteDirectoryRecursively(string path) => ResultFs.UnsupportedOperationModifyPartitionFileSystem.Log();
        public Result CleanDirectoryRecursively(string path) => ResultFs.UnsupportedOperationModifyPartitionFileSystem.Log();
        public Result DeleteFile(string path) => ResultFs.UnsupportedOperationModifyPartitionFileSystem.Log();
        public Result RenameDirectory(string oldPath, string newPath) => ResultFs.UnsupportedOperationModifyPartitionFileSystem.Log();
        public Result RenameFile(string oldPath, string newPath) => ResultFs.UnsupportedOperationModifyPartitionFileSystem.Log();

        public Result GetFreeSpaceSize(out long freeSpace, string path)
        {
            freeSpace = default;
            return ResultFs.NotImplemented.Log();
        }

        public Result GetTotalSpaceSize(out long totalSpace, string path)
        {
            totalSpace = default;
            return ResultFs.NotImplemented.Log();
        }

        public Result GetFileTimeStampRaw(out FileTimeStampRaw timeStamp, string path)
        {
            timeStamp = default;
            return ResultFs.NotImplemented.Log();
        }

        public Result Commit()
        {
            return Result.Success;
        }

        public Result QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, string path) => ResultFs.NotImplemented.Log();
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
                    ThrowHelper.ThrowResult(ResultFs.InvalidPartitionFileSystemMagic, $"Invalid Partition FS type \"{Magic}\"");
                    break;
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
