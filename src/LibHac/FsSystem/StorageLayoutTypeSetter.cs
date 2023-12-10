using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem;

[Flags]
internal enum StorageLayoutType
{
    Bis = 1 << 0,
    SdCard = 1 << 1,
    GameCard = 1 << 2,
    Usb = 1 << 3,

    NonGameCard = Bis | SdCard | Usb,
    All = Bis | SdCard | GameCard | Usb
}

/// <summary>
/// Contains functions for validating the storage layout type flag.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
internal static class StorageLayoutTypeFunctions
{
    public static bool IsStorageFlagValid(StorageLayoutType storageFlag)
    {
        return storageFlag != 0;
    }
}

internal struct ScopedStorageLayoutTypeSetter : IDisposable
{
    // ReSharper disable once UnusedParameter.Local
    public ScopedStorageLayoutTypeSetter(StorageLayoutType storageFlag)
    {
        // Todo: Implement
    }

    public void Dispose() { }
}

/// <summary>
/// Wraps an <see cref="IStorage"/>, automatically setting the thread's storage type when accessing the storage.
/// This is used to determine which storage speed emulation parameters to use for the current thread.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
internal class StorageLayoutTypeSetStorage : IStorage
{
    private SharedRef<IStorage> _baseStorage;
    private StorageLayoutType _storageFlag;

    public StorageLayoutTypeSetStorage(ref SharedRef<IStorage> baseStorage, StorageLayoutType storageFlag)
    {
        _baseStorage = SharedRef<IStorage>.CreateMove(ref baseStorage);
        _storageFlag = storageFlag;

        Assert.SdkAssert(StorageLayoutTypeFunctions.IsStorageFlagValid(storageFlag));
    }

    public override void Dispose()
    {
        using (new ScopedStorageLayoutTypeSetter(_storageFlag))
        {
            _baseStorage.Destroy();
        }

        base.Dispose();
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseStorage.Get.Read(offset, destination);
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseStorage.Get.Write(offset, source);
    }

    public override Result Flush()
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseStorage.Get.Flush();
    }

    public override Result SetSize(long size)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseStorage.Get.SetSize(size);
    }

    public override Result GetSize(out long size)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseStorage.Get.GetSize(out size);
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseStorage.Get.OperateRange(outBuffer, operationId, offset, size, inBuffer);
    }
}

/// <summary>
/// Wraps an <see cref="IFile"/>, automatically setting the thread's storage type when accessing the file.
/// This is used to determine which storage speed emulation parameters to use for the current thread.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
internal class StorageLayoutTypeSetFile : IFile
{
    private IFile _baseFile;
    private UniqueRef<IFile> _baseFileUnique;
    private SharedRef<IFile> _baseFileShared;
    private StorageLayoutType _storageFlag;

    public StorageLayoutTypeSetFile(ref UniqueRef<IFile> baseFile, StorageLayoutType storageFlag)
    {
        _baseFile = baseFile.Get;
        _baseFileUnique = new UniqueRef<IFile>(ref baseFile);
        _storageFlag = storageFlag;

        Assert.SdkAssert(StorageLayoutTypeFunctions.IsStorageFlagValid(storageFlag));
    }

    public StorageLayoutTypeSetFile(ref SharedRef<IFile> baseFile, StorageLayoutType storageFlag)
    {
        _baseFile = baseFile.Get;
        _baseFileShared = SharedRef<IFile>.CreateMove(ref baseFile);
        _storageFlag = storageFlag;

        Assert.SdkAssert(StorageLayoutTypeFunctions.IsStorageFlagValid(storageFlag));
    }

    public override void Dispose()
    {
        using (new ScopedStorageLayoutTypeSetter(_storageFlag))
        {
            _baseFile = null;
            _baseFileUnique.Destroy();
            _baseFileShared.Destroy();
        }

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

/// <summary>
/// Wraps an <see cref="IDirectory"/>, automatically setting the thread's storage type when accessing the directory.
/// This is used to determine which storage speed emulation parameters to use for the current thread.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
internal class StorageLayoutTypeSetDirectory : IDirectory
{
    private UniqueRef<IDirectory> _baseDirectory;
    private StorageLayoutType _storageFlag;

    public StorageLayoutTypeSetDirectory(ref UniqueRef<IDirectory> baseDirectory, StorageLayoutType storageFlag)
    {
        _baseDirectory = new UniqueRef<IDirectory>(ref baseDirectory);
        _storageFlag = storageFlag;
    }

    public override void Dispose()
    {
        using (new ScopedStorageLayoutTypeSetter(_storageFlag))
        {
            _baseDirectory.Destroy();
        }

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

/// <summary>
/// Wraps an <see cref="IFileSystem"/>, automatically setting the thread's storage type when accessing the file system.
/// This is used to determine which storage speed emulation parameters to use for the current thread.
/// </summary>
internal class StorageLayoutTypeSetFileSystem : IFileSystem
{
    private SharedRef<IFileSystem> _baseFileSystem;
    private StorageLayoutType _storageFlag;

    public StorageLayoutTypeSetFileSystem(ref SharedRef<IFileSystem> baseFileSystem, StorageLayoutType storageFlag)
    {
        _baseFileSystem = SharedRef<IFileSystem>.CreateMove(ref baseFileSystem);
        _storageFlag = storageFlag;

        Assert.SdkAssert(StorageLayoutTypeFunctions.IsStorageFlagValid(storageFlag));
    }

    public override void Dispose()
    {
        using (new ScopedStorageLayoutTypeSetter(_storageFlag))
        {
            _baseFileSystem.Destroy();
        }

        base.Dispose();
    }

    protected override Result DoCreateFile(ref readonly Path path, long size, CreateFileOptions option)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.CreateFile(in path, size, option);
    }

    protected override Result DoDeleteFile(ref readonly Path path)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.DeleteFile(in path);
    }

    protected override Result DoCreateDirectory(ref readonly Path path)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.CreateDirectory(in path);
    }

    protected override Result DoDeleteDirectory(ref readonly Path path)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.DeleteDirectory(in path);
    }

    protected override Result DoDeleteDirectoryRecursively(ref readonly Path path)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.DeleteDirectoryRecursively(in path);
    }

    protected override Result DoCleanDirectoryRecursively(ref readonly Path path)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.CleanDirectoryRecursively(in path);
    }

    protected override Result DoRenameFile(ref readonly Path currentPath, ref readonly Path newPath)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.RenameFile(in currentPath, in newPath);
    }

    protected override Result DoRenameDirectory(ref readonly Path currentPath, ref readonly Path newPath)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.RenameDirectory(in currentPath, in newPath);
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, ref readonly Path path)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.GetEntryType(out entryType, in path);
    }

    protected override Result DoGetFreeSpaceSize(out long freeSpace, ref readonly Path path)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.GetFreeSpaceSize(out freeSpace, in path);
    }

    protected override Result DoGetTotalSpaceSize(out long totalSpace, ref readonly Path path)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.GetTotalSpaceSize(out totalSpace, in path);
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, ref readonly Path path, OpenMode mode)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        using var baseFile = new UniqueRef<IFile>();

        Result res = _baseFileSystem.Get.OpenFile(ref baseFile.Ref, in path, mode);
        if (res.IsFailure()) return res.Miss();

        outFile.Reset(new StorageLayoutTypeSetFile(ref baseFile.Ref, _storageFlag));
        return Result.Success;
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, ref readonly Path path,
        OpenDirectoryMode mode)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        using var baseDirectory = new UniqueRef<IDirectory>();

        Result res = _baseFileSystem.Get.OpenDirectory(ref baseDirectory.Ref, in path, mode);
        if (res.IsFailure()) return res.Miss();

        outDirectory.Reset(new StorageLayoutTypeSetDirectory(ref baseDirectory.Ref, _storageFlag));
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

    protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, ref readonly Path path)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.GetFileTimeStampRaw(out timeStamp, in path);
    }

    protected override Result DoGetFileSystemAttribute(out FileSystemAttribute outAttribute)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.GetFileSystemAttribute(out outAttribute);
    }

    protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
        ref readonly Path path)
    {
        using var scopedContext = new ScopedStorageLayoutTypeSetter(_storageFlag);
        return _baseFileSystem.Get.QueryEntry(outBuffer, inBuffer, queryId, in path);
    }
}