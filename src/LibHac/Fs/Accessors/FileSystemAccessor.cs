using System;
using System.Collections.Generic;
using System.Linq;
using LibHac.Common;
using LibHac.Fs.Fsa;

namespace LibHac.Fs.Accessors
{
    public class FileSystemAccessor
    {
        public string Name { get; }

        private IFileSystem FileSystem { get; }
        internal FileSystemClient FsClient { get; }
        private ICommonMountNameGenerator MountNameGenerator { get; }

        private HashSet<FileAccessor> OpenFiles { get; } = new HashSet<FileAccessor>();
        private HashSet<DirectoryAccessor> OpenDirectories { get; } = new HashSet<DirectoryAccessor>();

        private readonly object _locker = new object();

        internal bool IsAccessLogEnabled { get; set; }

        public FileSystemAccessor(string name, IFileSystem baseFileSystem, FileSystemClient fsClient, ICommonMountNameGenerator nameGenerator)
        {
            Name = name;
            FileSystem = baseFileSystem;
            FsClient = fsClient;
            MountNameGenerator = nameGenerator;
        }

        public Result CreateDirectory(U8Span path)
        {
            return FileSystem.CreateDirectory(path);
        }

        public Result CreateFile(U8Span path, long size, CreateFileOptions options)
        {
            return FileSystem.CreateFile(path, size, options);
        }

        public Result DeleteDirectory(U8Span path)
        {
            return FileSystem.DeleteDirectory(path);
        }

        public Result DeleteDirectoryRecursively(U8Span path)
        {
            return FileSystem.DeleteDirectoryRecursively(path);
        }

        public Result CleanDirectoryRecursively(U8Span path)
        {
            return FileSystem.CleanDirectoryRecursively(path);
        }

        public Result DeleteFile(U8Span path)
        {
            return FileSystem.DeleteFile(path);
        }

        public Result OpenDirectory(out DirectoryAccessor directory, U8Span path, OpenDirectoryMode mode)
        {
            directory = default;

            Result rc = FileSystem.OpenDirectory(out IDirectory rawDirectory, path, mode);
            if (rc.IsFailure()) return rc;

            var accessor = new DirectoryAccessor(rawDirectory, this);

            lock (_locker)
            {
                OpenDirectories.Add(accessor);
            }

            directory = accessor;
            return Result.Success;
        }

        public Result OpenFile(out FileAccessor file, U8Span path, OpenMode mode)
        {
            file = default;

            Result rc = FileSystem.OpenFile(out IFile rawFile, path, mode);
            if (rc.IsFailure()) return rc;

            var accessor = new FileAccessor(rawFile, this, mode);

            lock (_locker)
            {
                OpenFiles.Add(accessor);
            }

            file = accessor;
            return Result.Success;
        }

        public Result RenameDirectory(U8Span oldPath, U8Span newPath)
        {
            return FileSystem.RenameDirectory(oldPath, newPath);
        }

        public Result RenameFile(U8Span oldPath, U8Span newPath)
        {
            return FileSystem.RenameFile(oldPath, newPath);
        }

        public Result GetEntryType(out DirectoryEntryType type, U8Span path)
        {
            return FileSystem.GetEntryType(out type, path);
        }

        public Result GetFreeSpaceSize(out long freeSpace, U8Span path)
        {
            return FileSystem.GetFreeSpaceSize(out freeSpace, path);
        }

        public Result GetTotalSpaceSize(out long totalSpace, U8Span path)
        {
            return FileSystem.GetTotalSpaceSize(out totalSpace, path);
        }

        public Result GetFileTimeStampRaw(out FileTimeStampRaw timeStamp, U8Span path)
        {
            return FileSystem.GetFileTimeStampRaw(out timeStamp, path);
        }

        public Result Commit()
        {
            if (OpenFiles.Any(x => (x.OpenMode & OpenMode.Write) != 0))
            {
                return ResultFs.WriteModeFileNotClosed.Log();
            }

            return FileSystem.Commit();
        }

        public Result QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, U8Span path, QueryId queryId)
        {
            return FileSystem.QueryEntry(outBuffer, inBuffer, queryId, path);
        }

        public Result GetCommonMountName(Span<byte> nameBuffer)
        {
            if (MountNameGenerator == null) return ResultFs.PreconditionViolation.Log();

            return MountNameGenerator.GenerateCommonMountName(nameBuffer);
        }

        internal void NotifyCloseFile(FileAccessor file)
        {
            lock (_locker)
            {
                OpenFiles.Remove(file);
            }
        }

        internal void NotifyCloseDirectory(DirectoryAccessor directory)
        {
            lock (_locker)
            {
                OpenDirectories.Remove(directory);
            }
        }

        internal void Close()
        {
            // Todo: Possibly check for open files and directories
            // Nintendo checks for them in DumpUnclosedAccessorList in
            // FileSystemAccessor's destructor

            FileSystem?.Dispose();
        }
    }
}
