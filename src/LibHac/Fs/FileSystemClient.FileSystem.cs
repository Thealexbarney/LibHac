using System;
using LibHac.Common;
using LibHac.Fs.Accessors;
using LibHac.Fs.Fsa;

namespace LibHac.Fs
{
    public partial class FileSystemClient
    {
        public Result CreateDirectory(U8Span path)
        {
            Result rc = FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path);
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.CreateDirectory(subPath);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", path: \"{path.ToString()}\"");
            }
            else
            {
                rc = fileSystem.CreateDirectory(subPath);
            }

            return rc;
        }

        public Result CreateFile(U8Span path, long size)
        {
            return CreateFile(path, size, CreateFileOptions.None);
        }

        public Result CreateFile(U8Span path, long size, CreateFileOptions options)
        {
            Result rc = FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path);
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.CreateFile(subPath, size, options);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", path: \"{path.ToString()}\", size: {size}");
            }
            else
            {
                rc = fileSystem.CreateFile(subPath, size, options);
            }

            return rc;
        }

        public Result DeleteDirectory(U8Span path)
        {
            Result rc = FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path);
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.DeleteDirectory(subPath);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", path: \"{path.ToString()}\"");
            }
            else
            {
                rc = fileSystem.DeleteDirectory(subPath);
            }

            return rc;
        }

        public Result DeleteDirectoryRecursively(U8Span path)
        {
            Result rc = FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path);
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.DeleteDirectoryRecursively(subPath);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", path: \"{path.ToString()}\"");
            }
            else
            {
                rc = fileSystem.DeleteDirectoryRecursively(subPath);
            }

            return rc;
        }

        public Result CleanDirectoryRecursively(U8Span path)
        {
            Result rc = FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path);
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.CleanDirectoryRecursively(subPath);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", path: \"{path.ToString()}\"");
            }
            else
            {
                rc = fileSystem.CleanDirectoryRecursively(subPath);
            }

            return rc;
        }

        public Result DeleteFile(U8Span path)
        {
            Result rc = FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path);
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.DeleteFile(subPath);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", path: \"{path.ToString()}\"");
            }
            else
            {
                rc = fileSystem.DeleteFile(subPath);
            }

            return rc;
        }

        public Result RenameDirectory(U8Span oldPath, U8Span newPath)
        {
            Result rc = FindFileSystem(out FileSystemAccessor oldFileSystem, out U8Span oldSubPath, oldPath);
            if (rc.IsFailure()) return rc;

            rc = FindFileSystem(out FileSystemAccessor newFileSystem, out U8Span newSubPath, newPath);
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

                OutputAccessLog(rc, startTime, endTime, $", path: \"{oldPath.ToString()}\", new_path: \"{newPath.ToString()}\"");
            }
            else
            {
                rc = oldFileSystem.RenameDirectory(oldSubPath, newSubPath);
            }

            return rc;
        }

        public Result RenameFile(U8Span oldPath, U8Span newPath)
        {
            Result rc = FindFileSystem(out FileSystemAccessor oldFileSystem, out U8Span oldSubPath, oldPath);
            if (rc.IsFailure()) return rc;

            rc = FindFileSystem(out FileSystemAccessor newFileSystem, out U8Span newSubPath, newPath);
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

                OutputAccessLog(rc, startTime, endTime, $", path: \"{oldPath.ToString()}\", new_path: \"{newPath.ToString()}\"");
            }
            else
            {
                rc = oldFileSystem.RenameFile(oldSubPath, newSubPath);
            }

            return rc;
        }

        public Result GetEntryType(out DirectoryEntryType type, U8Span path)
        {
            type = default;

            Result rc = FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path);
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.GetEntryType(out type, subPath);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", path: \"{path.ToString()}\"");
            }
            else
            {
                rc = fileSystem.GetEntryType(out type, subPath);
            }

            return rc;
        }

        public Result OpenFile(out FileHandle handle, U8Span path, OpenMode mode)
        {
            handle = default;

            Result rc = FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path);
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.OpenFile(out FileAccessor file, subPath, mode);
                handle = new FileHandle(file);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, handle, $", path: \"{path.ToString()}\", open_mode: {mode}");
            }
            else
            {
                rc = fileSystem.OpenFile(out FileAccessor file, subPath, mode);
                handle = new FileHandle(file);
            }

            return rc;
        }

        public Result OpenDirectory(out DirectoryHandle handle, U8Span path, OpenDirectoryMode mode)
        {
            handle = default;

            Result rc = FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path);
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.OpenDirectory(out DirectoryAccessor dir, subPath, mode);
                handle = new DirectoryHandle(dir);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, handle, $", path: \"{path.ToString()}\", open_mode: {mode}");
            }
            else
            {
                rc = fileSystem.OpenDirectory(out DirectoryAccessor dir, subPath, mode);
                handle = new DirectoryHandle(dir);
            }

            return rc;
        }

        public Result GetFreeSpaceSize(out long freeSpace, U8Span path)
        {
            freeSpace = default;

            Result rc = FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path);
            if (rc.IsFailure()) return rc;

            return fileSystem.GetFreeSpaceSize(out freeSpace, subPath);
        }

        public Result GetTotalSpaceSize(out long totalSpace, U8Span path)
        {
            totalSpace = default;

            Result rc = FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path);
            if (rc.IsFailure()) return rc;

            return fileSystem.GetTotalSpaceSize(out totalSpace, subPath);
        }

        public Result GetFileTimeStamp(out FileTimeStampRaw timeStamp, U8Span path)
        {
            timeStamp = default;

            Result rc = FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path);
            if (rc.IsFailure()) return rc;

            return fileSystem.GetFileTimeStampRaw(out timeStamp, subPath);
        }

        public Result Commit(U8Span mountName)
        {
            Result rc = MountTable.Find(mountName.ToString(), out FileSystemAccessor fileSystem);
            if (rc.IsFailure()) return rc;

            if (IsEnabledAccessLog() && fileSystem.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = fileSystem.Commit();
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, $", name: \"{mountName.ToString()}\"");
            }
            else
            {
                rc = fileSystem.Commit();
            }

            return rc;
        }
    }
}
