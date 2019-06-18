using System;
using System.Collections.Generic;
using LibHac.Fs.Accessors;

using static LibHac.Results;
using static LibHac.Fs.ResultsFs;

namespace LibHac.Fs
{
    public class FileSystemManager
    {
        internal Horizon Os { get; }

        internal MountTable MountTable { get; } = new MountTable();

        public FileSystemManager(Horizon os)
        {
            Os = os;
        }

        public void Register(string mountName, IFileSystem fileSystem)
        {
            var accessor = new FileSystemAccessor(mountName, fileSystem);

            MountTable.Mount(accessor);
        }

        public void CreateDirectory(string path)
        {
            FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
                .ThrowIfFailure();

            fileSystem.CreateDirectory(subPath.ToString());
        }

        public void CreateFile(string path, long size)
        {
            CreateFile(path, size, CreateFileOptions.None);
        }

        public void CreateFile(string path, long size, CreateFileOptions options)
        {
            FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
                .ThrowIfFailure();

            fileSystem.CreateFile(subPath.ToString(), size, options);
        }

        public void DeleteDirectory(string path)
        {
            FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
                .ThrowIfFailure();

            fileSystem.DeleteDirectory(subPath.ToString());
        }

        public void DeleteDirectoryRecursively(string path)
        {
            FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
                .ThrowIfFailure();

            fileSystem.DeleteDirectoryRecursively(subPath.ToString());
        }

        public void CleanDirectoryRecursively(string path)
        {
            FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
                .ThrowIfFailure();

            fileSystem.CleanDirectoryRecursively(subPath.ToString());
        }

        public void DeleteFile(string path)
        {
            FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
                .ThrowIfFailure();

            fileSystem.DeleteFile(subPath.ToString());
        }

        public void RenameDirectory(string oldPath, string newPath)
        {
            FindFileSystem(oldPath.AsSpan(), out FileSystemAccessor oldFileSystem, out ReadOnlySpan<char> oldSubPath)
                .ThrowIfFailure();

            FindFileSystem(newPath.AsSpan(), out FileSystemAccessor newFileSystem, out ReadOnlySpan<char> newSubPath)
                .ThrowIfFailure();

            if (oldFileSystem != newFileSystem)
            {
                ThrowHelper.ThrowResult(ResultFsDifferentDestFileSystem);
            }

            oldFileSystem.RenameDirectory(oldSubPath.ToString(), newSubPath.ToString());
        }

        public void RenameFile(string oldPath, string newPath)
        {
            FindFileSystem(oldPath.AsSpan(), out FileSystemAccessor oldFileSystem, out ReadOnlySpan<char> oldSubPath)
                .ThrowIfFailure();

            FindFileSystem(newPath.AsSpan(), out FileSystemAccessor newFileSystem, out ReadOnlySpan<char> newSubPath)
                .ThrowIfFailure();

            if (oldFileSystem != newFileSystem)
            {
                ThrowHelper.ThrowResult(ResultFsDifferentDestFileSystem);
            }

            oldFileSystem.RenameFile(oldSubPath.ToString(), newSubPath.ToString());
        }

        public DirectoryEntryType GetEntryType(string path)
        {
            FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
                .ThrowIfFailure();

            return fileSystem.GetEntryType(subPath.ToString());
        }

        public FileHandle OpenFile(string path, OpenMode mode)
        {
            FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
                .ThrowIfFailure();

            FileAccessor file = fileSystem.OpenFile(subPath.ToString(), mode);

            return new FileHandle(file);
        }

        public DirectoryHandle OpenDirectory(string path, OpenDirectoryMode mode)
        {
            FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
                .ThrowIfFailure();

            DirectoryAccessor dir = fileSystem.OpenDirectory(subPath.ToString(), mode);

            return new DirectoryHandle(dir);
        }

        long GetFreeSpaceSize(string path)
        {
            FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
                .ThrowIfFailure();

            return fileSystem.GetFreeSpaceSize(subPath.ToString());
        }

        long GetTotalSpaceSize(string path)
        {
            FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
                .ThrowIfFailure();

            return fileSystem.GetTotalSpaceSize(subPath.ToString());
        }

        FileTimeStampRaw GetFileTimeStamp(string path)
        {
            FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
                .ThrowIfFailure();

            return fileSystem.GetFileTimeStampRaw(subPath.ToString());
        }

        public void Commit(string mountName)
        {
            MountTable.Find(mountName, out FileSystemAccessor fileSystem).ThrowIfFailure();

            fileSystem.Commit();
        }

        // ==========================
        // Operations on file handles
        // ==========================
        public int ReadFile(FileHandle handle, Span<byte> destination, long offset)
        {
            return ReadFile(handle, destination, offset, ReadOption.None);
        }

        public int ReadFile(FileHandle handle, Span<byte> destination, long offset, ReadOption option)
        {
            return handle.File.Read(destination, offset, option);
        }

        public void WriteFile(FileHandle handle, ReadOnlySpan<byte> source, long offset)
        {
            WriteFile(handle, source, offset, WriteOption.None);
        }

        public void WriteFile(FileHandle handle, ReadOnlySpan<byte> source, long offset, WriteOption option)
        {
            handle.File.Write(source, offset, option);
        }

        public void FlushFile(FileHandle handle)
        {
            handle.File.Flush();
        }

        public long GetFileSize(FileHandle handle)
        {
            return handle.File.GetSize();
        }

        public void SetFileSize(FileHandle handle, long size)
        {
            handle.File.SetSize(size);
        }

        public OpenMode GetFileOpenMode(FileHandle handle)
        {
            return handle.File.OpenMode;
        }

        public void CloseFile(FileHandle handle)
        {
            handle.File.Dispose();
        }

        // ==========================
        // Operations on directory handles
        // ==========================
        public int GetDirectoryEntryCount(DirectoryHandle handle)
        {
            return handle.Directory.GetEntryCount();
        }

        public IEnumerable<DirectoryEntry> ReadDirectory(DirectoryHandle handle)
        {
            return handle.Directory.Read();
        }

        internal Result FindFileSystem(ReadOnlySpan<char> path, out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
        {
            fileSystem = default;

            Result result = GetMountName(path, out ReadOnlySpan<char> mountName, out subPath);
            if (result.IsFailure()) return result;

            result = MountTable.Find(mountName.ToString(), out fileSystem);
            if (result.IsFailure()) return result;

            return ResultSuccess;
        }

        internal Result GetMountName(ReadOnlySpan<char> path, out ReadOnlySpan<char> mountName, out ReadOnlySpan<char> subPath)
        {
            int mountLen = 0;
            int maxMountLen = Math.Min(path.Length, PathTools.MountNameLength);

            for (int i = 0; i < maxMountLen; i++)
            {
                if (path[i] == PathTools.MountSeparator)
                {
                    mountLen = i;
                    break;
                }
            }

            if (mountLen == 0)
            {
                mountName = default;
                subPath = default;

                return ResultFsInvalidMountName;
            }

            mountName = path.Slice(0, mountLen);

            if (mountLen + 2 < path.Length)
            {
                subPath = path.Slice(mountLen + 2);
            }
            else
            {
                subPath = default;
            }

            return ResultSuccess;
        }
    }
}
