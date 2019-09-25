using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs.Accessors;
using LibHac.FsSystem;

namespace LibHac.Fs
{
    // Todo: Access log for FindFileSystem
    public class FileSystemManager
    {
        internal Horizon Os { get; }
        internal ITimeSpanGenerator Time { get; }
        private IAccessLog AccessLog { get; set; }

        internal MountTable MountTable { get; } = new MountTable();

        private bool AccessLogEnabled { get; set; }

        public FileSystemManager(Horizon os)
        {
            Os = os;
        }

        public FileSystemManager(Horizon os, ITimeSpanGenerator timer)
        {
            Os = os;
            Time = timer;
        }

        public FileSystemManager(ITimeSpanGenerator timer)
        {
            Time = timer;
        }

        public Result Register(U8Span mountName, IFileSystem fileSystem)
        {
            return Register(mountName, fileSystem, null);
        }

        public Result Register(U8Span mountName, IFileSystem fileSystem, ICommonMountNameGenerator nameGenerator)
        {
            var accessor = new FileSystemAccessor(mountName.ToString(), fileSystem, this, nameGenerator);

            Result rc = MountTable.Mount(accessor);
            if (rc.IsFailure()) return rc;

            accessor.IsAccessLogEnabled = IsEnabledAccessLog();
            return Result.Success;
        }

        public void Unmount(string mountName)
        {
            Result rc;

            if (IsEnabledAccessLog() && this.IsEnabledFileSystemAccessorAccessLog(mountName))
            {
                TimeSpan startTime = Time.GetCurrent();

                rc = MountTable.Unmount(mountName);

                TimeSpan endTime = Time.GetCurrent();
                OutputAccessLog(rc, startTime, endTime, $", name: \"{mountName}\"");
            }
            else
            {
                rc = MountTable.Unmount(mountName);
            }

            rc.ThrowIfFailure();
        }

        public void SetAccessLog(bool isEnabled, IAccessLog accessLog = null)
        {
            AccessLogEnabled = isEnabled;

            if (accessLog != null) AccessLog = accessLog;
        }

        public Result CreateDirectory(string path)
        {
            Result rc = FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath);
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.CreateDirectory(subPath.ToString());
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", path: \"{path}\"");
            }
            else
            {
                rc = fileSystem.CreateDirectory(subPath.ToString());
            }

            return rc;
        }

        public Result CreateFile(string path, long size)
        {
            return CreateFile(path, size, CreateFileOptions.None);
        }

        public Result CreateFile(string path, long size, CreateFileOptions options)
        {
            Result rc = FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath);
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.CreateFile(subPath.ToString(), size, options);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", path: \"{path}\", size: {size}");
            }
            else
            {
                rc = fileSystem.CreateFile(subPath.ToString(), size, options);
            }

            return rc;
        }

        public Result DeleteDirectory(string path)
        {
            Result rc = FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath);
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.DeleteDirectory(subPath.ToString());
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", path: \"{path}\"");
            }
            else
            {
                rc = fileSystem.DeleteDirectory(subPath.ToString());
            }

            return rc;
        }

        public Result DeleteDirectoryRecursively(string path)
        {
            Result rc = FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath);
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.DeleteDirectoryRecursively(subPath.ToString());
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", path: \"{path}\"");
            }
            else
            {
                rc = fileSystem.DeleteDirectoryRecursively(subPath.ToString());
            }

            return rc;
        }

        public Result CleanDirectoryRecursively(string path)
        {
            Result rc = FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath);
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.CleanDirectoryRecursively(subPath.ToString());
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", path: \"{path}\"");
            }
            else
            {
                rc = fileSystem.CleanDirectoryRecursively(subPath.ToString());
            }

            return rc;
        }

        public Result DeleteFile(string path)
        {
            Result rc = FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath);
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.DeleteFile(subPath.ToString());
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", path: \"{path}\"");
            }
            else
            {
                rc = fileSystem.DeleteFile(subPath.ToString());
            }

            return rc;
        }

        public Result RenameDirectory(string oldPath, string newPath)
        {
            Result rc = FindFileSystem(oldPath.AsSpan(), out FileSystemAccessor oldFileSystem, out ReadOnlySpan<char> oldSubPath);
            if (rc.IsFailure()) return rc;

            rc = FindFileSystem(newPath.AsSpan(), out FileSystemAccessor newFileSystem, out ReadOnlySpan<char> newSubPath);
            if (rc.IsFailure()) return rc;

            if (oldFileSystem != newFileSystem)
            {
                return ResultFs.DifferentDestFileSystem.Log();
            }

            if (IsEnabledAccessLog() && oldFileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = oldFileSystem.RenameDirectory(oldSubPath.ToString(), newSubPath.ToString());
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", path: \"{oldPath}\", new_path: \"{newPath}\"");
            }
            else
            {
                rc = oldFileSystem.RenameDirectory(oldSubPath.ToString(), newSubPath.ToString());
            }

            return rc;
        }

        public Result RenameFile(string oldPath, string newPath)
        {
            Result rc = FindFileSystem(oldPath.AsSpan(), out FileSystemAccessor oldFileSystem, out ReadOnlySpan<char> oldSubPath);
            if (rc.IsFailure()) return rc;

            rc = FindFileSystem(newPath.AsSpan(), out FileSystemAccessor newFileSystem, out ReadOnlySpan<char> newSubPath);
            if (rc.IsFailure()) return rc;

            if (oldFileSystem != newFileSystem)
            {
                return ResultFs.DifferentDestFileSystem.Log();
            }

            if (IsEnabledAccessLog() && oldFileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = oldFileSystem.RenameFile(oldSubPath.ToString(), newSubPath.ToString());
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", path: \"{oldPath}\", new_path: \"{newPath}\"");
            }
            else
            {
                rc = oldFileSystem.RenameFile(oldSubPath.ToString(), newSubPath.ToString());
            }

            return rc;
        }

        public Result GetEntryType(out DirectoryEntryType type, string path)
        {
            type = default;

            Result rc = FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath);
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.GetEntryType(out type, subPath.ToString());
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", path: \"{path}\"");
            }
            else
            {
                rc = fileSystem.GetEntryType(out type, subPath.ToString());
            }

            return rc;
        }

        public Result OpenFile(out FileHandle handle, string path, OpenMode mode)
        {
            handle = default;

            Result rc = FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath);
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.OpenFile(out FileAccessor file, subPath.ToString(), mode);
                handle = new FileHandle(file);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, handle, $", path: \"{path}\", open_mode: {mode}");
            }
            else
            {
                rc = fileSystem.OpenFile(out FileAccessor file, subPath.ToString(), mode);
                handle = new FileHandle(file);
            }

            return rc;
        }

        public Result OpenDirectory(out DirectoryHandle handle, string path, OpenDirectoryMode mode)
        {
            handle = default;

            Result rc = FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath);
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.OpenDirectory(out DirectoryAccessor dir, subPath.ToString(), mode);
                handle = new DirectoryHandle(dir);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, handle, $", path: \"{path}\", open_mode: {mode}");
            }
            else
            {
                rc = fileSystem.OpenDirectory(out DirectoryAccessor dir, subPath.ToString(), mode);
                handle = new DirectoryHandle(dir);
            }

            return rc;
        }

        public Result GetFreeSpaceSize(out long freeSpace, string path)
        {
            freeSpace = default;

            Result rc = FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath);
            if (rc.IsFailure()) return rc;

            return fileSystem.GetFreeSpaceSize(out freeSpace, subPath.ToString());
        }

        public Result GetTotalSpaceSize(out long totalSpace, string path)
        {
            totalSpace = default;

            Result rc = FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath);
            if (rc.IsFailure()) return rc;

            return fileSystem.GetTotalSpaceSize(out totalSpace, subPath.ToString());
        }

        public Result GetFileTimeStamp(out FileTimeStampRaw timeStamp, string path)
        {
            timeStamp = default;

            Result rc = FindFileSystem(path.AsSpan(), out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath);
            if (rc.IsFailure()) return rc;

            return fileSystem.GetFileTimeStampRaw(out timeStamp, subPath.ToString());
        }

        public Result Commit(string mountName)
        {
            Result rc = MountTable.Find(mountName, out FileSystemAccessor fileSystem);
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.Commit();
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", name: \"{mountName}\"");
            }
            else
            {
                rc = fileSystem.Commit();
            }

            return rc;
        }

        // ==========================
        // Operations on file handles
        // ==========================
        public Result ReadFile(FileHandle handle, long offset, Span<byte> destination)
        {
            return ReadFile(handle, offset, destination, ReadOption.None);
        }

        public Result ReadFile(FileHandle handle, long offset, Span<byte> destination, ReadOption option)
        {
            Result rc = ReadFile(out long bytesRead, handle, offset, destination, option);
            if (rc.IsFailure()) return rc;

            if (bytesRead == destination.Length) return Result.Success;

            return ResultFs.ValueOutOfRange.Log();
        }

        public Result ReadFile(out long bytesRead, FileHandle handle, long offset, Span<byte> destination)
        {
            return ReadFile(out bytesRead, handle, offset, destination, ReadOption.None);
        }

        public Result ReadFile(out long bytesRead, FileHandle handle, long offset, Span<byte> destination, ReadOption option)
        {
            Result rc;

            if (IsEnabledAccessLog() && IsEnabledHandleAccessLog(handle))
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = handle.File.Read(out bytesRead, offset, destination, option);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, handle, $", offset: {offset}, size: {destination.Length}");
            }
            else
            {
                rc = handle.File.Read(out bytesRead, offset, destination, option);
            }

            return rc;
        }

        public Result WriteFile(FileHandle handle, long offset, ReadOnlySpan<byte> source)
        {
            return WriteFile(handle, offset, source, WriteOption.None);
        }

        public Result WriteFile(FileHandle handle, long offset, ReadOnlySpan<byte> source, WriteOption option)
        {
            Result rc;

            if (IsEnabledAccessLog() && IsEnabledHandleAccessLog(handle))
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = handle.File.Write(offset, source, option);
                TimeSpan endTime = Time.GetCurrent();

                string optionString = (option & WriteOption.Flush) == 0 ? "" : $", write_option: {option}";

                OutputAccessLog(rc, startTime, endTime, handle, $", offset: {offset}, size: {source.Length}{optionString}");
            }
            else
            {
                rc = handle.File.Write(offset, source, option);
            }

            return rc;
        }

        public Result FlushFile(FileHandle handle)
        {
            Result rc;

            if (IsEnabledAccessLog() && IsEnabledHandleAccessLog(handle))
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = handle.File.Flush();
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, handle, string.Empty);
            }
            else
            {
                rc = handle.File.Flush();
            }

            return rc;
        }

        public Result GetFileSize(out long fileSize, FileHandle handle)
        {
            return handle.File.GetSize(out fileSize);
        }

        public Result SetFileSize(FileHandle handle, long size)
        {
            Result rc;

            if (IsEnabledAccessLog() && IsEnabledHandleAccessLog(handle))
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = handle.File.SetSize(size);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, handle, $", size: {size}");
            }
            else
            {
                rc = handle.File.SetSize(size);
            }

            return rc;
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
                handle.File.Dispose();
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(Result.Success, startTime, endTime, handle, string.Empty);
            }
            else
            {
                handle.File.Dispose();
            }
        }

        // ==========================
        // Operations on directory handles
        // ==========================
        public Result GetDirectoryEntryCount(out long count, DirectoryHandle handle)
        {
            return handle.Directory.GetEntryCount(out count);
        }

        public Result ReadDirectory(out long entriesRead, Span<DirectoryEntry> entryBuffer, DirectoryHandle handle)
        {
            Result rc;

            if (IsEnabledAccessLog() && IsEnabledHandleAccessLog(handle))
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = handle.Directory.Read(out entriesRead, entryBuffer);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, handle, string.Empty);
            }
            else
            {
                rc = handle.Directory.Read(out entriesRead, entryBuffer);
            }

            return rc;
        }

        public void CloseDirectory(DirectoryHandle handle)
        {
            if (IsEnabledAccessLog() && IsEnabledHandleAccessLog(handle))
            {
                TimeSpan startTime = Time.GetCurrent();
                handle.Directory.Dispose();
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(Result.Success, startTime, endTime, handle, string.Empty);
            }
            else
            {
                handle.Directory.Dispose();
            }
        }

        internal Result FindFileSystem(ReadOnlySpan<char> path, out FileSystemAccessor fileSystem, out ReadOnlySpan<char> subPath)
        {
            fileSystem = default;

            Result rc = GetMountName(path, out ReadOnlySpan<char> mountName, out subPath);
            if (rc.IsFailure()) return rc;

            rc = MountTable.Find(mountName.ToString(), out fileSystem);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        internal static Result GetMountName(ReadOnlySpan<char> path, out ReadOnlySpan<char> mountName, out ReadOnlySpan<char> subPath)
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

                return ResultFs.InvalidMountName;
            }

            mountName = path.Slice(0, mountLen);

            if (mountLen + 1 < path.Length)
            {
                subPath = path.Slice(mountLen + 1);
            }
            else
            {
                subPath = default;
            }

            return Result.Success;
        }

        internal bool IsEnabledAccessLog()
        {
            return AccessLogEnabled && AccessLog != null && Time != null;
        }

        internal bool IsEnabledHandleAccessLog(FileHandle handle)
        {
            return handle.File.Parent.IsAccessLogEnabled;
        }

        internal bool IsEnabledHandleAccessLog(DirectoryHandle handle)
        {
            return handle.Directory.Parent.IsAccessLogEnabled;
        }

        internal void OutputAccessLog(Result result, TimeSpan startTime, TimeSpan endTime, string message, [CallerMemberName] string caller = "")
        {
            AccessLog.Log(result, startTime, endTime, 0, message, caller);
        }

        internal void OutputAccessLog(Result result, TimeSpan startTime, TimeSpan endTime, FileHandle handle, string message, [CallerMemberName] string caller = "")
        {
            AccessLog.Log(result, startTime, endTime, handle.GetId(), message, caller);
        }

        internal void OutputAccessLog(Result result, TimeSpan startTime, TimeSpan endTime, DirectoryHandle handle, string message, [CallerMemberName] string caller = "")
        {
            AccessLog.Log(result, startTime, endTime, handle.GetId(), message, caller);
        }
    }
}
