﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using LibHac.Common;
using LibHac.Crypto;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem.Impl;
using LibHac.Tools.Fs;
using LibHac.Util;

namespace LibHac.Tools.FsSystem;

public enum PartitionFileSystemType
{
    Standard,
    Hashed
}

public class PartitionFileSystemBuilder
{
    private const int HeaderSize = 0x10;
    private const int DefaultHashTargetSize = 0x200;

    private List<Entry> Entries { get; } = new List<Entry>();
    private long CurrentOffset { get; set; }

    public PartitionFileSystemBuilder() { }

    /// <summary>
    /// Creates a new <see cref="PartitionFileSystemBuilder"/> and populates it with all
    /// the files in the root directory.
    /// </summary>
    public PartitionFileSystemBuilder(IFileSystem input)
    {
        using var file = new UniqueRef<IFile>();

        foreach (DirectoryEntryEx entry in input.EnumerateEntries().Where(x => x.Type == DirectoryEntryType.File)
            .OrderBy(x => x.FullPath, StringComparer.Ordinal))
        {
            input.OpenFile(ref file.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

            AddFile(entry.FullPath.TrimStart('/'), file.Release());
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
            HashLength = (int)Math.Min(DefaultHashTargetSize, fileSize)
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

        int entryTableSize = Entries.Count * GetEntrySize(type);
        int stringTableSize = CalcStringTableSize(HeaderSize + entryTableSize, type);
        int metaDataSize = HeaderSize + entryTableSize + stringTableSize;

        byte[] metaData = new byte[metaDataSize];
        var writer = new BinaryWriter(new MemoryStream(metaData));

        writer.WriteUtf8(GetMagicValue(type));
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
            writer.WriteUtf8Z(entry.Name);
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

        int endOffset = Alignment.AlignUp(startOffset + size, GetMetaDataAlignment(type));
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

    private uint GetMetaDataAlignment(PartitionFileSystemType type)
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
        IHash sha = Sha256.CreateSha256Generator();

        foreach (Entry entry in Entries)
        {
            if (entry.HashLength == 0)
            {
                entry.HashLength = (int)Math.Min(DefaultHashTargetSize, entry.Length);
            }

            byte[] data = new byte[entry.HashLength];
            entry.File.Read(out long bytesRead, entry.HashOffset, data);

            if (bytesRead != entry.HashLength)
            {
                throw new ArgumentOutOfRangeException();
            }

            entry.Hash = new byte[Sha256.DigestSize];

            sha.Initialize();
            sha.Update(data);
            sha.GetHash(entry.Hash);
        }
    }

    public static int GetEntrySize(PartitionFileSystemType type)
    {
        switch (type)
        {
            case PartitionFileSystemType.Standard:
                return Unsafe.SizeOf<PartitionFileSystemFormat.PartitionEntry>();
            case PartitionFileSystemType.Hashed:
                return Unsafe.SizeOf<Sha256PartitionFileSystemFormat.PartitionEntry>();
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
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