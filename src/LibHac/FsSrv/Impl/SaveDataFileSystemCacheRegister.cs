using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsSrv.Impl;

/// <summary>
/// Wraps an <see cref="ISaveDataFileSystem"/>.
/// Upon disposal the base file system is returned to the provided <see cref="SaveDataFileSystemCacheManager"/>.
/// </summary>
/// <remarks>Based on nnSdk 17.5.0 (FS 17.0.0)</remarks>
public class SaveDataFileSystemCacheRegister : IFileSystem
{
    private SharedRef<ISaveDataFileSystem> _baseFileSystem;
    private SaveDataFileSystemCacheManager _cacheManager;
    private SaveDataSpaceId _spaceId;
    private ulong _saveDataId;

    public SaveDataFileSystemCacheRegister(ref readonly SharedRef<ISaveDataFileSystem> baseFileSystem,
        SaveDataFileSystemCacheManager cacheManager, SaveDataSpaceId spaceId, ulong saveDataId)
    {
        _baseFileSystem = SharedRef<ISaveDataFileSystem>.CreateCopy(in baseFileSystem);
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

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, ref readonly Path path, OpenMode mode)
    {
        return _baseFileSystem.Get.OpenFile(ref outFile, in path, mode).Ret();
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, ref readonly Path path,
        OpenDirectoryMode mode)
    {
        return _baseFileSystem.Get.OpenDirectory(ref outDirectory, in path, mode).Ret();
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, ref readonly Path path)
    {
        return _baseFileSystem.Get.GetEntryType(out entryType, in path).Ret();
    }

    protected override Result DoCreateFile(ref readonly Path path, long size, CreateFileOptions option)
    {
        return _baseFileSystem.Get.CreateFile(in path, size, option).Ret();
    }

    protected override Result DoDeleteFile(ref readonly Path path)
    {
        return _baseFileSystem.Get.DeleteFile(in path).Ret();
    }

    protected override Result DoCreateDirectory(ref readonly Path path)
    {
        return _baseFileSystem.Get.CreateDirectory(in path).Ret();
    }

    protected override Result DoDeleteDirectory(ref readonly Path path)
    {
        return _baseFileSystem.Get.DeleteDirectory(in path).Ret();
    }

    protected override Result DoDeleteDirectoryRecursively(ref readonly Path path)
    {
        return _baseFileSystem.Get.DeleteDirectoryRecursively(in path).Ret();
    }

    protected override Result DoCleanDirectoryRecursively(ref readonly Path path)
    {
        return _baseFileSystem.Get.CleanDirectoryRecursively(in path).Ret();
    }

    protected override Result DoRenameFile(ref readonly Path currentPath, ref readonly Path newPath)
    {
        return _baseFileSystem.Get.RenameFile(in currentPath, in newPath).Ret();
    }

    protected override Result DoRenameDirectory(ref readonly Path currentPath, ref readonly Path newPath)
    {
        return _baseFileSystem.Get.RenameDirectory(in currentPath, in newPath).Ret();
    }

    protected override Result DoCommit()
    {
        return _baseFileSystem.Get.Commit().Ret();
    }

    protected override Result DoCommitProvisionally(long counter)
    {
        return _baseFileSystem.Get.CommitProvisionally(counter).Ret();
    }

    protected override Result DoRollback()
    {
        return _baseFileSystem.Get.Rollback().Ret();
    }

    protected override Result DoGetFreeSpaceSize(out long freeSpace, ref readonly Path path)
    {
        return _baseFileSystem.Get.GetFreeSpaceSize(out freeSpace, in path).Ret();
    }

    protected override Result DoGetTotalSpaceSize(out long totalSpace, ref readonly Path path)
    {
        return _baseFileSystem.Get.GetTotalSpaceSize(out totalSpace, in path).Ret();
    }

    protected override Result DoGetFileSystemAttribute(out FileSystemAttribute outAttribute)
    {
        return _baseFileSystem.Get.GetFileSystemAttribute(out outAttribute).Ret();
    }
}