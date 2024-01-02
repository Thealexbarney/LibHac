// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.FsSystem.Save;

namespace LibHac.Fs.Save;

public class SaveDataFileSystemCore : IFileSystem
{
    private ExclusiveFileSystem _exclusiveFs;
    private SaveDataFileSystemCoreImpl _saveFsImpl;

    public SaveDataFileSystemCore()
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    public static long QueryDataSize(long blockSize, ulong blockCount)
    {
        throw new NotImplementedException();
    }

    public static long QueryMetaSize(long blockSize, ulong blockCount)
    {
        throw new NotImplementedException();
    }

    public static Result Format(in ValueSubStorage storageControlArea, in ValueSubStorage storageMeta,
        in ValueSubStorage storageData, long blockSize, IBufferManager bufferManager)
    {
        throw new NotImplementedException();
    }

    public static Result ExpandControlArea(in ValueSubStorage storageControlArea, long sizeDataNew)
    {
        throw new NotImplementedException();
    }

    public static Result ExpandMeta(in ValueSubStorage storageMeta, long blockSize, long sizeDataOld, long sizeDataNew)
    {
        throw new NotImplementedException();
    }

    public Result Initialize(in ValueSubStorage storageControlArea, in ValueSubStorage storageMeta,
        in ValueSubStorage storageData, IBufferManager bufferManager)
    {
        throw new NotImplementedException();
    }

    public void FinalizeObject()
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

    public Result AcceptVisitor(IInternalStorageFileSystemVisitor visitor)
    {
        throw new NotImplementedException();
    }
}