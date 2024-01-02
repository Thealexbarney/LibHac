// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Fs.Dbm;
using LibHac.Fs.Fsa;
using LibHac.FsSystem.Save;

namespace LibHac.Fs.Save;

public class SaveDataFileSystemCoreImpl : IExclusiveFileSystemBase
{
    private ValueSubStorage _allocationTableControlAreaStorage;
    private ValueSubStorage _allocationTableMetaStorage;
    private FileSystemControlArea _controlArea;
    private FileSystemObjectTemplate<DirectoryName, FileName> _fileSystem;
    private BufferedAllocationTableStorage _directoryEntries;
    private BufferedAllocationTableStorage _fileEntries;
    private ValueSubStorage _allocationTableDataStorage;
    private long _offset;
    private uint _countBody;
    private long _blockSize;

    public SaveDataFileSystemCoreImpl()
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    public static long QueryDataSize(long blockSize, ulong blockCount)
    {
        throw new NotImplementedException();
    }

    public static long QueryMetaSize(long blockSize, ulong blockCount)
    {
        throw new NotImplementedException();
    }

    private static long QueryAllocationTableStorageSize(uint blockCount)
    {
        throw new NotImplementedException();
    }

    public static Result Format(in ValueSubStorage storageControlArea, in ValueSubStorage storageMeta,
        in ValueSubStorage storageData, long blockSize, IBufferManager bufferManager)
    {
        throw new NotImplementedException();
    }

    public static Result ExpandControlArea(in ValueSubStorage storageControlArea, long sizeDataNew)
    {
        throw new NotImplementedException();
    }

    public static Result ExpandMeta(in ValueSubStorage storageMeta, long blockSize, long sizeDataOld, long sizeDataNew)
    {
        throw new NotImplementedException();
    }

    public Result Initialize(in ValueSubStorage storageControlArea, in ValueSubStorage storageMeta,
        in ValueSubStorage storageData, IBufferManager bufferManager)
    {
        throw new NotImplementedException();
    }

    public void FinalizeObject()
    {
        throw new NotImplementedException();
    }

    public Result CheckPathFormat(ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    public override Result OpenBaseFile(ref UniqueRef<IExclusiveFileBase> outFile, ref readonly Path path, OpenMode mode)
    {
        throw new NotImplementedException();
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, ref readonly Path path, OpenMode mode)
    {
        throw new NotImplementedException();
    }

    public override Result OpenBaseDirectory(ref UniqueRef<IExclusiveDirectoryBase> outDirectory, ref readonly Path path,
        OpenDirectoryMode mode)
    {
        throw new NotImplementedException();
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, ref readonly Path path,
        OpenDirectoryMode mode)
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoDeleteFile(ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoDeleteDirectory(ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoCreateFile(ref readonly Path path, long size, CreateFileOptions option)
    {
        throw new NotImplementedException();
    }

    protected override Result DoCreateDirectory(ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoRenameFile(ref readonly Path currentPath, ref readonly Path newPath)
    {
        throw new NotImplementedException();
    }

    protected override Result DoRenameDirectory(ref readonly Path currentPath, ref readonly Path newPath)
    {
        throw new NotImplementedException();
    }

    protected override Result DoDeleteDirectoryRecursively(ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoCleanDirectoryRecursively(ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetFreeSpaceSize(out long freeSpace, ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetTotalSpaceSize(out long totalSpace, ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoCommit()
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetFileSystemAttribute(out FileSystemAttribute outAttribute)
    {
        throw new NotImplementedException();
    }

    private Result CalcFreeListLength(out uint outCount)
    {
        throw new NotImplementedException();
    }

    public static Result FormatDbmLayer(in ValueSubStorage storageControlArea, in ValueSubStorage storageMeta,
        in ValueSubStorage storageData, long blockSize, uint blockCount, IBufferManager bufferManager)
    {
        throw new NotImplementedException();
    }

    public override Result GetFileIdFromPath(out long outId, ref readonly Path path, bool isFile)
    {
        throw new NotImplementedException();
    }

    public override Result CheckSubEntry(out bool isSubEntry, long baseEntryId, long entryIdToCheck, bool isFile)
    {
        throw new NotImplementedException();
    }

    public Result AcceptVisitor(IInternalStorageFileSystemVisitor visitor)
    {
        throw new NotImplementedException();
    }
}

public class SaveDataFileImpl : IExclusiveFileBase
{
    private FileObjectTemplate<DirectoryName, FileName> _fileObject;
    private OpenMode _mode;
    private AllocationTableStorage _allocationTableStorage;
    private AllocationTable _allocationTable;
    private ValueSubStorage _baseStorage;
    private long _blockSize;
    private long _offset;
    private long _size;

    internal SaveDataFileImpl()
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    public override OpenMode GetMode()
    {
        throw new NotImplementedException();
    }

    private FileObjectTemplate<DirectoryName, FileName> GetFileObject()
    {
        throw new NotImplementedException();
    }

    private Result InitializeTable()
    {
        throw new NotImplementedException();
    }

    private Result Initialize(OpenMode mode, AllocationTable allocationTable, long blockSize,
        in ValueSubStorage baseStorage, long offset, long size)
    {
        throw new NotImplementedException();
    }

    protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination, in ReadOption option)
    {
        throw new NotImplementedException();
    }

    protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
    {
        throw new NotImplementedException();
    }

    protected override Result DoSetSize(long size)
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetSize(out long size)
    {
        throw new NotImplementedException();
    }

    protected override Result DoFlush()
    {
        throw new NotImplementedException();
    }

    protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }
}

public class SaveDataDirectoryImpl : IExclusiveDirectoryBase
{
    private DirectoryObjectTemplate<DirectoryName, FileName> _directoryObject;
    private OpenDirectoryMode _mode;

    internal SaveDataDirectoryImpl(OpenDirectoryMode mode)
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    private DirectoryObjectTemplate<DirectoryName, FileName> GetDirectoryObject()
    {
        throw new NotImplementedException();
    }

    private Result Initialize()
    {
        throw new NotImplementedException();
    }

    protected override Result DoRead(out long entriesRead, Span<DirectoryEntry> entryBuffer)
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetEntryCount(out long entryCount)
    {
        throw new NotImplementedException();
    }

    public override Result NotifyDelete(long id, bool isFile)
    {
        throw new NotImplementedException();
    }
}