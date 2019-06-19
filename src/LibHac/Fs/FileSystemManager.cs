using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using LibHac.Fs.Accessors;

using static LibHac.Results;
using static LibHac.Fs.ResultsFs;

namespace LibHac.Fs
{
    public class FileSystemManager
    {
        internal Horizon Os { get; }
        internal ITimeSpanGenerator Time { get; }
        internal IAccessLogger Logger { get; }

        internal MountTable MountTable { get; } = new MountTable();

        private bool AccessLogEnabled { get; set; } = true;

        public FileSystemManager(Horizon os)
        {
            Os = os;
        }

        public FileSystemManager(Horizon os, IAccessLogger logger, ITimeSpanGenerator timer)
        {
            Os = os;
            Logger = logger;
            Time = timer;
        }

        public void Register(string mountName, IFileSystem fileSystem)
        {
            var accessor = new FileSystemAccessor(mountName, fileSystem);

            MountTable.Mount(accessor);

            accessor.IsAccessLogEnabled = IsEnabledAccessLog();
        }

        public void CreateDirectory(string path)
        {
            FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
                .ThrowIfFailure();

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                fileSystem.CreateDirectory(subPath.ToString());
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(startTime, endTime, $", path: \"{path}\"");
            }
            else
            {
                fileSystem.CreateDirectory(subPath.ToString());
            }
        }

        public void CreateFile(string path, long size)
        {
            CreateFile(path, size, CreateFileOptions.None);
        }

        public void CreateFile(string path, long size, CreateFileOptions options)
        {
            FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
                .ThrowIfFailure();

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                fileSystem.CreateFile(subPath.ToString(), size, options);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(startTime, endTime, $", path: \"{path}\", size: {size}");
            }
            else
            {
                fileSystem.CreateFile(subPath.ToString(), size, options);
            }
        }

        public void DeleteDirectory(string path)
        {
            FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
                .ThrowIfFailure();

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                fileSystem.DeleteDirectory(subPath.ToString());
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(startTime, endTime, $", path: \"{path}\"");
            }
            else
            {
                fileSystem.DeleteDirectory(subPath.ToString());
            }
        }

        public void DeleteDirectoryRecursively(string path)
        {
            FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
                .ThrowIfFailure();

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                fileSystem.DeleteDirectoryRecursively(subPath.ToString());
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(startTime, endTime, $", path: \"{path}\"");
            }
            else
            {
                fileSystem.DeleteDirectoryRecursively(subPath.ToString());
            }
        }

        public void CleanDirectoryRecursively(string path)
        {
            FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
                .ThrowIfFailure();

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                fileSystem.CleanDirectoryRecursively(subPath.ToString());
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(startTime, endTime, $", path: \"{path}\"");
            }
            else
            {
                fileSystem.CleanDirectoryRecursively(subPath.ToString());
            }
        }

        public void DeleteFile(string path)
        {
            FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
                .ThrowIfFailure();

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                fileSystem.DeleteFile(subPath.ToString());
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(startTime, endTime, $", path: \"{path}\"");
            }
            else
            {
                fileSystem.DeleteFile(subPath.ToString());
            }
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

            if (IsEnabledAccessLog() && oldFileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                oldFileSystem.RenameDirectory(oldSubPath.ToString(), newSubPath.ToString());
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(startTime, endTime, $", path: \"{oldPath}\", new_path: \"{newPath}\"");
            }
            else
            {
                oldFileSystem.RenameDirectory(oldSubPath.ToString(), newSubPath.ToString());
            }
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

            if (IsEnabledAccessLog() && oldFileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                oldFileSystem.RenameFile(oldSubPath.ToString(), newSubPath.ToString());
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(startTime, endTime, $", path: \"{oldPath}\", new_path: \"{newPath}\"");
            }
            else
            {
                oldFileSystem.RenameFile(oldSubPath.ToString(), newSubPath.ToString());
            }
        }

        public DirectoryEntryType GetEntryType(string path)
        {
            FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
                .ThrowIfFailure();

            DirectoryEntryType type;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                type = fileSystem.GetEntryType(subPath.ToString());
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(startTime, endTime, $", path: \"{path}\"");
            }
            else
            {
                type = fileSystem.GetEntryType(subPath.ToString());
            }

            return type;
        }

        public FileHandle OpenFile(string path, OpenMode mode)
        {
            FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
                .ThrowIfFailure();

            FileHandle handle;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                FileAccessor file = fileSystem.OpenFile(subPath.ToString(), mode);
                handle = new FileHandle(file);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(startTime, endTime, handle, $", path: \"{path}\", open_mode: {mode}");
            }
            else
            {
                FileAccessor file = fileSystem.OpenFile(subPath.ToString(), mode);
                handle = new FileHandle(file);
            }

            return handle;
        }

        public DirectoryHandle OpenDirectory(string path, OpenDirectoryMode mode)
        {
            FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
                .ThrowIfFailure();

            DirectoryHandle handle;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                DirectoryAccessor dir = fileSystem.OpenDirectory(subPath.ToString(), mode);
                handle = new DirectoryHandle(dir);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(startTime, endTime, handle, $", path: \"{path}\", open_mode: {mode}");
            }
            else
            {
                DirectoryAccessor dir = fileSystem.OpenDirectory(subPath.ToString(), mode);
                handle = new DirectoryHandle(dir);
            }

            return handle;
        }

        public long GetFreeSpaceSize(string path)
        {
            FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
                .ThrowIfFailure();

            return fileSystem.GetFreeSpaceSize(subPath.ToString());
        }

        public long GetTotalSpaceSize(string path)
        {
            FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
                .ThrowIfFailure();

            return fileSystem.GetTotalSpaceSize(subPath.ToString());
        }

        public FileTimeStampRaw GetFileTimeStamp(string path)
        {
            FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
                .ThrowIfFailure();

            return fileSystem.GetFileTimeStampRaw(subPath.ToString());
        }

        public void Commit(string mountName)
        {
            MountTable.Find(mountName, out FileSystemAccessor fileSystem).ThrowIfFailure();

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                fileSystem.Commit();
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(startTime, endTime, $", name: \"{mountName}\"");
            }
            else
            {
                fileSystem.Commit();
            }
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
            int bytesRead;

            if (IsEnabledAccessLog() && IsEnabledHandleAccessLog(handle))
            {
                TimeSpan startTime = Time.GetCurrent();
                bytesRead = handle.File.Read(destination, offset, option);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(startTime, endTime, handle, $", offset: {offset}, size: {destination.Length}");
            }
            else
            {
                bytesRead = handle.File.Read(destination, offset, option);
            }

            return bytesRead;
        }

        public void WriteFile(FileHandle handle, ReadOnlySpan<byte> source, long offset)
        {
            WriteFile(handle, source, offset, WriteOption.None);
        }

        public void WriteFile(FileHandle handle, ReadOnlySpan<byte> source, long offset, WriteOption option)
        {
            if (IsEnabledAccessLog() && IsEnabledHandleAccessLog(handle))
            {
                TimeSpan startTime = Time.GetCurrent();
                handle.File.Write(source, offset, option);
                TimeSpan endTime = Time.GetCurrent();

                string optionString = (option & WriteOption.Flush) == 0 ? "" : $", write_option: {option}";

                OutputAccessLog(startTime, endTime, handle, $", offset: {offset}, size: {source.Length}{optionString}");
            }
            else
            {
                handle.File.Write(source, offset, option);
            }
        }

        public void FlushFile(FileHandle handle)
        {
            if (IsEnabledAccessLog() && IsEnabledHandleAccessLog(handle))
            {
                TimeSpan startTime = Time.GetCurrent();
                handle.File.Flush();
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(startTime, endTime, handle, string.Empty);
            }
            else
            {
                handle.File.Flush();
            }
        }

        public long GetFileSize(FileHandle handle)
        {
            return handle.File.GetSize();
        }

        public void SetFileSize(FileHandle handle, long size)
        {
            if (IsEnabledAccessLog() && IsEnabledHandleAccessLog(handle))
            {
                TimeSpan startTime = Time.GetCurrent();
                handle.File.SetSize(size);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(startTime, endTime, handle, $", size: {size}");
            }
            else
            {
                handle.File.SetSize(size);
            }
        }

        public OpenMode GetFileOpenMode(FileHandle handle)
        {
            return handle.File.OpenMode;
        }

        public void CloseFile(FileHandle handle)
        {
            if (IsEnabledAccessLog() && IsEnabledHandleAccessLog(handle))
            {
                TimeSpan startTime = Time.GetCurrent();
                handle.Dispose();
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(startTime, endTime, handle, string.Empty);
            }
            else
            {
                handle.Dispose();
            }
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
            if (IsEnabledAccessLog() && IsEnabledHandleAccessLog(handle))
            {
                TimeSpan startTime = Time.GetCurrent();
                IEnumerable<DirectoryEntry> entries = handle.Directory.Read();
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(startTime, endTime, handle, string.Empty);
                return entries;
            }

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

        internal bool IsEnabledAccessLog()
        {
            return AccessLogEnabled && Logger != null && Time != null;
        }

        internal bool IsEnabledHandleAccessLog(FileHandle handle)
        {
            return handle.File.Parent.IsAccessLogEnabled;
        }

        internal bool IsEnabledHandleAccessLog(DirectoryHandle handle)
        {
            return handle.Directory.Parent.IsAccessLogEnabled;
        }

        internal void OutputAccessLog(TimeSpan startTime, TimeSpan endTime, string message, [CallerMemberName] string caller = "")
        {
            Logger.Log(startTime, endTime, 0, message, caller);
        }

        internal void OutputAccessLog(TimeSpan startTime, TimeSpan endTime, FileHandle handle, string message, [CallerMemberName] string caller = "")
        {
            Logger.Log(startTime, endTime, handle.GetId(), message, caller);
        }

        internal void OutputAccessLog(TimeSpan startTime, TimeSpan endTime, DirectoryHandle handle, string message, [CallerMemberName] string caller = "")
        {
            Logger.Log(startTime, endTime, handle.GetId(), message, caller);
        }
    }
}
