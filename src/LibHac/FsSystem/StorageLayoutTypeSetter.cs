﻿using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem;

[Flags]
internal enum StorageType
{
    Bis = 1 << 0,
    SdCard = 1 << 1,
    GameCard = 1 << 2,
    Usb = 1 << 3,

    NonGameCard = Bis | SdCard | Usb,
    All = Bis | SdCard | GameCard | Usb
}

internal static class StorageLayoutType
{
    public static bool IsStorageFlagValid(StorageType storageFlag)
    {
        return storageFlag != 0;
    }
}

internal struct ScopedStorageLayoutTypeSetter : IDisposable
{
    // ReSharper disable once UnusedParameter.Local
    public ScopedStorageLayoutTypeSetter(StorageType storageFlag)
    {
        // Todo: Implement
    }

    public void Dispose()
    {

    }
}

internal class StorageLayoutTypeSetStorage : IStorage
{
    private SharedRef<IStorage> _baseStorage;
    private StorageType _storageFlag;

    public StorageLayoutTypeSetStorage(ref SharedRef<IStorage> baseStorage, StorageType storageFlag)
    {
        _baseStorage = SharedRef<IStorage>.CreateMove(ref baseStorage);
        _storageFlag = storageFlag;

        Assert.SdkAssert(StorageLayoutType.IsStorageFlagValid(storageFlag));
    }

    public override void Dispose()
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        _baseStorage.Destroy();

        base.Dispose();
    }

    protected override Result DoRead(long offset, Span<byte> destination)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseStorage.Get.Read(offset, destination);
    }

    protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseStorage.Get.Write(offset, source);
    }

    protected override Result DoFlush()
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseStorage.Get.Flush();
    }

    protected override Result DoSetSize(long size)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseStorage.Get.SetSize(size);
    }

    protected override Result DoGetSize(out long size)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseStorage.Get.GetSize(out size);
    }

    protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseStorage.Get.OperateRange(outBuffer, operationId, offset, size, inBuffer);
    }
}

internal class StorageLayoutTypeSetFile : IFile
{
    private IFile _baseFile;
    private UniqueRef<IFile> _baseFileUnique;
    private SharedRef<IFile> _baseFileShared;
    private StorageType _storageFlag;

    public StorageLayoutTypeSetFile(ref UniqueRef<IFile> baseFile, StorageType storageFlag)
    {
        _baseFile = baseFile.Get;
        _baseFileUnique = new UniqueRef<IFile>(ref baseFile);
        _storageFlag = storageFlag;

        Assert.SdkAssert(StorageLayoutType.IsStorageFlagValid(storageFlag));
    }

    public StorageLayoutTypeSetFile(ref SharedRef<IFile> baseFile, StorageType storageFlag)
    {
        _baseFile = baseFile.Get;
        _baseFileShared = SharedRef<IFile>.CreateMove(ref baseFile);
        _storageFlag = storageFlag;

        Assert.SdkAssert(StorageLayoutType.IsStorageFlagValid(storageFlag));
    }

    public override void Dispose()
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);

        _baseFile = null;
        _baseFileUnique.Destroy();
        _baseFileShared.Destroy();

        base.Dispose();
    }

    protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination, in ReadOption option)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFile.Read(out bytesRead, offset, destination, in option);
    }

    protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFile.Write(offset, source, in option);
    }

    protected override Result DoFlush()
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFile.Flush();
    }

    protected override Result DoSetSize(long size)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFile.SetSize(size);
    }

    protected override Result DoGetSize(out long size)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFile.GetSize(out size);
    }

    protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFile.OperateRange(outBuffer, operationId, offset, size, inBuffer);
    }
}

internal class StorageLayoutTypeSetDirectory : IDirectory
{
    private UniqueRef<IDirectory> _baseDirectory;
    private StorageType _storageFlag;

    public StorageLayoutTypeSetDirectory(ref UniqueRef<IDirectory> baseDirectory, StorageType storageFlag)
    {
        _baseDirectory = new UniqueRef<IDirectory>(ref baseDirectory);
        _storageFlag = storageFlag;
    }

    public override void Dispose()
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        _baseDirectory.Destroy();

        base.Dispose();
    }

    protected override Result DoRead(out long entriesRead, Span<DirectoryEntry> entryBuffer)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseDirectory.Get.Read(out entriesRead, entryBuffer);
    }

    protected override Result DoGetEntryCount(out long entryCount)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseDirectory.Get.GetEntryCount(out entryCount);
    }
}

internal class StorageLayoutTypeSetFileSystem : IFileSystem
{
    private SharedRef<IFileSystem> _baseFileSystem;
    private StorageType _storageFlag;

    public StorageLayoutTypeSetFileSystem(ref SharedRef<IFileSystem> baseFileSystem, StorageType storageFlag)
    {
        _baseFileSystem = SharedRef<IFileSystem>.CreateMove(ref baseFileSystem);
        _storageFlag = storageFlag;

        Assert.SdkAssert(StorageLayoutType.IsStorageFlagValid(storageFlag));
    }

    public override void Dispose()
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        _baseFileSystem.Destroy();
        base.Dispose();
    }

    protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.CreateFile(path, size, option);
    }

    protected override Result DoDeleteFile(in Path path)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.DeleteFile(path);
    }

    protected override Result DoCreateDirectory(in Path path)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.CreateDirectory(path);
    }

    protected override Result DoDeleteDirectory(in Path path)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.DeleteDirectory(path);
    }

    protected override Result DoDeleteDirectoryRecursively(in Path path)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.DeleteDirectoryRecursively(path);
    }

    protected override Result DoCleanDirectoryRecursively(in Path path)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.CleanDirectoryRecursively(path);
    }

    protected override Result DoRenameFile(in Path currentPath, in Path newPath)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.RenameFile(currentPath, newPath);
    }

    protected override Result DoRenameDirectory(in Path currentPath, in Path newPath)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.RenameDirectory(currentPath, newPath);
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.GetEntryType(out entryType, path);
    }

    protected override Result DoGetFreeSpaceSize(out long freeSpace, in Path path)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.GetFreeSpaceSize(out freeSpace, path);
    }

    protected override Result DoGetTotalSpaceSize(out long totalSpace, in Path path)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.GetTotalSpaceSize(out totalSpace, path);
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        using var baseFile = new UniqueRef<IFile>();

        Result rc = _baseFileSystem.Get.OpenFile(ref baseFile.Ref(), in path, mode);
        if (rc.IsFailure()) return rc;

        outFile.Reset(new StorageLayoutTypeSetFile(ref baseFile.Ref(), _storageFlag));
        return Result.Success;
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
        OpenDirectoryMode mode)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        using var baseDirectory = new UniqueRef<IDirectory>();

        Result rc = _baseFileSystem.Get.OpenDirectory(ref baseDirectory.Ref(), in path, mode);
        if (rc.IsFailure()) return rc;

        outDirectory.Reset(new StorageLayoutTypeSetDirectory(ref baseDirectory.Ref(), _storageFlag));
        return Result.Success;
    }

    protected override Result DoCommit()
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.Commit();
    }

    protected override Result DoCommitProvisionally(long counter)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.CommitProvisionally(counter);
    }

    protected override Result DoRollback()
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.Rollback();
    }

    protected override Result DoFlush()
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.Flush();
    }

    protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in Path path)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.GetFileTimeStampRaw(out timeStamp, path);
    }

    protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
        in Path path)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.QueryEntry(outBuffer, inBuffer, queryId, path);
    }
}
