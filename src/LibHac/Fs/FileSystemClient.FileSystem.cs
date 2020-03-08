using System;
using LibHac.Common;
using LibHac.Fs.Accessors;

namespace LibHac.Fs
{
    public partial class FileSystemClient
    {
        public Result CreateDirectory(string path)
        {
            Result rc = FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path.ToU8Span());
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.CreateDirectory(subPath);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", path: \"{path}\"");
            }
            else
            {
                rc = fileSystem.CreateDirectory(subPath);
            }

            return rc;
        }

        public Result CreateFile(string path, long size)
        {
            return CreateFile(path, size, CreateFileOptions.None);
        }

        public Result CreateFile(string path, long size, CreateFileOptions options)
        {
            Result rc = FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path.ToU8Span());
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.CreateFile(subPath, size, options);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", path: \"{path}\", size: {size}");
            }
            else
            {
                rc = fileSystem.CreateFile(subPath, size, options);
            }

            return rc;
        }

        public Result DeleteDirectory(string path)
        {
            Result rc = FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path.ToU8Span());
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.DeleteDirectory(subPath);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", path: \"{path}\"");
            }
            else
            {
                rc = fileSystem.DeleteDirectory(subPath);
            }

            return rc;
        }

        public Result DeleteDirectoryRecursively(string path)
        {
            Result rc = FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path.ToU8Span());
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.DeleteDirectoryRecursively(subPath);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", path: \"{path}\"");
            }
            else
            {
                rc = fileSystem.DeleteDirectoryRecursively(subPath);
            }

            return rc;
        }

        public Result CleanDirectoryRecursively(string path)
        {
            Result rc = FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path.ToU8Span());
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.CleanDirectoryRecursively(subPath);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", path: \"{path}\"");
            }
            else
            {
                rc = fileSystem.CleanDirectoryRecursively(subPath);
            }

            return rc;
        }

        public Result DeleteFile(string path)
        {
            Result rc = FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path.ToU8Span());
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.DeleteFile(subPath);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", path: \"{path}\"");
            }
            else
            {
                rc = fileSystem.DeleteFile(subPath);
            }

            return rc;
        }

        public Result RenameDirectory(string oldPath, string newPath)
        {
            Result rc = FindFileSystem(out FileSystemAccessor oldFileSystem, out U8Span oldSubPath, oldPath.ToU8Span());
            if (rc.IsFailure()) return rc;

            rc = FindFileSystem(out FileSystemAccessor newFileSystem, out U8Span newSubPath, newPath.ToU8Span());
            if (rc.IsFailure()) return rc;

            if (oldFileSystem != newFileSystem)
            {
                return ResultFs.DifferentDestFileSystem.Log();
            }

            if (IsEnabledAccessLog() && oldFileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = oldFileSystem.RenameDirectory(oldSubPath, newSubPath);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", path: \"{oldPath}\", new_path: \"{newPath}\"");
            }
            else
            {
                rc = oldFileSystem.RenameDirectory(oldSubPath, newSubPath);
            }

            return rc;
        }

        public Result RenameFile(string oldPath, string newPath)
        {
            Result rc = FindFileSystem(out FileSystemAccessor oldFileSystem, out U8Span oldSubPath, oldPath.ToU8Span());
            if (rc.IsFailure()) return rc;

            rc = FindFileSystem(out FileSystemAccessor newFileSystem, out U8Span newSubPath, newPath.ToU8Span());
            if (rc.IsFailure()) return rc;

            if (oldFileSystem != newFileSystem)
            {
                return ResultFs.DifferentDestFileSystem.Log();
            }

            if (IsEnabledAccessLog() && oldFileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = oldFileSystem.RenameFile(oldSubPath, newSubPath);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", path: \"{oldPath}\", new_path: \"{newPath}\"");
            }
            else
            {
                rc = oldFileSystem.RenameFile(oldSubPath, newSubPath);
            }

            return rc;
        }

        public Result GetEntryType(out DirectoryEntryType type, string path)
        {
            type = default;

            Result rc = FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path.ToU8Span());
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.GetEntryType(out type, subPath);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", path: \"{path}\"");
            }
            else
            {
                rc = fileSystem.GetEntryType(out type, subPath);
            }

            return rc;
        }

        public Result OpenFile(out FileHandle handle, string path, OpenMode mode)
        {
            handle = default;

            Result rc = FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path.ToU8Span());
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.OpenFile(out FileAccessor file, subPath, mode);
                handle = new FileHandle(file);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, handle, $", path: \"{path}\", open_mode: {mode}");
            }
            else
            {
                rc = fileSystem.OpenFile(out FileAccessor file, subPath, mode);
                handle = new FileHandle(file);
            }

            return rc;
        }

        public Result OpenDirectory(out DirectoryHandle handle, string path, OpenDirectoryMode mode)
        {
            handle = default;

            Result rc = FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path.ToU8Span());
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.OpenDirectory(out DirectoryAccessor dir, subPath, mode);
                handle = new DirectoryHandle(dir);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, handle, $", path: \"{path}\", open_mode: {mode}");
            }
            else
            {
                rc = fileSystem.OpenDirectory(out DirectoryAccessor dir, subPath, mode);
                handle = new DirectoryHandle(dir);
            }

            return rc;
        }

        public Result GetFreeSpaceSize(out long freeSpace, string path)
        {
            freeSpace = default;

            Result rc = FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path.ToU8Span());
            if (rc.IsFailure()) return rc;

            return fileSystem.GetFreeSpaceSize(out freeSpace, subPath);
        }

        public Result GetTotalSpaceSize(out long totalSpace, string path)
        {
            totalSpace = default;

            Result rc = FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path.ToU8Span());
            if (rc.IsFailure()) return rc;

            return fileSystem.GetTotalSpaceSize(out totalSpace, subPath);
        }

        public Result GetFileTimeStamp(out FileTimeStampRaw timeStamp, string path)
        {
            timeStamp = default;

            Result rc = FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path.ToU8Span());
            if (rc.IsFailure()) return rc;

            return fileSystem.GetFileTimeStampRaw(out timeStamp, subPath);
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
