// ReSharper disable UnusedMember.Local UnassignedField.Local
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using System;
using System.Runtime.InteropServices;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs;

using Position = uint;
using RomDirectoryId = uint;
using RomFileId = uint;

public class HierarchicalRomFileTable : IDisposable
{
    private const Position InvalidPosition = ~default(Position);

    public struct FindPosition
    {
        public Position NextDirectory;
        public Position NextFile;
    }

    public struct CacheContext
    {
        public Position ParentFirstFilePosition;
        public Position ParentLastFilePosition;

        public CacheContext()
        {
            ParentFirstFilePosition = InvalidPosition;
        }
    }

    private ref struct EntryKey
    {
        public RomEntryKey Key;
        public RomPathTool.RomEntryName Name;

        public EntryKey()
        {
            Name = new RomPathTool.RomEntryName();
        }

        public readonly uint Hash()
        {
            uint hash = 123456789 ^ Key.Parent;

            foreach (byte c in Name.GetPath())
            {
                hash = c ^ ((hash << 27) | (hash >> 5));
            }

            return hash;
        }
    }

    private struct RomEntryKey
    {
        public Position Parent;

        public readonly bool IsEqual(in RomEntryKey rhs, ReadOnlySpan<byte> lhsExtraKey, ReadOnlySpan<byte> rhsExtraKey)
        {
            if (Parent != rhs.Parent)
                return false;

            if (lhsExtraKey.Length != rhsExtraKey.Length)
                return false;

            return RomPathTool.IsEqualPath(lhsExtraKey, rhsExtraKey, lhsExtraKey.Length);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DirectoryRomEntry
    {
        public Position Next;
        public Position Dir;
        public Position File;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileRomEntry
    {
        public Position Next;
        public RomFileInfo Info;
    }

    // Todo: Add generic types for the key types once generics can use ref structs
    private class EntryMapTable<TValue> : KeyValueRomStorageTemplate<RomEntryKey, TValue> where TValue : unmanaged
    {
        public Result Add(out Position outPosition, in EntryKey key, in TValue value)
        {
            return AddInternal(out outPosition, in key.Key, key.Hash(), key.Name.GetPath(), in value).Ret();
        }

        public Result Get(out Position outPosition, out TValue outValue, in EntryKey key)
        {
            return GetInternal(out outPosition, out outValue, in key.Key, key.Hash(), key.Name.GetPath()).Ret();
        }

        public new Result GetByPosition(out RomEntryKey outKey, out TValue outValue, Position position)
        {
            return base.GetByPosition(out outKey, out outValue, position).Ret();
        }

        public new Result GetByPosition(out RomEntryKey outKey, out TValue outValue, Span<byte> outExtraKey,
            out int outExtraKeySize, Position position)
        {
            return base.GetByPosition(out outKey, out outValue, outExtraKey, out outExtraKeySize, position).Ret();
        }

        public new Result SetByPosition(Position position, in TValue value)
        {
            return base.SetByPosition(position, in value).Ret();
        }
    }

    public HierarchicalRomFileTable()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public static long QueryDirectoryEntryBucketStorageSize(uint bucketCount)
    {
        return EntryMapTable<DirectoryRomEntry>.QueryBucketCount(bucketCount);
    }

    public static long QueryDirectoryEntrySize(uint extraKeySize)
    {
        return EntryMapTable<DirectoryRomEntry>.QueryEntrySize(extraKeySize);
    }

    public static long QueryFileEntryBucketStorageSize(uint bucketCount)
    {
        return EntryMapTable<FileRomEntry>.QueryBucketCount(bucketCount);
    }

    public static long QueryFileEntrySize(uint extraKeySize)
    {
        return EntryMapTable<FileRomEntry>.QueryEntrySize(extraKeySize);
    }

    public static Result Format(ref readonly ValueSubStorage directoryBucketStorage,
        ref readonly ValueSubStorage fileBucketStorage)
    {
        throw new NotImplementedException();
    }

    public Result Initialize(ref readonly ValueSubStorage directoryBucketStorage,
        ref readonly ValueSubStorage directoryEntryStorage, ref readonly ValueSubStorage fileBucketStorage,
        ref readonly ValueSubStorage fileEntryStorage)
    {
        throw new NotImplementedException();
    }

    public void FinalizeObject()
    {
        throw new NotImplementedException();
    }

    public Result CreateRootDirectory()
    {
        throw new NotImplementedException();
    }

    public Result CreateDirectory(out RomDirectoryId outId, ReadOnlySpan<byte> fullPath)
    {
        throw new NotImplementedException();
    }

    public Result CreateFile(out RomFileId outId, ReadOnlySpan<byte> fullPath, in RomFileInfo info)
    {
        throw new NotImplementedException();
    }

    public Result CreateFile(out RomFileId outId, ReadOnlySpan<byte> fullPath, in RomFileInfo info,
        ref CacheContext cacheContext)
    {
        throw new NotImplementedException();
    }

    public Result ConvertPathToDirectoryId(out RomDirectoryId outId, ReadOnlySpan<byte> fullPath)
    {
        throw new NotImplementedException();
    }

    public Result ConvertPathToFileId(out RomFileId outId, ReadOnlySpan<byte> fullPath)
    {
        throw new NotImplementedException();
    }

    public Result OpenFile(out RomFileInfo outInfo, ReadOnlySpan<byte> fullPath)
    {
        throw new NotImplementedException();
    }

    public Result OpenFile(out RomFileInfo outInfo, RomFileId id)
    {
        throw new NotImplementedException();
    }

    public Result FindOpen(out FindPosition outPosition, ReadOnlySpan<byte> fullPath)
    {
        throw new NotImplementedException();
    }

    public Result FindOpen(out FindPosition outPosition, RomDirectoryId id)
    {
        throw new NotImplementedException();
    }

    public Result FindNextDirectory(Span<byte> outName, ref FindPosition findPosition, int length)
    {
        throw new NotImplementedException();
    }

    public Result FindNextFile(Span<byte> outName, ref FindPosition findPosition, int length)
    {
        throw new NotImplementedException();
    }

    public Result QueryRomFileSystemSize(out long outDirectoryEntrySize, out long outFileEntrySize)
    {
        throw new NotImplementedException();
    }

    private Result GetParent(out Position outParentPosition, ref EntryKey outDirectoryKey,
        ref DirectoryRomEntry outDirectoryEntry, Position position, ref RomPathTool.RomEntryName name,
        ReadOnlySpan<byte> fullPath)
    {
        throw new NotImplementedException();
    }

    private Result FindParentDirectoryRecursive(out Position outParentPosition, ref EntryKey outDirectoryKey,
        ref DirectoryRomEntry outDirectoryEntry, ref RomPathTool.PathParser parser, ReadOnlySpan<byte> fullPath)
    {
        throw new NotImplementedException();
    }

    private Result FindPathRecursive(ref EntryKey outKey, ref DirectoryRomEntry outParentDirectoryEntry,
        bool isDirectory, ReadOnlySpan<byte> fullPath)
    {
        throw new NotImplementedException();
    }

    private Result FindDirectoryRecursive(ref EntryKey outKey, ref DirectoryRomEntry outParentDirectoryEntry,
        ReadOnlySpan<byte> fullPath)
    {
        throw new NotImplementedException();
    }

    private Result FindFileRecursive(ref EntryKey outKey, ref DirectoryRomEntry outParentDirectoryEntry,
        ReadOnlySpan<byte> fullPath)
    {
        throw new NotImplementedException();
    }

    private Result CheckSameEntryExists(in EntryKey key, Result resultIfExists)
    {
        throw new NotImplementedException();
    }

    private Result GetDirectoryEntry(out Position outPosition, out DirectoryRomEntry outEntry, in EntryKey key)
    {
        throw new NotImplementedException();
    }

    private Result GetDirectoryEntry(out DirectoryRomEntry outEntry, RomDirectoryId id)
    {
        throw new NotImplementedException();
    }

    private Result GetFileEntry(out Position outPosition, out FileRomEntry outEntry, in EntryKey key)
    {
        throw new NotImplementedException();
    }

    private Result GetFileEntry(out FileRomEntry outEntry, RomFileId id)
    {
        throw new NotImplementedException();
    }

    private Result OpenFile(out RomFileInfo outFileInfo, in EntryKey key)
    {
        throw new NotImplementedException();
    }

    private Result FindOpen(out FindPosition outPosition, in EntryKey key)
    {
        throw new NotImplementedException();
    }
}