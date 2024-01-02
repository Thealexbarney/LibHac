// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem.Save;

namespace LibHac.FsSystem;

file class ApplicationTemporaryFile : IFile
{
    private UniqueRef<IFile> _file;

    public ApplicationTemporaryFile(ref UniqueRef<IFile> file)
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

public class ApplicationTemporaryFileSystem : IFileSystem, ICacheableSaveDataFileSystem, ISaveDataExtraDataAccessor
{
    private SharedRef<IStorage> _baseStorage;
    private IntegritySaveDataFileSystemDriver _saveFsDriver;
    private bool _isInitialized;
    private ISaveDataExtraDataAccessorObserver _cacheObserver;
    private ulong _saveDataId;
    private SaveDataSpaceId _spaceId;

    public ApplicationTemporaryFileSystem()
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result Initialize(IStorage baseStorage, IBufferManager bufferManager, IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector)
    {
        throw new NotImplementedException();
    }

    public Result Initialize(ref readonly SharedRef<IStorage> baseStorage, IBufferManager bufferManager,
        IMacGenerator macGenerator, IHash256GeneratorFactorySelector hashGeneratorFactorySelector)
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

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, ref readonly Path path,
        OpenDirectoryMode mode)
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

    public bool IsSaveDataFileSystemCacheEnabled()
    {
        throw new NotImplementedException();
    }

    public Result RollbackOnlyModified()
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetFileSystemAttribute(out FileSystemAttribute outAttribute)
    {
        throw new NotImplementedException();
    }

    public Result WriteExtraData(in SaveDataExtraData extraData)
    {
        throw new NotImplementedException();
    }

    public Result CommitExtraData(bool updateTimeStamp)
    {
        throw new NotImplementedException();
    }

    public Result ReadExtraData(out SaveDataExtraData extraData)
    {
        throw new NotImplementedException();
    }

    public void RegisterExtraDataAccessorObserver(ISaveDataExtraDataAccessorObserver observer, SaveDataSpaceId spaceId,
        ulong saveDataId)
    {
        throw new NotImplementedException();
    }
}