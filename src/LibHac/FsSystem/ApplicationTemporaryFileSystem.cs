using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem
{
    public class ApplicationTemporaryFileSystem : IFileSystem, ISaveDataExtraDataAccessor
    {
        protected override Result DoCreateFile(U8Span path, long size, CreateFileOptions option)
        {
            throw new NotImplementedException();
        }

        protected override Result DoDeleteFile(U8Span path)
        {
            throw new NotImplementedException();
        }

        protected override Result DoCreateDirectory(U8Span path)
        {
            throw new NotImplementedException();
        }

        protected override Result DoDeleteDirectory(U8Span path)
        {
            throw new NotImplementedException();
        }

        protected override Result DoDeleteDirectoryRecursively(U8Span path)
        {
            throw new NotImplementedException();
        }

        protected override Result DoCleanDirectoryRecursively(U8Span path)
        {
            throw new NotImplementedException();
        }

        protected override Result DoRenameFile(U8Span oldPath, U8Span newPath)
        {
            throw new NotImplementedException();
        }

        protected override Result DoRenameDirectory(U8Span oldPath, U8Span newPath)
        {
            throw new NotImplementedException();
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, U8Span path)
        {
            throw new NotImplementedException();
        }

        protected override Result DoOpenFile(out IFile file, U8Span path, OpenMode mode)
        {
            throw new NotImplementedException();
        }

        protected override Result DoOpenDirectory(out IDirectory directory, U8Span path, OpenDirectoryMode mode)
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

        public void RegisterCacheObserver(ISaveDataExtraDataAccessorCacheObserver observer, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            throw new NotImplementedException();
        }
    }
}
