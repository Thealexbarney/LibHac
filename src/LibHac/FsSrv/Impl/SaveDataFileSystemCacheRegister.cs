using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsSrv.Impl;

/// <summary>
/// Wraps an <see cref="ISaveDataFileSystem"/>.
/// Upon disposal the base file system is returned to the provided <see cref="SaveDataFileSystemCacheManager"/>.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
public class SaveDataFileSystemCacheRegister : IFileSystem
{
    private SharedRef<ISaveDataFileSystem> _baseFileSystem;
    private SaveDataFileSystemCacheManager _cacheManager;
    private SaveDataSpaceId _spaceId;
    private ulong _saveDataId;

    public SaveDataFileSystemCacheRegister(ref SharedRef<ISaveDataFileSystem> baseFileSystem,
        SaveDataFileSystemCacheManager cacheManager, SaveDataSpaceId spaceId, ulong saveDataId)
    {
        _baseFileSystem = SharedRef<ISaveDataFileSystem>.CreateMove(ref baseFileSystem);
        _cacheManager = cacheManager;
        _spaceId = spaceId;
        _saveDataId = saveDataId;
    }

    public override void Dispose()
    {
        _cacheManager.Register(ref _baseFileSystem, _spaceId, _saveDataId);
        _baseFileSystem.Destroy();

        base.Dispose();
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
    {
        return _baseFileSystem.Get.OpenFile(ref outFile, in path, mode);
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
        OpenDirectoryMode mode)
    {
        return _baseFileSystem.Get.OpenDirectory(ref outDirectory, in path, mode);
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
    {
        return _baseFileSystem.Get.GetEntryType(out entryType, in path);
    }

    protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option)
    {
        return _baseFileSystem.Get.CreateFile(in path, size, option);
    }

    protected override Result DoDeleteFile(in Path path)
    {
        return _baseFileSystem.Get.DeleteFile(in path);
    }

    protected override Result DoCreateDirectory(in Path path)
    {
        return _baseFileSystem.Get.CreateDirectory(in path);
    }

    protected override Result DoDeleteDirectory(in Path path)
    {
        return _baseFileSystem.Get.DeleteDirectory(in path);
    }

    protected override Result DoDeleteDirectoryRecursively(in Path path)
    {
        return _baseFileSystem.Get.DeleteDirectoryRecursively(in path);
    }

    protected override Result DoCleanDirectoryRecursively(in Path path)
    {
        return _baseFileSystem.Get.CleanDirectoryRecursively(in path);
    }

    protected override Result DoRenameFile(in Path currentPath, in Path newPath)
    {
        return _baseFileSystem.Get.RenameFile(in currentPath, in newPath);
    }

    protected override Result DoRenameDirectory(in Path currentPath, in Path newPath)
    {
        return _baseFileSystem.Get.RenameDirectory(in currentPath, in newPath);
    }

    protected override Result DoCommit()
    {
        return _baseFileSystem.Get.Commit();
    }

    protected override Result DoCommitProvisionally(long counter)
    {
        return _baseFileSystem.Get.CommitProvisionally(counter);
    }

    protected override Result DoRollback()
    {
        return _baseFileSystem.Get.Rollback();
    }

    protected override Result DoGetFreeSpaceSize(out long freeSpace, in Path path)
    {
        return _baseFileSystem.Get.GetFreeSpaceSize(out freeSpace, in path);
    }

    protected override Result DoGetTotalSpaceSize(out long totalSpace, in Path path)
    {
        return _baseFileSystem.Get.GetTotalSpaceSize(out totalSpace, in path);
    }

    protected override Result DoGetFileSystemAttribute(out FileSystemAttribute outAttribute)
    {
        return _baseFileSystem.Get.GetFileSystemAttribute(out outAttribute);
    }
}