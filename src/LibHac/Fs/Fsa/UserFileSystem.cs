using System;
using System.Runtime.CompilerServices;
using LibHac.Common;

namespace LibHac.Fs.Fsa
{
    public static class UserFileSystem
    {
        public static Result CreateFile(this FileSystemClient fs, U8Span path, long size)
        {
            throw new NotImplementedException();
        }

        public static Result DeleteFile(this FileSystemClient fs, U8Span path)
        {
            throw new NotImplementedException();
        }

        public static Result CreateDirectory(this FileSystemClient fs, U8Span path)
        {
            throw new NotImplementedException();
        }

        public static Result DeleteDirectory(this FileSystemClient fs, U8Span path)
        {
            throw new NotImplementedException();
        }

        public static Result DeleteDirectoryRecursively(this FileSystemClient fs, U8Span path)
        {
            throw new NotImplementedException();
        }

        public static Result CleanDirectoryRecursively(this FileSystemClient fs, U8Span path)
        {
            throw new NotImplementedException();
        }

        public static Result RenameFile(this FileSystemClient fs, U8Span oldPath, U8Span newPath)
        {
            throw new NotImplementedException();
        }

        public static Result RenameDirectory(this FileSystemClient fs, U8Span oldPath, U8Span newPath)
        {
            throw new NotImplementedException();
        }

        public static Result GetEntryType(this FileSystemClient fs, out DirectoryEntryType type, U8Span path)
        {
            throw new NotImplementedException();
        }

        public static Result GetFreeSpaceSize(this FileSystemClient fs, out long freeSpace, U8Span path)
        {
            throw new NotImplementedException();
        }

        public static Result OpenFile(this FileSystemClient fs, out FileHandle2 handle, U8Span path, OpenMode mode)
        {
            throw new NotImplementedException();
        }

        public static Result OpenFile(this FileSystemClient fs, out FileHandle2 handle, IFile file, OpenMode mode)
        {
            throw new NotImplementedException();
        }

        public static Result OpenDirectory(this FileSystemClient fs, out DirectoryHandle2 handle, U8Span path,
            OpenDirectoryMode mode)
        {
            throw new NotImplementedException();
        }

        private static Result CommitImpl(FileSystemClient fs, U8Span mountName,
            [CallerMemberName] string functionName = "")
        {
            throw new NotImplementedException();
        }

        public static Result Commit(this FileSystemClient fs, ReadOnlySpan<U8String> mountNames)
        {
            throw new NotImplementedException();
        }

        public static Result Commit(this FileSystemClient fs, U8Span mountName, CommitOption option)
        {
            throw new NotImplementedException();
        }

        public static Result Commit(this FileSystemClient fs, U8Span mountName)
        {
            throw new NotImplementedException();
        }

        public static Result CommitSaveData(this FileSystemClient fs, U8Span mountName)
        {
            throw new NotImplementedException();
        }
    }
}
