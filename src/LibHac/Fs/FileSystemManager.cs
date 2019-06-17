using System;
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        public void DeleteDirectoryRecursively(string path)
        {
            throw new NotImplementedException();
        }

        public void CleanDirectoryRecursively(string path)
        {
            throw new NotImplementedException();
        }

        public void DeleteFile(string path)
        {
            throw new NotImplementedException();
        }

        public void RenameDirectory(string oldPath, string newPath)
        {
            throw new NotImplementedException();
        }

        public void RenameFile(string oldPath, string newPath)
        {
            throw new NotImplementedException();
        }

        // How to report when entry isn't found?
        public DirectoryEntryType GetEntryType(string path)
        {
            throw new NotImplementedException();
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
