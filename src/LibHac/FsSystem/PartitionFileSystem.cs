﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LibHac.Common;
using LibHac.Crypto;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using Path = LibHac.Fs.Path;

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

        protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
            OpenDirectoryMode mode)
        {
            outDirectory.Reset(new PartitionDirectory(this, path.ToString(), mode));
            return Result.Success;
        }

        protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
        {
            string pathNormalized = PathTools.Normalize(path.ToString()).TrimStart('/');

            if (!FileDict.TryGetValue(pathNormalized, out PartitionFileEntry entry))
            {
                ThrowHelper.ThrowResult(ResultFs.PathNotFound.Value);
            }

            outFile.Reset(OpenFile(entry, mode));
            return Result.Success;
        }

        public IFile OpenFile(PartitionFileEntry entry, OpenMode mode)
        {
            return new PartitionFile(BaseStorage, HeaderSize + entry.Offset, entry.Size, mode);
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
        {
            UnsafeHelpers.SkipParamInit(out entryType);

            if (path.ToString() == "/")
            {
                entryType = DirectoryEntryType.Directory;
                return Result.Success;
            }

            if (FileDict.ContainsKey(path.ToString().TrimStart('/')))
            {
                entryType = DirectoryEntryType.File;
                return Result.Success;
            }

            return ResultFs.PathNotFound.Log();
        }

        protected override Result DoCreateDirectory(in Path path) => ResultFs.UnsupportedWriteForPartitionFileSystem.Log();
        protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option) => ResultFs.UnsupportedWriteForPartitionFileSystem.Log();
        protected override Result DoDeleteDirectory(in Path path) => ResultFs.UnsupportedWriteForPartitionFileSystem.Log();
        protected override Result DoDeleteDirectoryRecursively(in Path path) => ResultFs.UnsupportedWriteForPartitionFileSystem.Log();
        protected override Result DoCleanDirectoryRecursively(in Path path) => ResultFs.UnsupportedWriteForPartitionFileSystem.Log();
        protected override Result DoDeleteFile(in Path path) => ResultFs.UnsupportedWriteForPartitionFileSystem.Log();
        protected override Result DoRenameDirectory(in Path currentPath, in Path newPath) => ResultFs.UnsupportedWriteForPartitionFileSystem.Log();
        protected override Result DoRenameFile(in Path currentPath, in Path newPath) => ResultFs.UnsupportedWriteForPartitionFileSystem.Log();

        protected override Result DoCommit()
        {
            return Result.Success;
        }
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
                    ThrowHelper.ThrowResult(ResultFs.PartitionSignatureVerificationFailed.Value, $"Invalid Partition FS type \"{Magic}\"");
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
                Hash = reader.ReadBytes(Sha256.DigestSize);
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
