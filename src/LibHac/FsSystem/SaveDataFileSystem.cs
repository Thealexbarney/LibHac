using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem.Save;
using LibHac.Os;

namespace LibHac.FsSystem;

/// <summary>
/// Wraps an <see cref="IFile"/> opened by a <see cref="SaveDataFileSystem"/>
/// </summary>
/// <remarks>Based on nnSdk 17.5.0 (FS 17.0.0)</remarks>
file class SaveDataFile : IFile
{
    private UniqueRef<IFile> _file;

    public SaveDataFile(ref UniqueRef<IFile> file)
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
            return ResultFs.UnsupportedOperateRangeForSaveDataFile.Log();

        return _file.Get.OperateRange(outBuffer, operationId, offset, size, inBuffer).Ret();
    }
}

/// <summary>
/// Reads and writes to the file system inside a journal integrity save data image file.
/// </summary>
/// <remarks>Based on nnSdk 17.5.0 (FS 17.0.0)</remarks>
public class SaveDataFileSystem : ISaveDataFileSystem, InternalStorageFileSystemHolder
{
    private SharedRef<IStorage> _baseStorage;
    private JournalIntegritySaveDataFileSystemDriver _saveFsDriver;
    private ISaveDataExtraDataAccessorObserver _cacheObserver;
    private ulong _saveDataId;
    private SaveDataSpaceId _spaceId;
    private ISaveDataCommitTimeStampGetter _commitTimeStampGetter;
    private RandomDataGenerator _randomGeneratorForCommit;
    private SdkMutex _mutex;
    private bool _canCommitProvisionally;

    public SaveDataFileSystem()
    {
        _baseStorage = new SharedRef<IStorage>();
        _saveFsDriver = new JournalIntegritySaveDataFileSystemDriver();
        _cacheObserver = null;
        _mutex = new SdkMutex();
        _canCommitProvisionally = false;
    }

    public override void Dispose()
    {
        _saveFsDriver.FinalizeObject();
        _cacheObserver?.Unregister(_spaceId, _saveDataId);

        _saveFsDriver.Dispose();
        _baseStorage.Destroy();
        base.Dispose();
    }

    public static Result ExtractParameters(out JournalIntegritySaveDataParameters outParam, IStorage saveStorage,
        IBufferManager bufferManager, IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector, uint minimumVersion)
    {
        UnsafeHelpers.SkipParamInit(out outParam);

        using var fileSystem = new JournalIntegritySaveDataFileSystemDriver();

        Result res = saveStorage.GetSize(out long size);
        if (res.IsFailure()) return res.Miss();

        using var saveSubStorage = new ValueSubStorage(saveStorage, 0, size);
        res = fileSystem.Initialize(in saveSubStorage, bufferManager, macGenerator, hashGeneratorFactorySelector, minimumVersion);
        if (res.IsFailure()) return res.Miss();

        fileSystem.ExtractParameters(out outParam);
        return Result.Success;
    }

    public Result Initialize(IStorage baseStorage, IBufferManager bufferManager, IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector, uint minimumVersion, bool canCommitProvisionally)
    {
        return Initialize(baseStorage, bufferManager, macGenerator, hashGeneratorFactorySelector, timeStampGetter: null,
            randomGenerator: null, minimumVersion, canCommitProvisionally).Ret();
    }

    public Result Initialize(IStorage baseStorage, IBufferManager bufferManager, IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector, ISaveDataCommitTimeStampGetter timeStampGetter,
        RandomDataGenerator randomGenerator, uint minimumVersion, bool canCommitProvisionally)
    {
        Result res = baseStorage.GetSize(out long size);
        if (res.IsFailure()) return res.Miss();

        using var baseSubStorage = new ValueSubStorage(baseStorage, 0, size);
        res = _saveFsDriver.Initialize(in baseSubStorage, bufferManager, macGenerator, hashGeneratorFactorySelector, minimumVersion);
        if (res.IsFailure()) return res.Miss();

        _commitTimeStampGetter = timeStampGetter;
        _randomGeneratorForCommit = randomGenerator;
        _canCommitProvisionally = canCommitProvisionally;

        return Result.Success;
    }

    public Result Initialize(ref readonly SharedRef<IStorage> baseStorage, IBufferManager bufferManager,
        IMacGenerator macGenerator, IHash256GeneratorFactorySelector hashGeneratorFactorySelector, uint minimumVersion,
        bool canCommitProvisionally)
    {
        _baseStorage.SetByCopy(in baseStorage);

        return Initialize(_baseStorage.Get, bufferManager, macGenerator, hashGeneratorFactorySelector, minimumVersion,
            canCommitProvisionally).Ret();
    }

    public Result Initialize(ref readonly SharedRef<IStorage> baseStorage, IBufferManager bufferManager,
        IMacGenerator macGenerator, IHash256GeneratorFactorySelector hashGeneratorFactorySelector,
        ISaveDataCommitTimeStampGetter timeStampGetter, RandomDataGenerator randomGenerator, uint minimumVersion,
        bool canCommitProvisionally)
    {
        _baseStorage.SetByCopy(in baseStorage);

        return Initialize(_baseStorage.Get, bufferManager, macGenerator, hashGeneratorFactorySelector, timeStampGetter,
            randomGenerator, minimumVersion, canCommitProvisionally).Ret();
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

        using var wrapperFile = new UniqueRef<SaveDataFile>(new SaveDataFile(ref file.Ref));

        outFile.Set(ref wrapperFile.Ref);
        return Result.Success;
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, ref readonly Path path, OpenDirectoryMode mode)
    {
        return _saveFsDriver.OpenDirectory(ref outDirectory, in path, mode).Ret();
    }

    private Result DoCommit(bool updateTimeStamp)
    {
        if (updateTimeStamp && _commitTimeStampGetter is not null)
        {
            Assert.SdkNotNull(_randomGeneratorForCommit);

            Result res = ReadExtraData(out SaveDataExtraData extraData);
            if (res.IsFailure()) return res.Miss();

            res = _commitTimeStampGetter.Get(out long timeStamp);
            if (res.IsSuccess())
                extraData.TimeStamp = timeStamp;

            long commitId = 0;
            do
            {
                _randomGeneratorForCommit(SpanHelpers.AsByteSpan(ref commitId));
            } while (commitId == 0 || commitId == extraData.CommitId);

            extraData.CommitId = commitId;

            res = WriteExtraData(in extraData);
            if (res.IsFailure()) return res.Miss();
        }

        return _saveFsDriver.Commit().Ret();
    }

    protected override Result DoCommit()
    {
        return DoCommit(updateTimeStamp: true).Ret();
    }

    public long GetCounterForBundledCommit()
    {
        return _saveFsDriver.GetCounterForBundledCommit();
    }

    protected override Result DoCommitProvisionally(long counter)
    {
        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        if (!_canCommitProvisionally)
            return ResultFs.UnsupportedCommitProvisionallyForSaveDataFileSystem.Log();

        return _saveFsDriver.CommitProvisionally(counter).Ret();
    }

    protected override Result DoRollback()
    {
        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        return _saveFsDriver.Rollback().Ret();
    }

    protected override Result DoGetFreeSpaceSize(out long freeSpace, ref readonly Path path)
    {
        return _saveFsDriver.GetFreeSpaceSize(out freeSpace, in path).Ret();
    }

    protected override Result DoGetTotalSpaceSize(out long totalSpace, ref readonly Path path)
    {
        return _saveFsDriver.GetTotalSpaceSize(out totalSpace, in path).Ret();
    }

    protected override Result DoGetFileSystemAttribute(out FileSystemAttribute outAttribute)
    {
        return _saveFsDriver.GetFileSystemAttribute(out outAttribute).Ret();
    }

    public override Result WriteExtraData(in SaveDataExtraData extraData)
    {
        return _saveFsDriver.WriteExtraData(in Unsafe.As<SaveDataExtraData, JournalIntegritySaveDataFileSystem.ExtraData>(ref Unsafe.AsRef(in extraData))).Ret();
    }

    public override Result CommitExtraData(bool updateTimeStamp)
    {
        return DoCommit(updateTimeStamp).Ret();
    }

    public override Result ReadExtraData(out SaveDataExtraData extraData)
    {
        UnsafeHelpers.SkipParamInit(out extraData);
        _saveFsDriver.ReadExtraData(out Unsafe.As<SaveDataExtraData, JournalIntegritySaveDataFileSystem.ExtraData>(ref extraData));

        return Result.Success;
    }

    public override void RegisterExtraDataAccessorObserver(ISaveDataExtraDataAccessorObserver observer,
        SaveDataSpaceId spaceId, ulong saveDataId)
    {
        _cacheObserver = observer;
        _spaceId = spaceId;
        _saveDataId = saveDataId;
    }

    public override bool IsSaveDataFileSystemCacheEnabled()
    {
        return true;
    }

    public override Result RollbackOnlyModified()
    {
        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        return _saveFsDriver.RollbackOnlyModified().Ret();
    }

    public IInternalStorageFileSystem GetInternalStorageFileSystem()
    {
        return _saveFsDriver;
    }
}