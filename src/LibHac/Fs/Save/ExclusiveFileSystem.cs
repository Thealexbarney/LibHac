// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.Os;

namespace LibHac.Fs.Save;

public interface IExclusiveObject
{
    long GetId();
    IExclusiveObject GetNext();
    void SetId(long id);
    void SetNext(IExclusiveObject nextObject);

    OpenMode GetMode();
    bool IsFile();
    Result NotifyDelete(long id, bool isFile);
    void NotifyFileSystemDestructed();
}

// ReSharper disable once InconsistentNaming
public abstract class IExclusiveDirectoryBase : IDirectory
{
    public abstract Result NotifyDelete(long id, bool isFile);
}

// ReSharper disable once InconsistentNaming
public abstract class IExclusiveFileBase : IFile
{
    public abstract OpenMode GetMode();
}

// ReSharper disable once InconsistentNaming
public abstract class IExclusiveFileSystemBase : IFileSystem
{
    public abstract Result OpenBaseFile(ref UniqueRef<IExclusiveFileBase> outFile, ref readonly Path path, OpenMode mode);
    public abstract Result OpenBaseDirectory(ref UniqueRef<IExclusiveDirectoryBase> outDirectory, ref readonly Path path, OpenDirectoryMode mode);
    public abstract Result GetFileIdFromPath(out long outId, ref readonly Path path, bool isFile);
    public abstract Result CheckSubEntry(out bool isSubEntry, long baseEntryId, long entryIdToCheck, bool isFile);
}

public class ExclusiveFile : IFile, IExclusiveObject
{
    // Pasting the contents of IExclusiveObject directly in this class because C# doesn't have multiple inheritance.
    private IExclusiveObject _next;
    private long _id;

    public long GetId() => _id;
    public IExclusiveObject GetNext() => _next;
    public void SetId(long id) => _id = id;
    public void SetNext(IExclusiveObject nextObject) => _next = nextObject;

    // Contents of ExclusiveFile
    private ExclusiveFileSystem _fileSystem;
    private UniqueRef<IExclusiveFileBase> _baseFile;
    private SdkMutex _fileOperationLock;

    public ExclusiveFile(ref UniqueRef<IExclusiveFileBase> baseFile, long id, ExclusiveFileSystem fileSystem,
        SdkMutex fileOperationLock)
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    public OpenMode GetMode()
    {
        throw new NotImplementedException();
    }

    public bool IsFile()
    {
        throw new NotImplementedException();
    }

    public Result NotifyDelete(long id, bool isFile)
    {
        throw new NotImplementedException();
    }

    public void NotifyFileSystemDestructed()
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

public class ExclusiveDirectory : IDirectory, IExclusiveObject
{
    // Pasting the contents of IExclusiveObject directly in this class because C# doesn't have multiple inheritance.
    private IExclusiveObject _next;
    private long _id;

    public long GetId() => _id;
    public IExclusiveObject GetNext() => _next;
    public void SetId(long id) => _id = id;
    public void SetNext(IExclusiveObject nextObject) => _next = nextObject;

    // Contents of ExclusiveDirectory
    private ExclusiveFileSystem _fileSystem;
    private UniqueRef<IExclusiveDirectoryBase> _baseDirectory;
    private SdkMutex _fileOperationLock;

    public ExclusiveDirectory(ref UniqueRef<IExclusiveDirectoryBase> baseDirectory, long id,
        ExclusiveFileSystem fileSystem, SdkMutex fileOperationLock)
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    public OpenMode GetMode()
    {
        throw new NotImplementedException();
    }

    public bool IsFile()
    {
        throw new NotImplementedException();
    }

    protected override Result DoRead(out long entriesRead, Span<DirectoryEntry> entryBuffer)
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetEntryCount(out long entryCount)
    {
        throw new NotImplementedException();
    }

    public Result NotifyDelete(long id, bool isFile)
    {
        throw new NotImplementedException();
    }

    public void NotifyFileSystemDestructed()
    {
        throw new NotImplementedException();
    }
}

public class ExclusiveFileSystem : IFileSystem
{
    private SdkMutex _lockObject;
    private IExclusiveObject _openedObjects;
    private IExclusiveFileSystemBase _baseFileSystem;

    public ExclusiveFileSystem()
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result Initialize(IExclusiveFileSystemBase baseFileSystem)
    {
        throw new NotImplementedException();
    }

    protected override Result DoCreateFile(ref readonly Path path, long size, CreateFileOptions option)
    {
        throw new NotImplementedException();
    }

    protected override Result DoCreateDirectory(ref readonly Path path)
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

    protected override Result DoDeleteFile(ref readonly Path path)
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

    protected override Result DoCommit()
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

    public bool HasOpenedFiles(int openMode)
    {
        throw new NotImplementedException();
    }

    public bool HasOpenedEntries(out bool outIsFirstEntryFile)
    {
        throw new NotImplementedException();
    }

    private Result CheckAccessPolicy(out long outId, ref readonly Path path, int openMode, bool isFile, bool checkSubEntries)
    {
        throw new NotImplementedException();
    }

    private Result CheckAccessPolicy(out long outId, out bool outDirectoryOpened, ref readonly Path path, int openMode,
        bool isFile, bool checkSubEntries)
    {
        throw new NotImplementedException();
    }

    private bool CheckEntryExistence(ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    private void LinkObject(IExclusiveObject obj)
    {
        throw new NotImplementedException();
    }

    private void UnlinkObject(IExclusiveObject obj)
    {
        throw new NotImplementedException();
    }

    private Result DeleteDirectoryRecursivelyInternal(ref readonly Path path, bool deleteRootDir)
    {
        throw new NotImplementedException();
    }
}