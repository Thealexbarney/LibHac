using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem;

public class ApplicationTemporaryFileSystem : IFileSystem, ISaveDataExtraDataAccessor
{
    protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option)
    {
        throw new NotImplementedException();
    }

    protected override Result DoDeleteFile(in Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoCreateDirectory(in Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoDeleteDirectory(in Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoDeleteDirectoryRecursively(in Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoCleanDirectoryRecursively(in Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoRenameFile(in Path currentPath, in Path newPath)
    {
        throw new NotImplementedException();
    }

    protected override Result DoRenameDirectory(in Path currentPath, in Path newPath)
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
    {
        throw new NotImplementedException();
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
        OpenDirectoryMode mode)
    {
        throw new NotImplementedException();
    }

    protected override Result DoCommit()
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