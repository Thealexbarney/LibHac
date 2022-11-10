using System.IO;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Tools.Fs;
using LibHac.Util;
using Path = LibHac.Fs.Path;

namespace LibHac.Tools.FsSystem.Save;

public class SaveDataFileSystemCore : IFileSystem
{
    private IStorage BaseStorage { get; }
    private IStorage HeaderStorage { get; }

    public AllocationTable AllocationTable { get; }
    private SaveHeader Header { get; }

    public HierarchicalSaveFileTable FileTable { get; }

    public SaveDataFileSystemCore(IStorage storage, IStorage allocationTable, IStorage header)
    {
        HeaderStorage = header;
        BaseStorage = storage;
        AllocationTable = new AllocationTable(allocationTable, header.Slice(0x18, 0x30));

        Header = new SaveHeader(HeaderStorage);

        AllocationTableStorage dirTableStorage = OpenFatStorage(AllocationTable.Header.DirectoryTableBlock);
        AllocationTableStorage fileTableStorage = OpenFatStorage(AllocationTable.Header.FileTableBlock);

        FileTable = new HierarchicalSaveFileTable(dirTableStorage, fileTableStorage);
    }

    private Result CheckIfNormalized(in Path path)
    {
        Result res = PathNormalizer.IsNormalized(out bool isNormalized, out _, path.GetString());
        if (res.IsFailure()) return res.Miss();

        if (!isNormalized)
            return ResultFs.NotNormalized.Log();

        return Result.Success;
    }

    protected override Result DoCreateDirectory(in Path path)
    {
        Result res = CheckIfNormalized(in path);
        if (res.IsFailure()) return res.Miss();

        FileTable.AddDirectory(new U8Span(path.GetString()));

        return Result.Success;
    }

    protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option)
    {
        Result res = CheckIfNormalized(in path);
        if (res.IsFailure()) return res.Miss();

        if (size == 0)
        {
            var emptyFileEntry = new SaveFileInfo { StartBlock = int.MinValue, Length = size };
            FileTable.AddFile(new U8Span(path.GetString()), ref emptyFileEntry);

            return Result.Success;
        }

        int blockCount = (int)BitUtil.DivideUp(size, AllocationTable.Header.BlockSize);
        int startBlock = AllocationTable.Allocate(blockCount);

        if (startBlock == -1)
        {
            return ResultFs.AllocationTableFull.Log();
        }

        var fileEntry = new SaveFileInfo { StartBlock = startBlock, Length = size };

        FileTable.AddFile(new U8Span(path.GetString()), ref fileEntry);

        return Result.Success;
    }

    protected override Result DoDeleteDirectory(in Path path)
    {
        Result res = CheckIfNormalized(in path);
        if (res.IsFailure()) return res.Miss();

        FileTable.DeleteDirectory(new U8Span(path.GetString()));

        return Result.Success;
    }

    protected override Result DoDeleteDirectoryRecursively(in Path path)
    {
        Result res = CheckIfNormalized(in path);
        if (res.IsFailure()) return res.Miss();

        res = CleanDirectoryRecursively(in path);
        if (res.IsFailure()) return res.Miss();

        res = DeleteDirectory(in path);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoCleanDirectoryRecursively(in Path path)
    {
        Result res = CheckIfNormalized(in path);
        if (res.IsFailure()) return res.Miss();

        FileSystemExtensions.CleanDirectoryRecursivelyGeneric(this, new U8Span(path.GetString()).ToString());

        return Result.Success;
    }

    protected override Result DoDeleteFile(in Path path)
    {
        Result res = CheckIfNormalized(in path);
        if (res.IsFailure()) return res.Miss();

        if (!FileTable.TryOpenFile(new U8Span(path.GetString()), out SaveFileInfo fileInfo))
        {
            return ResultFs.PathNotFound.Log();
        }

        if (fileInfo.StartBlock != int.MinValue)
        {
            AllocationTable.Free(fileInfo.StartBlock);
        }

        FileTable.DeleteFile(new U8Span(path.GetString()));

        return Result.Success;
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
        OpenDirectoryMode mode)
    {
        Result res = CheckIfNormalized(in path);
        if (res.IsFailure()) return res.Miss();

        if (!FileTable.TryOpenDirectory(new U8Span(path.GetString()), out SaveFindPosition position))
        {
            return ResultFs.PathNotFound.Log();
        }

        outDirectory.Reset(new SaveDataDirectory(this, position, mode));

        return Result.Success;
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
    {
        Result res = CheckIfNormalized(in path);
        if (res.IsFailure()) return res.Miss();

        if (!FileTable.TryOpenFile(new U8Span(path.GetString()), out SaveFileInfo fileInfo))
        {
            return ResultFs.PathNotFound.Log();
        }

        AllocationTableStorage storage = OpenFatStorage(fileInfo.StartBlock);

        outFile.Reset(new SaveDataFile(storage, new U8Span(path.GetString()), FileTable, fileInfo.Length, mode));

        return Result.Success;
    }

    protected override Result DoRenameDirectory(in Path currentPath, in Path newPath)
    {
        Result res = CheckIfNormalized(in currentPath);
        if (res.IsFailure()) return res.Miss();

        res = CheckIfNormalized(in newPath);
        if (res.IsFailure()) return res.Miss();

        return FileTable.RenameDirectory(new U8Span(currentPath.GetString()), new U8Span(newPath.GetString()));
    }

    protected override Result DoRenameFile(in Path currentPath, in Path newPath)
    {
        Result res = CheckIfNormalized(in currentPath);
        if (res.IsFailure()) return res.Miss();

        res = CheckIfNormalized(in newPath);
        if (res.IsFailure()) return res.Miss();

        FileTable.RenameFile(new U8Span(currentPath.GetString()), new U8Span(newPath.GetString()));

        return Result.Success;
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
    {
        UnsafeHelpers.SkipParamInit(out entryType);

        Result res = CheckIfNormalized(in path);
        if (res.IsFailure()) return res.Miss();

        if (FileTable.TryOpenFile(new U8Span(path.GetString()), out SaveFileInfo _))
        {
            entryType = DirectoryEntryType.File;
            return Result.Success;
        }

        if (FileTable.TryOpenDirectory(new U8Span(path.GetString()), out SaveFindPosition _))
        {
            entryType = DirectoryEntryType.Directory;
            return Result.Success;
        }

        return ResultFs.PathNotFound.Log();
    }

    protected override Result DoGetFreeSpaceSize(out long freeSpace, in Path path)
    {
        int freeBlockCount = AllocationTable.GetFreeListLength();
        freeSpace = Header.BlockSize * freeBlockCount;

        return Result.Success;
    }

    protected override Result DoGetTotalSpaceSize(out long totalSpace, in Path path)
    {
        totalSpace = Header.BlockSize * Header.BlockCount;

        return Result.Success;
    }

    protected override Result DoCommit()
    {
        return Result.Success;
    }

    public IStorage GetBaseStorage() => BaseStorage;
    public IStorage GetHeaderStorage() => HeaderStorage;

    public void FsTrim()
    {
        AllocationTable.FsTrim();

        foreach (DirectoryEntryEx file in this.EnumerateEntries("*", SearchOptions.RecurseSubdirectories))
        {
            if (FileTable.TryOpenFile(file.FullPath.ToU8Span(), out SaveFileInfo fileInfo) && fileInfo.StartBlock >= 0)
            {
                AllocationTable.FsTrimList(fileInfo.StartBlock);

                OpenFatStorage(fileInfo.StartBlock).Slice(fileInfo.Length).Fill(SaveDataFileSystem.TrimFillValue);
            }
        }

        int freeIndex = AllocationTable.GetFreeListBlockIndex();
        if (freeIndex == 0) return;

        AllocationTable.FsTrimList(freeIndex);

        OpenFatStorage(freeIndex).Fill(SaveDataFileSystem.TrimFillValue);

        FileTable.TrimFreeEntries();
    }

    private AllocationTableStorage OpenFatStorage(int blockIndex)
    {
        return new AllocationTableStorage(BaseStorage, AllocationTable, (int)Header.BlockSize, blockIndex);
    }
}

public class SaveHeader
{
    public string Magic { get; }
    public uint Version { get; }
    public long BlockCount { get; }
    public long BlockSize { get; }


    public SaveHeader(IStorage storage)
    {
        var reader = new BinaryReader(storage.AsStream());

        Magic = reader.ReadAscii(4);
        Version = reader.ReadUInt32();
        BlockCount = reader.ReadInt64();
        BlockSize = reader.ReadInt64();
    }
}