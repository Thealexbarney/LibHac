using System;
using LibHac.Common;
using LibHac.Fs.Impl;
using LibHac.Util;

namespace LibHac.Fs.Fsa;

public interface ICommonMountNameGenerator : IDisposable
{
    Result GenerateCommonMountName(Span<byte> nameBuffer);
}

public interface ISaveDataAttributeGetter : IDisposable
{
    Result GetSaveDataAttribute(out SaveDataAttribute attribute);
}

/// <summary>
/// Contains functions for registering and unregistering mounted <see cref="IFileSystem"/>s.
/// </summary>
/// <remarks>Based on FS 12.1.0 (nnSdk 12.3.1)</remarks>
public static class Registrar
{
    public static Result Register(this FileSystemClient fs, U8Span name, ref UniqueRef<IFileSystem> fileSystem)
    {
        using var attributeGetter = new UniqueRef<ISaveDataAttributeGetter>();
        using var mountNameGenerator = new UniqueRef<ICommonMountNameGenerator>();

        using var accessor = new UniqueRef<FileSystemAccessor>(new FileSystemAccessor(fs.Hos, name, null,
            ref fileSystem, ref mountNameGenerator.Ref(), ref attributeGetter.Ref()));

        Result rc = fs.Impl.Register(ref accessor.Ref());
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result Register(this FileSystemClient fs, U8Span name, ref UniqueRef<IFileSystem> fileSystem,
        ref UniqueRef<ICommonMountNameGenerator> mountNameGenerator)
    {
        using var attributeGetter = new UniqueRef<ISaveDataAttributeGetter>();

        using var accessor = new UniqueRef<FileSystemAccessor>(new FileSystemAccessor(fs.Hos, name, null,
            ref fileSystem, ref mountNameGenerator, ref attributeGetter.Ref()));

        Result rc = fs.Impl.Register(ref accessor.Ref());
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result Register(this FileSystemClient fs, U8Span name, IMultiCommitTarget multiCommitTarget,
        ref UniqueRef<IFileSystem> fileSystem, ref UniqueRef<ICommonMountNameGenerator> mountNameGenerator,
        bool useDataCache, bool usePathCache)
    {
        using var attributeGetter = new UniqueRef<ISaveDataAttributeGetter>();

        Result rc = Register(fs, name, multiCommitTarget, ref fileSystem, ref mountNameGenerator,
            ref attributeGetter.Ref(), useDataCache, usePathCache, new Optional<Ncm.DataId>());
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result Register(this FileSystemClient fs, U8Span name, IMultiCommitTarget multiCommitTarget,
        ref UniqueRef<IFileSystem> fileSystem, ref UniqueRef<ICommonMountNameGenerator> mountNameGenerator,
        bool useDataCache, bool usePathCache, Optional<Ncm.DataId> dataId)
    {
        using var attributeGetter = new UniqueRef<ISaveDataAttributeGetter>();

        Result rc = Register(fs, name, multiCommitTarget, ref fileSystem, ref mountNameGenerator,
            ref attributeGetter.Ref(), useDataCache, usePathCache, dataId);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result Register(this FileSystemClient fs, U8Span name, IMultiCommitTarget multiCommitTarget,
        ref UniqueRef<IFileSystem> fileSystem, ref UniqueRef<ICommonMountNameGenerator> mountNameGenerator,
        ref UniqueRef<ISaveDataAttributeGetter> saveAttributeGetter, bool useDataCache, bool usePathCache)
    {
        Result rc = Register(fs, name, multiCommitTarget, ref fileSystem, ref mountNameGenerator,
            ref saveAttributeGetter, useDataCache, usePathCache, new Optional<Ncm.DataId>());
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result Register(this FileSystemClient fs, U8Span name, IMultiCommitTarget multiCommitTarget,
        ref UniqueRef<IFileSystem> fileSystem, ref UniqueRef<ICommonMountNameGenerator> mountNameGenerator,
        ref UniqueRef<ISaveDataAttributeGetter> saveAttributeGetter, bool useDataCache, bool usePathCache,
        Optional<Ncm.DataId> dataId)
    {
        using var accessor = new UniqueRef<FileSystemAccessor>(new FileSystemAccessor(fs.Hos, name,
            multiCommitTarget, ref fileSystem, ref mountNameGenerator, ref saveAttributeGetter));

        if (!accessor.HasValue)
            return ResultFs.AllocationMemoryFailedInRegisterB.Log();

        accessor.Get.SetFileDataCacheAttachable(useDataCache);
        accessor.Get.SetPathBasedFileDataCacheAttachable(usePathCache);
        accessor.Get.SetDataId(dataId);

        Result rc = fs.Impl.Register(ref accessor.Ref());
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static void Unregister(this FileSystemClient fs, U8Span name)
    {
        fs.Impl.Unregister(name);
    }
}
