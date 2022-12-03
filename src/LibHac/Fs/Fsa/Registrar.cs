using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Impl;

namespace LibHac.Fs.Fsa;

public interface ICommonMountNameGenerator : IDisposable
{
    Result GenerateCommonMountName(Span<byte> nameBuffer);
}

public interface ISaveDataAttributeGetter : IDisposable
{
    Result GetSaveDataAttribute(out SaveDataAttribute attribute);
}

public interface IUnmountHookInvoker : IDisposable
{
    void Invoke();
}

/// <summary>
/// Contains functions for registering and unregistering mounted <see cref="IFileSystem"/>s.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public static class Registrar
{
    private class UnmountHookFileSystem : IFileSystem
    {
        private UniqueRef<IFileSystem> _fileSystem;
        private UniqueRef<IUnmountHookInvoker> _unmountHookInvoker;

        public UnmountHookFileSystem(ref UniqueRef<IFileSystem> fileSystem,
            ref UniqueRef<IUnmountHookInvoker> unmountHookInvoker)
        {
            _fileSystem = new UniqueRef<IFileSystem>(ref fileSystem);
            _unmountHookInvoker = new UniqueRef<IUnmountHookInvoker>(ref unmountHookInvoker);
        }

        public override void Dispose()
        {
            if (_unmountHookInvoker.HasValue)
                _unmountHookInvoker.Get.Invoke();

            _unmountHookInvoker.Destroy();
            _fileSystem.Destroy();

            base.Dispose();
        }

        protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option) =>
            _fileSystem.Get.CreateFile(in path, size, option);

        protected override Result DoDeleteFile(in Path path) => _fileSystem.Get.DeleteFile(in path);

        protected override Result DoCreateDirectory(in Path path) => _fileSystem.Get.CreateDirectory(in path);

        protected override Result DoDeleteDirectory(in Path path) => _fileSystem.Get.DeleteDirectory(in path);

        protected override Result DoDeleteDirectoryRecursively(in Path path) =>
            _fileSystem.Get.DeleteDirectoryRecursively(in path);

        protected override Result DoCleanDirectoryRecursively(in Path path) =>
            _fileSystem.Get.CleanDirectoryRecursively(in path);

        protected override Result DoRenameFile(in Path currentPath, in Path newPath) =>
            _fileSystem.Get.RenameFile(in currentPath, in newPath);

        protected override Result DoRenameDirectory(in Path currentPath, in Path newPath) =>
            _fileSystem.Get.RenameDirectory(in currentPath, in newPath);

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path) =>
            _fileSystem.Get.GetEntryType(out entryType, in path);

        protected override Result DoGetFreeSpaceSize(out long freeSpace, in Path path) =>
            _fileSystem.Get.GetFreeSpaceSize(out freeSpace, in path);

        protected override Result DoGetTotalSpaceSize(out long totalSpace, in Path path) =>
            _fileSystem.Get.GetTotalSpaceSize(out totalSpace, in path);

        protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode) =>
            _fileSystem.Get.OpenFile(ref outFile, in path, mode);

        protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
            OpenDirectoryMode mode) =>
            _fileSystem.Get.OpenDirectory(ref outDirectory, in path, mode);

        protected override Result DoCommit() => _fileSystem.Get.Commit();

        protected override Result DoCommitProvisionally(long counter) =>
            _fileSystem.Get.CommitProvisionally(counter);

        protected override Result DoRollback() => _fileSystem.Get.Rollback();

        protected override Result DoFlush() => _fileSystem.Get.Flush();

        protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in Path path) =>
            _fileSystem.Get.GetFileTimeStampRaw(out timeStamp, in path);

        protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
            in Path path) => _fileSystem.Get.QueryEntry(outBuffer, inBuffer, queryId, in path);
    }

    public static Result Register(this FileSystemClient fs, U8Span name, ref UniqueRef<IFileSystem> fileSystem)
    {
        using var attributeGetter = new UniqueRef<ISaveDataAttributeGetter>();
        using var mountNameGenerator = new UniqueRef<ICommonMountNameGenerator>();

        using var accessor = new UniqueRef<FileSystemAccessor>(new FileSystemAccessor(fs.Hos, name, null,
            ref fileSystem, ref mountNameGenerator.Ref, ref attributeGetter.Ref));

        Result res = fs.Impl.Register(ref accessor.Ref);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result Register(this FileSystemClient fs, U8Span name, ref UniqueRef<IFileSystem> fileSystem,
        ref UniqueRef<ICommonMountNameGenerator> mountNameGenerator)
    {
        using var attributeGetter = new UniqueRef<ISaveDataAttributeGetter>();

        using var accessor = new UniqueRef<FileSystemAccessor>(new FileSystemAccessor(fs.Hos, name, null,
            ref fileSystem, ref mountNameGenerator, ref attributeGetter.Ref));

        Result res = fs.Impl.Register(ref accessor.Ref);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result Register(this FileSystemClient fs, U8Span name, IMultiCommitTarget multiCommitTarget,
        ref UniqueRef<IFileSystem> fileSystem, ref UniqueRef<ICommonMountNameGenerator> mountNameGenerator,
        bool useDataCache, IStorage storageForPurgeFileDataCache, bool usePathCache)
    {
        using var unmountHookInvoker = new UniqueRef<IUnmountHookInvoker>();
        using var attributeGetter = new UniqueRef<ISaveDataAttributeGetter>();

        Result res = Register(fs, name, multiCommitTarget, ref fileSystem, ref mountNameGenerator,
            ref attributeGetter.Ref, useDataCache, storageForPurgeFileDataCache, usePathCache,
            ref unmountHookInvoker.Ref);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result Register(this FileSystemClient fs, U8Span name, IMultiCommitTarget multiCommitTarget,
        ref UniqueRef<IFileSystem> fileSystem, ref UniqueRef<ICommonMountNameGenerator> mountNameGenerator,
        bool useDataCache, IStorage storageForPurgeFileDataCache, bool usePathCache,
        ref UniqueRef<IUnmountHookInvoker> unmountHook)
    {
        using var attributeGetter = new UniqueRef<ISaveDataAttributeGetter>();

        Result res = Register(fs, name, multiCommitTarget, ref fileSystem, ref mountNameGenerator,
            ref attributeGetter.Ref, useDataCache, storageForPurgeFileDataCache, usePathCache, ref unmountHook);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result Register(this FileSystemClient fs, U8Span name, IMultiCommitTarget multiCommitTarget,
        ref UniqueRef<IFileSystem> fileSystem, ref UniqueRef<ICommonMountNameGenerator> mountNameGenerator,
        ref UniqueRef<ISaveDataAttributeGetter> saveAttributeGetter, bool useDataCache,
        IStorage storageForPurgeFileDataCache, bool usePathCache)
    {
        using var unmountHookInvoker = new UniqueRef<IUnmountHookInvoker>();

        Result res = Register(fs, name, multiCommitTarget, ref fileSystem, ref mountNameGenerator,
            ref saveAttributeGetter, useDataCache, storageForPurgeFileDataCache, usePathCache,
            ref unmountHookInvoker.Ref);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result Register(this FileSystemClient fs, U8Span name, IMultiCommitTarget multiCommitTarget,
        ref UniqueRef<IFileSystem> fileSystem, ref UniqueRef<ICommonMountNameGenerator> mountNameGenerator,
        ref UniqueRef<ISaveDataAttributeGetter> saveAttributeGetter, bool useDataCache,
        IStorage storageForPurgeFileDataCache, bool usePathCache, ref UniqueRef<IUnmountHookInvoker> unmountHook)
    {
        if (useDataCache)
            Assert.SdkAssert(storageForPurgeFileDataCache is not null);

        using (var unmountHookFileSystem =
               new UniqueRef<UnmountHookFileSystem>(new UnmountHookFileSystem(ref fileSystem, ref unmountHook)))
        {
            fileSystem.Set(ref unmountHookFileSystem.Ref);
        }

        if (!fileSystem.HasValue)
            return ResultFs.AllocationMemoryFailedInRegisterB.Log();

        using var accessor = new UniqueRef<FileSystemAccessor>(new FileSystemAccessor(fs.Hos, name,
            multiCommitTarget, ref fileSystem, ref mountNameGenerator, ref saveAttributeGetter));

        if (!accessor.HasValue)
            return ResultFs.AllocationMemoryFailedInRegisterB.Log();

        accessor.Get.SetFileDataCacheAttachable(useDataCache, storageForPurgeFileDataCache);
        accessor.Get.SetPathBasedFileDataCacheAttachable(usePathCache);

        Result res = fs.Impl.Register(ref accessor.Ref);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static void Unregister(this FileSystemClient fs, U8Span name)
    {
        fs.Impl.Unregister(name);
    }
}