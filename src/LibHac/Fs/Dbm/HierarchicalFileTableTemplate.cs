// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using IndexType = uint;

namespace LibHac.Fs.Dbm;

public class HierarchicalFileTableTemplate<TDirName, TFileName, TDirInfo, TFileInfo>
    where TDirName : unmanaged
    where TFileName : unmanaged
    where TDirInfo : unmanaged
    where TFileInfo : unmanaged
{
    protected const IndexType IndexNone = 0;
    private const int AllocationTableStorageReservedCount = 2;

    public struct ScopedHoldingCacheSection : IDisposable
    {
        private HierarchicalFileTableTemplate<TDirName, TFileName, TDirInfo, TFileInfo> _fileTable;

        public ScopedHoldingCacheSection(HierarchicalFileTableTemplate<TDirName, TFileName, TDirInfo, TFileInfo> fileTable)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }

    public struct FindIndex
    {
        public IndexType NextDirectory;
        public IndexType NextFile;
    }

    public struct DirectoryKey
    {
        public IndexType ParentIndex;
        public TDirName Name;

        public static bool operator ==(in DirectoryKey left, in DirectoryKey right)
        {
            throw new NotImplementedException();
        }

        public static bool operator !=(in DirectoryKey left, in DirectoryKey right)
        {
            throw new NotImplementedException();
        }
    }

    public struct FileKey
    {
        public IndexType ParentIndex;
        public TFileName Name;

        public static bool operator ==(in FileKey left, in FileKey right)
        {
            throw new NotImplementedException();
        }

        public static bool operator !=(in FileKey left, in FileKey right)
        {
            throw new NotImplementedException();
        }
    }

    public struct DirectoryEntry
    {
        public IndexType IndexNextSibling;
        public IndexType IndexDirectory;
        public IndexType IndexFile;
        public TDirInfo Info;
    }

    public struct FileEntry
    {
        public IndexType IndexNextSibling;
        public TFileInfo Info;
    }

    public class EntryList<TKey, TEntry> : KeyValueListTemplate<TKey, TEntry>
        where TKey : unmanaged where TEntry : unmanaged
    {
        public EntryList()
        {
            throw new NotImplementedException();
        }

        public Result Add(out uint outIndex, in TKey key, in TEntry entry)
        {
            throw new NotImplementedException();
        }

        public Result Get(out uint outIndex, out TEntry outEntry, in TKey key)
        {
            throw new NotImplementedException();
        }

        public new Result GetByIndex(out TKey outKey, out TEntry outEntry, uint index)
        {
            throw new NotImplementedException();
        }

        public new Result Remove(in TKey key)
        {
            throw new NotImplementedException();
        }

        public Result Rename(in TKey newKey, in TKey oldKey)
        {
            throw new NotImplementedException();
        }

        protected new Result SetByIndex(uint index, in TEntry entry)
        {
            throw new NotImplementedException();
        }
    }

    private EntryList<DirectoryKey, DirectoryEntry> _directoryList;
    private EntryList<FileKey, FileEntry> _fileList;

    public HierarchicalFileTableTemplate()
    {
        throw new NotImplementedException();
    }

    public static uint QueryDirectoryEntryStorageSize(uint entryCount)
    {
        throw new NotImplementedException();
    }

    public static uint QueryFileEntryStorageSize(uint entryCount)
    {
        throw new NotImplementedException();
    }

    public static Result Format(BufferedAllocationTableStorage directoryEntries,
        BufferedAllocationTableStorage fileEntries)
    {
        throw new NotImplementedException();
    }

    public Result Initialize(BufferedAllocationTableStorage directoryEntries,
        BufferedAllocationTableStorage fileEntries)
    {
        throw new NotImplementedException();
    }

    public Result CreateDirectory(U8Span fullPath, in TDirInfo directoryInfo)
    {
        throw new NotImplementedException();
    }

    public Result CreateFile(U8Span fullPath, in TDirInfo directoryInfo)
    {
        throw new NotImplementedException();
    }

    public Result DeleteDirectory(U8Span fullPath)
    {
        throw new NotImplementedException();
    }

    public Result DeleteFile(U8Span fullPath)
    {
        throw new NotImplementedException();
    }

    public Result RenameDirectory(out bool outIsFile, out bool outIsSameEntry, out bool outIsParentEntry,
        U8Span newFullPath, U8Span oldFullPath)
    {
        throw new NotImplementedException();
    }

    public Result RenameFile(out bool outIsFile, out bool outIsSameEntry, U8Span newFullPath, U8Span oldFullPath)
    {
        throw new NotImplementedException();
    }

    public Result OpenFile(out IndexType outIndex, out TFileInfo outFileInfo, U8Span fullPath)
    {
        throw new NotImplementedException();
    }

    public Result OpenFile(out TFileInfo outFileInfo, U8Span fullPath)
    {
        throw new NotImplementedException();
    }

    public Result FindNextDirectory(out TDirName outDirectoryName, out bool outIsFinished, ref FindIndex iterator)
    {
        throw new NotImplementedException();
    }

    public Result FindNextFile(out long outFileSize, out TFileName outFileName, out bool outIsFinished,
        ref FindIndex iterator)
    {
        throw new NotImplementedException();
    }

    protected Result FindDirectoryRecursive(out DirectoryKey outParentDirectoryKey,
        out DirectoryEntry outParentDirectoryEntry, out DirectoryKey outDirectoryKey, U8Span fullPath)
    {
        throw new NotImplementedException();
    }

    protected Result FindFileRecursive(out DirectoryKey outParentDirectoryKey,
        out DirectoryEntry outParentDirectoryEntry, out FileKey outFileKey, U8Span fullPath)
    {
        throw new NotImplementedException();
    }

    protected Result GetDirectoryInformationFromKey(out IndexType outIndex, out TDirInfo outDirectoryInfo,
        in DirectoryKey key)
    {
        throw new NotImplementedException();
    }

    protected Result OpenFileFromKey(out IndexType outIndex, out TFileInfo outFileInfo, in FileKey key)
    {
        throw new NotImplementedException();
    }

    protected Result UpdateFileInformationFromKey(in FileKey key, in TFileInfo fileInfo)
    {
        throw new NotImplementedException();
    }

    protected Result FindOpenWithKey(ref FindIndex iterator, in DirectoryKey key)
    {
        throw new NotImplementedException();
    }

    public Result CheckSubEntry(out bool isSubEntry, IndexType baseEntryIndex, IndexType entryIndexToCheck, bool isFile)
    {
        throw new NotImplementedException();
    }

    protected Result Notify(out bool outIsFinished, ref FindIndex iterator, long id, bool isFile)
    {
        throw new NotImplementedException();
    }

    private Result FindParentDirectoryRecursive(out FindIndex outParentIndex, out DirectoryKey outParentDirectoryKey,
        out DirectoryEntry outParentDirectoryEntry, ref PathTool.PathParser pathParser)
    {
        throw new NotImplementedException();
    }

    private Result CheckSameEntryExists(out bool outIsFile, IndexType parentIndex, U8Span name, Result resultIfExists)
    {
        throw new NotImplementedException();
    }

    private Result GetDirectoryEntry(out IndexType outIndex, out DirectoryEntry outEntry, in DirectoryKey key)
    {
        throw new NotImplementedException();
    }

    private Result GetFileEntry(out IndexType outIndex, out FileEntry outEntry, in FileKey key)
    {
        throw new NotImplementedException();
    }

    private Result RemoveDirectoryLink(ref DirectoryEntry parentEntry, IndexType parentIndex,
        ref DirectoryEntry deleteEntry, IndexType deleteIndex)
    {
        throw new NotImplementedException();
    }

    private Result RemoveFileLink(ref DirectoryEntry parentEntry, IndexType parentIndex,
        ref FileEntry deleteEntry, IndexType deleteIndex)
    {
        throw new NotImplementedException();
    }

    private Result CheckSubDirectory(in DirectoryKey baseDirectoryKey, in DirectoryKey directoryKeyToCheck,
        Result resultIfSubDirectory)
    {
        throw new NotImplementedException();
    }
}