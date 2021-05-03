using System.IO;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;

namespace LibHac.FsSystem.Save
{
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

        protected override Result DoCreateDirectory(U8Span path)
        {
            Unsafe.SkipInit(out FsPath normalizedPath);

            Result rc = PathNormalizer.Normalize(normalizedPath.Str, out _, path, false, false);
            if (rc.IsFailure()) return rc;

            FileTable.AddDirectory(normalizedPath);

            return Result.Success;
        }

        protected override Result DoCreateFile(U8Span path, long size, CreateFileOptions options)
        {
            Unsafe.SkipInit(out FsPath normalizedPath);

            Result rc = PathNormalizer.Normalize(normalizedPath.Str, out _, path, false, false);
            if (rc.IsFailure()) return rc;

            if (size == 0)
            {
                var emptyFileEntry = new SaveFileInfo { StartBlock = int.MinValue, Length = size };
                FileTable.AddFile(normalizedPath, ref emptyFileEntry);

                return Result.Success;
            }

            int blockCount = (int)BitUtil.DivideUp(size, AllocationTable.Header.BlockSize);
            int startBlock = AllocationTable.Allocate(blockCount);

            if (startBlock == -1)
            {
                return ResultFs.AllocationTableFull.Log();
            }

            var fileEntry = new SaveFileInfo { StartBlock = startBlock, Length = size };

            FileTable.AddFile(normalizedPath, ref fileEntry);

            return Result.Success;
        }

        protected override Result DoDeleteDirectory(U8Span path)
        {
            Unsafe.SkipInit(out FsPath normalizedPath);

            Result rc = PathNormalizer.Normalize(normalizedPath.Str, out _, path, false, false);
            if (rc.IsFailure()) return rc;

            FileTable.DeleteDirectory(normalizedPath);

            return Result.Success;
        }

        protected override Result DoDeleteDirectoryRecursively(U8Span path)
        {
            Unsafe.SkipInit(out FsPath normalizedPath);

            Result rc = PathNormalizer.Normalize(normalizedPath.Str, out _, path, false, false);
            if (rc.IsFailure()) return rc;

            rc = CleanDirectoryRecursively(normalizedPath);
            if (rc.IsFailure()) return rc;

            rc = DeleteDirectory(normalizedPath);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        protected override Result DoCleanDirectoryRecursively(U8Span path)
        {
            Unsafe.SkipInit(out FsPath normalizedPath);

            Result rc = PathNormalizer.Normalize(normalizedPath.Str, out _, path, false, false);
            if (rc.IsFailure()) return rc;

            FileSystemExtensions.CleanDirectoryRecursivelyGeneric(this, normalizedPath.ToString());

            return Result.Success;
        }

        protected override Result DoDeleteFile(U8Span path)
        {
            Unsafe.SkipInit(out FsPath normalizedPath);

            Result rc = PathNormalizer.Normalize(normalizedPath.Str, out _, path, false, false);
            if (rc.IsFailure()) return rc;

            if (!FileTable.TryOpenFile(normalizedPath, out SaveFileInfo fileInfo))
            {
                return ResultFs.PathNotFound.Log();
            }

            if (fileInfo.StartBlock != int.MinValue)
            {
                AllocationTable.Free(fileInfo.StartBlock);
            }

            FileTable.DeleteFile(normalizedPath);

            return Result.Success;
        }

        protected override Result DoOpenDirectory(out IDirectory directory, U8Span path, OpenDirectoryMode mode)
        {
            UnsafeHelpers.SkipParamInit(out directory);

            Unsafe.SkipInit(out FsPath normalizedPath);

            Result rc = PathNormalizer.Normalize(normalizedPath.Str, out _, path, false, false);
            if (rc.IsFailure()) return rc;

            if (!FileTable.TryOpenDirectory(normalizedPath, out SaveFindPosition position))
            {
                return ResultFs.PathNotFound.Log();
            }

            directory = new SaveDataDirectory(this, position, mode);

            return Result.Success;
        }

        protected override Result DoOpenFile(out IFile file, U8Span path, OpenMode mode)
        {
            UnsafeHelpers.SkipParamInit(out file);

            Unsafe.SkipInit(out FsPath normalizedPath);

            Result rc = PathNormalizer.Normalize(normalizedPath.Str, out _, path, false, false);
            if (rc.IsFailure()) return rc;

            if (!FileTable.TryOpenFile(normalizedPath, out SaveFileInfo fileInfo))
            {
                return ResultFs.PathNotFound.Log();
            }

            AllocationTableStorage storage = OpenFatStorage(fileInfo.StartBlock);

            file = new SaveDataFile(storage, normalizedPath, FileTable, fileInfo.Length, mode);

            return Result.Success;
        }

        protected override Result DoRenameDirectory(U8Span oldPath, U8Span newPath)
        {
            Unsafe.SkipInit(out FsPath normalizedCurrentPath);
            Unsafe.SkipInit(out FsPath normalizedNewPath);

            Result rc = PathNormalizer.Normalize(normalizedCurrentPath.Str, out _, oldPath, false, false);
            if (rc.IsFailure()) return rc;

            rc = PathNormalizer.Normalize(normalizedNewPath.Str, out _, newPath, false, false);
            if (rc.IsFailure()) return rc;

            return FileTable.RenameDirectory(normalizedCurrentPath, normalizedNewPath);
        }

        protected override Result DoRenameFile(U8Span oldPath, U8Span newPath)
        {
            Unsafe.SkipInit(out FsPath normalizedCurrentPath);
            Unsafe.SkipInit(out FsPath normalizedNewPath);

            Result rc = PathNormalizer.Normalize(normalizedCurrentPath.Str, out _, oldPath, false, false);
            if (rc.IsFailure()) return rc;

            rc = PathNormalizer.Normalize(normalizedNewPath.Str, out _, newPath, false, false);
            if (rc.IsFailure()) return rc;

            FileTable.RenameFile(normalizedCurrentPath, normalizedNewPath);

            return Result.Success;
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out entryType);

            Unsafe.SkipInit(out FsPath normalizedPath);

            Result rc = PathNormalizer.Normalize(normalizedPath.Str, out _, path, false, false);
            if (rc.IsFailure()) return rc;

            if (FileTable.TryOpenFile(normalizedPath, out SaveFileInfo _))
            {
                entryType = DirectoryEntryType.File;
                return Result.Success;
            }

            if (FileTable.TryOpenDirectory(normalizedPath, out SaveFindPosition _))
            {
                entryType = DirectoryEntryType.Directory;
                return Result.Success;
            }

            return ResultFs.PathNotFound.Log();
        }

        protected override Result DoGetFreeSpaceSize(out long freeSpace, U8Span path)
        {
            int freeBlockCount = AllocationTable.GetFreeListLength();
            freeSpace = Header.BlockSize * freeBlockCount;

            return Result.Success;
        }

        protected override Result DoGetTotalSpaceSize(out long totalSpace, U8Span path)
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
}
