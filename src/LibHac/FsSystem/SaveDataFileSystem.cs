// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem.Save;
using LibHac.Os;

namespace LibHac.FsSystem;

file class SaveDataFile : IFile
{
    private UniqueRef<IFile> _file;

    public SaveDataFile(ref UniqueRef<IFile> file)
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination, in ReadOption option)
    {
        throw new NotImplementedException();
    }

    protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
    {
        throw new NotImplementedException();
    }

    protected override Result DoFlush()
    {
        throw new NotImplementedException();
    }

    protected override Result DoSetSize(long size)
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetSize(out long size)
    {
        throw new NotImplementedException();
    }

    protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }
}

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
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    public static Result ExtractParameters(out JournalIntegritySaveDataParameters outParam, IStorage saveStorage,
        IBufferManager bufferManager, IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector, uint minimumVersion)
    {
        throw new NotImplementedException();
    }

    public Result Initialize(IStorage baseStorage, IBufferManager bufferManager, IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector, uint minimumVersion, bool canCommitProvisionally)
    {
        throw new NotImplementedException();
    }

    public Result Initialize(IStorage baseStorage, IBufferManager bufferManager, IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector, ISaveDataCommitTimeStampGetter timeStampGetter,
        RandomDataGenerator randomGenerator, uint minimumVersion, bool canCommitProvisionally)
    {
        throw new NotImplementedException();
    }

    public Result Initialize(ref readonly SharedRef<IStorage> baseStorage, IBufferManager bufferManager,
        IMacGenerator macGenerator, IHash256GeneratorFactorySelector hashGeneratorFactorySelector, uint minimumVersion,
        bool canCommitProvisionally)
    {
        throw new NotImplementedException();
    }

    public Result Initialize(ref readonly SharedRef<IStorage> baseStorage, IBufferManager bufferManager,
        IMacGenerator macGenerator, IHash256GeneratorFactorySelector hashGeneratorFactorySelector,
        ISaveDataCommitTimeStampGetter timeStampGetter, RandomDataGenerator randomGenerator, uint minimumVersion,
        bool canCommitProvisionally)
    {
        throw new NotImplementedException();
    }

    protected override Result DoCreateFile(ref readonly Path path, long size, CreateFileOptions option)
    {
        throw new NotImplementedException();
    }

    protected override Result DoDeleteFile(ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoCreateDirectory(ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoDeleteDirectory(ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoDeleteDirectoryRecursively(ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoCleanDirectoryRecursively(ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoRenameFile(ref readonly Path currentPath, ref readonly Path newPath)
    {
        throw new NotImplementedException();
    }

    protected override Result DoRenameDirectory(ref readonly Path currentPath, ref readonly Path newPath)
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, ref readonly Path path, OpenMode mode)
    {
        throw new NotImplementedException();
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, ref readonly Path path, OpenDirectoryMode mode)
    {
        throw new NotImplementedException();
    }

    private Result DoCommit(bool updateTimeStamp)
    {
        throw new NotImplementedException();
    }

    protected override Result DoCommit()
    {
        throw new NotImplementedException();
    }

    protected override Result DoCommitProvisionally(long counter)
    {
        throw new NotImplementedException();
    }

    protected override Result DoRollback()
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetFreeSpaceSize(out long freeSpace, ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetTotalSpaceSize(out long totalSpace, ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetFileSystemAttribute(out FileSystemAttribute outAttribute)
    {
        throw new NotImplementedException();
    }

    public override Result WriteExtraData(in SaveDataExtraData extraData)
    {
        throw new NotImplementedException();
    }

    public override Result CommitExtraData(bool updateTimeStamp)
    {
        throw new NotImplementedException();
    }

    public override Result ReadExtraData(out SaveDataExtraData extraData)
    {
        throw new NotImplementedException();
    }

    public override void RegisterExtraDataAccessorObserver(ISaveDataExtraDataAccessorObserver observer, SaveDataSpaceId spaceId, ulong saveDataId)
    {
        throw new NotImplementedException();
    }

    public override bool IsSaveDataFileSystemCacheEnabled()
    {
        throw new NotImplementedException();
    }

    public override Result RollbackOnlyModified()
    {
        throw new NotImplementedException();
    }

    public IInternalStorageFileSystem GetInternalStorageFileSystem()
    {
        throw new NotImplementedException();
    }
}