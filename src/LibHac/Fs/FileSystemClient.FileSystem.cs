using System;
using LibHac.Fs.Accessors;

namespace LibHac.Fs
{
    public partial class FileSystemClient
    {
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
    }
}
