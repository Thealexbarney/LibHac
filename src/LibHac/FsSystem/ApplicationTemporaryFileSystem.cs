// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem.Save;

namespace LibHac.FsSystem;

file class ApplicationTemporaryFile : IFile
{
    private UniqueRef<IFile> _file;

    public ApplicationTemporaryFile(ref UniqueRef<IFile> file)
    {
        _file = new UniqueRef<IFile>(ref file);

        Assert.SdkRequiresNotNull(in _file);
    }

    public override void Dispose()
    {
        Assert.SdkRequiresNotNull(in _file);

        _file.Destroy();
        base.Dispose();
    }

    protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination, in ReadOption option)
    {
        Assert.SdkRequiresNotNull(in _file);

        Result res = _file.Get.Read(out bytesRead, offset, destination);

        if (res.IsFailure())
        {
            if (ResultFs.InvalidSaveDataFileReadOffset.Includes(res))
            {
                return ResultFs.OutOfRange.LogConverted(res);
            }

            return res.Miss();
        }

        return Result.Success;
    }

    protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
    {
        Assert.SdkRequiresNotNull(in _file);

        return _file.Get.Write(offset, source, in option).Ret();
    }

    protected override Result DoFlush()
    {
        Assert.SdkRequiresNotNull(in _file);

        return _file.Get.Flush().Ret();
    }

    protected override Result DoSetSize(long size)
    {
        Assert.SdkRequiresNotNull(in _file);

        return _file.Get.SetSize(size).Ret();
    }

    protected override Result DoGetSize(out long size)
    {
        Assert.SdkRequiresNotNull(in _file);

        return _file.Get.GetSize(out size).Ret();
    }

    protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        Assert.SdkRequiresNotNull(in _file);

        if (operationId == OperationId.InvalidateCache)
            return ResultFs.UnsupportedOperateRangeForApplicationTemporaryFile.Log();

        return _file.Get.OperateRange(outBuffer, operationId, offset, size, inBuffer).Ret();
    }
}

public class ApplicationTemporaryFileSystem : ISaveDataFileSystem, ISaveDataExtraDataAccessor
{
    private SharedRef<IStorage> _baseStorage;
    private IntegritySaveDataFileSystemDriver _saveFsDriver;
    private bool _isInitialized;
    private ISaveDataExtraDataAccessorObserver _cacheObserver;
    private ulong _saveDataId;
    private SaveDataSpaceId _spaceId;

    public ApplicationTemporaryFileSystem()
    {
        _baseStorage = new SharedRef<IStorage>();
        _saveFsDriver = new IntegritySaveDataFileSystemDriver();
        _isInitialized = false;
        _cacheObserver = null;
    }

    public override void Dispose()
    {
        if (_isInitialized)
        {
            _saveFsDriver.Commit().IgnoreResult();
            _saveFsDriver.FinalizeObject();
            _cacheObserver?.Unregister(_spaceId, _saveDataId);

            _isInitialized = false;
        }

        _saveFsDriver.Dispose();
        _baseStorage.Destroy();
        base.Dispose();
    }

    public Result Initialize(IStorage baseStorage, IBufferManager bufferManager, IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector)
    {
        Result res = baseStorage.GetSize(out long size);
        if (res.IsFailure()) return res.Miss();

        using var baseSubStorage = new ValueSubStorage(baseStorage, 0, size);
        res = _saveFsDriver.Initialize(in baseSubStorage, bufferManager, macGenerator, hashGeneratorFactorySelector);
        if (res.IsFailure()) return res.Miss();

        _isInitialized = true;

        return Result.Success;
    }

    public Result Initialize(ref readonly SharedRef<IStorage> baseStorage, IBufferManager bufferManager,
        IMacGenerator macGenerator, IHash256GeneratorFactorySelector hashGeneratorFactorySelector)
    {
        _baseStorage.SetByCopy(in baseStorage);

        return Initialize(_baseStorage.Get, bufferManager, macGenerator, hashGeneratorFactorySelector).Ret();
    }

    protected override Result DoCreateFile(ref readonly Path path, long size, CreateFileOptions option)
    {
        return _saveFsDriver.CreateFile(in path, size, option).Ret();
    }

    protected override Result DoDeleteFile(ref readonly Path path)
    {
        return _saveFsDriver.DeleteFile(in path).Ret();
    }

    protected override Result DoCreateDirectory(ref readonly Path path)
    {
        return _saveFsDriver.CreateDirectory(in path).Ret();
    }

    protected override Result DoDeleteDirectory(ref readonly Path path)
    {
        return _saveFsDriver.DeleteDirectory(in path).Ret();
    }

    protected override Result DoDeleteDirectoryRecursively(ref readonly Path path)
    {
        return _saveFsDriver.DeleteDirectoryRecursively(in path).Ret();
    }

    protected override Result DoCleanDirectoryRecursively(ref readonly Path path)
    {
        return _saveFsDriver.CleanDirectoryRecursively(in path).Ret();
    }

    protected override Result DoRenameFile(ref readonly Path currentPath, ref readonly Path newPath)
    {
        return _saveFsDriver.RenameFile(in currentPath, in newPath).Ret();
    }

    protected override Result DoRenameDirectory(ref readonly Path currentPath, ref readonly Path newPath)
    {
        return _saveFsDriver.RenameDirectory(in currentPath, in newPath).Ret();
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, ref readonly Path path)
    {
        return _saveFsDriver.GetEntryType(out entryType, in path).Ret();
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, ref readonly Path path, OpenMode mode)
    {
        using var file = new UniqueRef<IFile>();
        Result res = _saveFsDriver.OpenFile(ref file.Ref, in path, mode);
        if (res.IsFailure()) return res.Miss();

        using var wrapperFile = new UniqueRef<ApplicationTemporaryFile>(new ApplicationTemporaryFile(ref file.Ref));

        outFile.Set(ref wrapperFile.Ref);
        return Result.Success;
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, ref readonly Path path,
        OpenDirectoryMode mode)
    {
        return _saveFsDriver.OpenDirectory(ref outDirectory, in path, mode).Ret();
    }

    protected override Result DoCommit()
    {
        return _saveFsDriver.Commit().Ret();
    }

    protected override Result DoCommitProvisionally(long counter)
    {
        return ResultFs.UnsupportedCommitProvisionallyForApplicationTemporaryFileSystem.Log();
    }

    protected override Result DoRollback()
    {
        return Result.Success;
    }

    protected override Result DoGetFreeSpaceSize(out long freeSpace, ref readonly Path path)
    {
        return _saveFsDriver.GetFreeSpaceSize(out freeSpace, in path).Ret();
    }

    protected override Result DoGetTotalSpaceSize(out long totalSpace, ref readonly Path path)
    {
        return _saveFsDriver.GetTotalSpaceSize(out totalSpace, in path).Ret();
    }

    public override bool IsSaveDataFileSystemCacheEnabled()
    {
        return false;
    }

    public override Result RollbackOnlyModified()
    {
        return ResultFs.UnsupportedRollbackOnlyModifiedForApplicationTemporaryFileSystem.Log();
    }

    protected override Result DoGetFileSystemAttribute(out FileSystemAttribute outAttribute)
    {
        return _saveFsDriver.GetFileSystemAttribute(out outAttribute).Ret();
    }

    public override Result WriteExtraData(in SaveDataExtraData extraData)
    {
        return _saveFsDriver.WriteExtraData(in Unsafe.As<SaveDataExtraData, IntegritySaveDataFileSystem.ExtraData>(ref Unsafe.AsRef(in extraData))).Ret();
    }

    public override Result CommitExtraData(bool updateTimeStamp)
    {
        return DoCommit().Ret();
    }

    public override Result ReadExtraData(out SaveDataExtraData extraData)
    {
        UnsafeHelpers.SkipParamInit(out extraData);
        _saveFsDriver.ReadExtraData(out Unsafe.As<SaveDataExtraData, IntegritySaveDataFileSystem.ExtraData>(ref extraData));

        return Result.Success;
    }

    public override void RegisterExtraDataAccessorObserver(ISaveDataExtraDataAccessorObserver observer, SaveDataSpaceId spaceId,
        ulong saveDataId)
    {
        _cacheObserver = observer;
        _spaceId = spaceId;
        _saveDataId = saveDataId;
    }
}