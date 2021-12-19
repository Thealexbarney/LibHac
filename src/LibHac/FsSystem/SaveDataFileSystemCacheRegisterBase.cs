using System;
using InlineIL;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Tools.FsSystem.Save;

namespace LibHac.FsSystem;

/// <summary>
/// Wraps a save data <see cref="IFileSystem"/>.
/// Upon disposal the base file system is returned to the provided <see cref="ISaveDataFileSystemCacheManager"/>.
/// </summary>
/// <typeparam name="T">The type of the base file system. Must be one of <see cref="SaveDataFileSystem"/>,
/// <see cref="ApplicationTemporaryFileSystem"/> or <see cref="DirectorySaveDataFileSystem"/>.</typeparam>
public class SaveDataFileSystemCacheRegisterBase<T> : IFileSystem where T : IFileSystem
{
    private SharedRef<T> _baseFileSystem;
    private ISaveDataFileSystemCacheManager _cacheManager;

    public SaveDataFileSystemCacheRegisterBase(ref SharedRef<T> baseFileSystem,
        ISaveDataFileSystemCacheManager cacheManager)
    {
        if (typeof(T) != typeof(SaveDataFileSystemHolder) && typeof(T) != typeof(ApplicationTemporaryFileSystem))
        {
            throw new NotSupportedException(
                $"The file system type of a {nameof(SaveDataFileSystemCacheRegisterBase<T>)} must be {nameof(SaveDataFileSystemHolder)} or {nameof(ApplicationTemporaryFileSystem)}.");
        }

        _baseFileSystem = SharedRef<T>.CreateMove(ref baseFileSystem);
        _cacheManager = cacheManager;
    }

    public override void Dispose()
    {
        if (typeof(T) == typeof(SaveDataFileSystemHolder))
        {
            _cacheManager.Register(ref GetBaseFileSystemNormal());
        }
        else if (typeof(T) == typeof(ApplicationTemporaryFileSystem))
        {
            _cacheManager.Register(ref GetBaseFileSystemTemp());
        }
        else
        {
            Assert.SdkAssert(false, "Invalid save data file system type.");
        }
    }

    // Hack around not being able to use Unsafe.As on ref structs
    private ref SharedRef<SaveDataFileSystemHolder> GetBaseFileSystemNormal()
    {
        IL.Emit.Ldarg_0();
        IL.Emit.Ldflda(new FieldRef(typeof(SaveDataFileSystemCacheRegisterBase<T>), nameof(_baseFileSystem)));
        IL.Emit.Ret();
        throw IL.Unreachable();
    }

    private ref SharedRef<ApplicationTemporaryFileSystem> GetBaseFileSystemTemp()
    {
        IL.Emit.Ldarg_0();
        IL.Emit.Ldflda(new FieldRef(typeof(SaveDataFileSystemCacheRegisterBase<T>), nameof(_baseFileSystem)));
        IL.Emit.Ret();
        throw IL.Unreachable();
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
    {
        return _baseFileSystem.Get.OpenFile(ref outFile, path, mode);
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
        OpenDirectoryMode mode)
    {
        return _baseFileSystem.Get.OpenDirectory(ref outDirectory, path, mode);
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
    {
        return _baseFileSystem.Get.GetEntryType(out entryType, path);
    }

    protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option)
    {
        return _baseFileSystem.Get.CreateFile(path, size, option);
    }

    protected override Result DoDeleteFile(in Path path)
    {
        return _baseFileSystem.Get.DeleteFile(path);
    }

    protected override Result DoCreateDirectory(in Path path)
    {
        return _baseFileSystem.Get.CreateDirectory(path);
    }

    protected override Result DoDeleteDirectory(in Path path)
    {
        return _baseFileSystem.Get.DeleteDirectory(path);
    }

    protected override Result DoDeleteDirectoryRecursively(in Path path)
    {
        return _baseFileSystem.Get.DeleteDirectoryRecursively(path);
    }

    protected override Result DoCleanDirectoryRecursively(in Path path)
    {
        return _baseFileSystem.Get.CleanDirectoryRecursively(path);
    }

    protected override Result DoRenameFile(in Path currentPath, in Path newPath)
    {
        return _baseFileSystem.Get.RenameFile(currentPath, newPath);
    }

    protected override Result DoRenameDirectory(in Path currentPath, in Path newPath)
    {
        return _baseFileSystem.Get.RenameDirectory(currentPath, newPath);
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
        return _baseFileSystem.Get.GetFreeSpaceSize(out freeSpace, path);
    }

    protected override Result DoGetTotalSpaceSize(out long totalSpace, in Path path)
    {
        return _baseFileSystem.Get.GetTotalSpaceSize(out totalSpace, path);
    }
}