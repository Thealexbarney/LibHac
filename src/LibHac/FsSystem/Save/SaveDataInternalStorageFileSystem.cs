// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem.Save;

public interface IInternalStorageFileSystemVisitor : IDisposable
{
    Result Visit(U8Span name, ReadOnlySpan<byte> buffer);
    Result Visit(U8Span name, IStorage storage);
}

// ReSharper disable once InconsistentNaming
public interface InternalStorageFileSystemHolder
{
    IInternalStorageFileSystem GetInternalStorageFileSystem();
}

public interface IInternalStorageFileSystem
{
    Result AcceptVisitor(IInternalStorageFileSystemVisitor visitor);
    Result WriteExtraData(in JournalIntegritySaveDataFileSystem.ExtraData extraData);
    Result ReadExtraData(out JournalIntegritySaveDataFileSystem.ExtraData outExtraData);
    Result CommitFileSystem();
    Result UpdateMac(IMacGenerator macGenerator);
}

file class SaveDataInternalStorageFileSystemAccessor : IInternalStorageFileSystemVisitor
{
    private bool _isOperationDone;
    private Func<Result> _updateMacFunc;

    public SaveDataInternalStorageFileSystemAccessor(Func<Result> updateMacFunc)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result Visit(U8Span name, ReadOnlySpan<byte> buffer)
    {
        throw new NotImplementedException();
    }

    public Result Visit(U8Span name, IStorage storage)
    {
        throw new NotImplementedException();
    }
}

file class SaveDataInternalStorageFreeBitmap : IInternalStorageFileSystemVisitor
{
    private Func<byte[], Result> _freeBitmapFunc;

    public SaveDataInternalStorageFreeBitmap(Func<byte[], Result> freeBitmapFunc)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result Visit(U8Span name, ReadOnlySpan<byte> buffer)
    {
        throw new NotImplementedException();
    }

    public Result Visit(U8Span name, IStorage storage)
    {
        throw new NotImplementedException();
    }
}

file class SaveDataExtraDataInternalStorageFile : IFile
{
    private IInternalStorageFileSystem _fileSystem;
    private OpenMode _mode;
    private Array64<byte> _name;

    public SaveDataExtraDataInternalStorageFile(IInternalStorageFileSystem fileSystem, U8Span name, OpenMode mode)
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

file class MemoryFile : IFile
{
    private byte[] _buffer;
    private int _size;
    private OpenMode _mode;

    public MemoryFile(byte[] buffer, OpenMode mode)
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

    protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }
}

public class SaveDataInternalStorageFileSystem : IFileSystem, ICacheableSaveDataFileSystem, ISaveDataExtraDataAccessor
{
    private IInternalStorageFileSystem _internalStorageFileSystem;
    private IMacGenerator _normalMacGenerator;
    private IMacGenerator _temporaryMacGenerator;
    private IHash256GeneratorFactorySelector _hashGeneratorFactorySelector;
    private Array32<byte> _fileNameSha2Salt;

    public SaveDataInternalStorageFileSystem()
    {
        throw new NotImplementedException();
    }

    public Result Initialize(IInternalStorageFileSystem internalStorageFileSystem, IMacGenerator normalMacGenerator,
        IMacGenerator temporaryMacGenerator, IHash256GeneratorFactorySelector hashGeneratorFactorySelector)
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

    protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
        ref readonly Path path)
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

    public void RegisterExtraDataAccessorObserver(ISaveDataExtraDataAccessorObserver observer, SaveDataSpaceId spaceId, ulong saveDataId)
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
}