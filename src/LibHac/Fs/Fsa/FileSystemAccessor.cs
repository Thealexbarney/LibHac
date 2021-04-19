using System;
using System.Collections.Generic;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Fsa;
using LibHac.Os;
using LibHac.Util;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs.Impl
{
    internal class FileSystemAccessor : IDisposable
    {
        private MountName _mountName;
        private IFileSystem _fileSystem;
        private LinkedList<FileAccessor> _openFiles;
        private LinkedList<DirectoryAccessor> _openDirectories;
        private SdkMutexType _openListLock;
        private ICommonMountNameGenerator _mountNameGenerator;
        private ISaveDataAttributeGetter _saveDataAttributeGetter;
        private bool _isAccessLogEnabled;
        private bool _isDataCacheAttachable;
        private bool _isPathCacheAttachable;
        private bool _isPathCacheAttached;
        private IMultiCommitTarget _multiCommitTarget;

        internal FileSystemClient FsClient { get; }

        public FileSystemAccessor(FileSystemClient fsClient, U8Span name, IMultiCommitTarget multiCommitTarget,
            IFileSystem fileSystem, ICommonMountNameGenerator mountNameGenerator,
            ISaveDataAttributeGetter saveAttributeGetter)
        {
            FsClient = fsClient;

            _fileSystem = fileSystem;
            _openFiles = new LinkedList<FileAccessor>();
            _openDirectories = new LinkedList<DirectoryAccessor>();
            _openListLock.Initialize();
            _mountNameGenerator = mountNameGenerator;
            _saveDataAttributeGetter = saveAttributeGetter;
            _multiCommitTarget = multiCommitTarget;

            if (name.IsEmpty())
                Abort.DoAbort(ResultFs.InvalidMountName.Log());

            if (StringUtils.GetLength(name, PathTool.MountNameLengthMax + 1) > PathTool.MountNameLengthMax)
                Abort.DoAbort(ResultFs.InvalidMountName.Log());

            StringUtils.Copy(_mountName.Name.Slice(0, PathTool.MountNameLengthMax), name);
            _mountName.Name[PathTool.MountNameLengthMax] = 0;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            using (ScopedLock.Lock(ref _openListLock))
            {
                Abort.DoAbortUnless(_openFiles.Count == 0, ResultFs.FileNotClosed.Value,
                    "All files must be closed before unmounting.");

                Abort.DoAbortUnless(_openDirectories.Count == 0, ResultFs.DirectoryNotClosed.Value,
                    "All directories must be closed before unmounting.");

                if (_isPathCacheAttached)
                {
                    throw new NotImplementedException();
                }
            }

            _saveDataAttributeGetter?.Dispose();
            _saveDataAttributeGetter = null;

            _mountNameGenerator?.Dispose();
            _mountNameGenerator = null;

            _fileSystem?.Dispose();
            _fileSystem = null;
        }

        private static void Remove<T>(LinkedList<T> list, T item)
        {
            LinkedListNode<T> node = list.Find(item);
            Abort.DoAbortUnless(node is not null, "Invalid file or directory object.");

            list.Remove(node);
        }

        private static Result CheckPath(U8Span mountName, U8Span path)
        {
            int mountNameLength = StringUtils.GetLength(mountName, PathTool.MountNameLengthMax);
            int pathLength = StringUtils.GetLength(path, PathTool.EntryNameLengthMax);

            if (mountNameLength + 1 + pathLength > PathTool.EntryNameLengthMax)
                return ResultFs.TooLongPath.Log();

            return Result.Success;
        }

        private static bool HasOpenWriteModeFiles(LinkedList<FileAccessor> list)
        {
            for (LinkedListNode<FileAccessor> file = list.First; file is not null; file = file.Next)
            {
                if (file.Value.GetOpenMode().HasFlag(OpenMode.Write))
                {
                    return true;
                }
            }

            return false;
        }

        public void SetAccessLog(bool isEnabled) => _isAccessLogEnabled = isEnabled;
        public void SetFileDataCacheAttachable(bool isAttachable) => _isDataCacheAttachable = isAttachable;
        public void SetPathBasedFileDataCacheAttachable(bool isAttachable) => _isPathCacheAttachable = isAttachable;

        public bool IsEnabledAccessLog() => _isAccessLogEnabled;
        public bool IsFileDataCacheAttachable() => _isDataCacheAttachable;
        public bool IsPathBasedFileDataCacheAttachable() => _isPathCacheAttachable;

        public void AttachPathBasedFileDataCache()
        {
            if (_isPathCacheAttachable)
                _isPathCacheAttached = true;
        }

        public Result CreateFile(U8Span path, long size, CreateFileOptions option)
        {
            Result rc = CheckPath(new U8Span(_mountName.Name), path);
            if (rc.IsFailure()) return rc;

            if (_isPathCacheAttached)
            {
                throw new NotImplementedException();
            }
            else
            {
                rc = _fileSystem.CreateFile(path, size, option);
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
        }

        public Result DeleteFile(U8Span path)
        {
            Result rc = CheckPath(new U8Span(_mountName.Name), path);
            if (rc.IsFailure()) return rc;

            return _fileSystem.DeleteFile(path);
        }

        public Result CreateDirectory(U8Span path)
        {
            Result rc = CheckPath(new U8Span(_mountName.Name), path);
            if (rc.IsFailure()) return rc;

            return _fileSystem.CreateDirectory(path);
        }

        public Result DeleteDirectory(U8Span path)
        {
            Result rc = CheckPath(new U8Span(_mountName.Name), path);
            if (rc.IsFailure()) return rc;

            return _fileSystem.DeleteDirectory(path);
        }

        public Result DeleteDirectoryRecursively(U8Span path)
        {
            Result rc = CheckPath(new U8Span(_mountName.Name), path);
            if (rc.IsFailure()) return rc;

            return _fileSystem.DeleteDirectoryRecursively(path);
        }

        public Result CleanDirectoryRecursively(U8Span path)
        {
            Result rc = CheckPath(new U8Span(_mountName.Name), path);
            if (rc.IsFailure()) return rc;

            return _fileSystem.CleanDirectoryRecursively(path);
        }

        public Result RenameFile(U8Span oldPath, U8Span newPath)
        {
            Result rc = CheckPath(new U8Span(_mountName.Name), oldPath);
            if (rc.IsFailure()) return rc;

            rc = CheckPath(new U8Span(_mountName.Name), newPath);
            if (rc.IsFailure()) return rc;

            if (_isPathCacheAttached)
            {
                throw new NotImplementedException();
            }
            else
            {
                rc = _fileSystem.RenameFile(oldPath, newPath);
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
        }

        public Result RenameDirectory(U8Span oldPath, U8Span newPath)
        {
            Result rc = CheckPath(new U8Span(_mountName.Name), oldPath);
            if (rc.IsFailure()) return rc;

            rc = CheckPath(new U8Span(_mountName.Name), newPath);
            if (rc.IsFailure()) return rc;

            if (_isPathCacheAttached)
            {
                throw new NotImplementedException();
            }
            else
            {
                rc = _fileSystem.RenameDirectory(oldPath, newPath);
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
        }

        public Result GetEntryType(out DirectoryEntryType entryType, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out entryType);

            Result rc = CheckPath(new U8Span(_mountName.Name), path);
            if (rc.IsFailure()) return rc;

            return _fileSystem.GetEntryType(out entryType, path);
        }

        public Result GetFreeSpaceSize(out long freeSpace, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out freeSpace);

            Result rc = CheckPath(new U8Span(_mountName.Name), path);
            if (rc.IsFailure()) return rc;

            return _fileSystem.GetFreeSpaceSize(out freeSpace, path);
        }

        public Result GetTotalSpaceSize(out long totalSpace, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out totalSpace);

            Result rc = CheckPath(new U8Span(_mountName.Name), path);
            if (rc.IsFailure()) return rc;

            return _fileSystem.GetTotalSpaceSize(out totalSpace, path);
        }

        public Result OpenFile(out FileAccessor file, U8Span path, OpenMode mode)
        {
            UnsafeHelpers.SkipParamInit(out file);

            Result rc = CheckPath(new U8Span(_mountName.Name), path);
            if (rc.IsFailure()) return rc;

            IFile iFile = null;
            try
            {
                rc = _fileSystem.OpenFile(out iFile, path, mode);
                if (rc.IsFailure()) return rc;

                var fileAccessor = new FileAccessor(FsClient, ref iFile, this, mode);

                using (ScopedLock.Lock(ref _openListLock))
                {
                    _openFiles.AddLast(fileAccessor);
                }

                if (_isPathCacheAttached)
                {
                    if (mode.HasFlag(OpenMode.AllowAppend))
                    {
                        throw new NotImplementedException();
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }

                file = Shared.Move(ref fileAccessor);
                return Result.Success;
            }
            finally
            {
                iFile?.Dispose();
            }
        }

        public Result OpenDirectory(out DirectoryAccessor directory, U8Span path, OpenDirectoryMode mode)
        {
            UnsafeHelpers.SkipParamInit(out directory);

            Result rc = CheckPath(new U8Span(_mountName.Name), path);
            if (rc.IsFailure()) return rc;

            IDirectory iDirectory = null;
            try
            {
                rc = _fileSystem.OpenDirectory(out iDirectory, path, mode);
                if (rc.IsFailure()) return rc;

                var directoryAccessor = new DirectoryAccessor(ref iDirectory, this);

                using (ScopedLock.Lock(ref _openListLock))
                {
                    _openDirectories.AddLast(directoryAccessor);
                }

                directory = Shared.Move(ref directoryAccessor);
                return Result.Success;
            }
            finally
            {
                iDirectory?.Dispose();
            }
        }

        public Result Commit()
        {
            using (ScopedLock.Lock(ref _openListLock))
            {
                DumpUnclosedAccessorList(OpenMode.Write, 0);

                if (HasOpenWriteModeFiles(_openFiles))
                    return ResultFs.WriteModeFileNotClosed.Log();
            }

            return _fileSystem.Commit();
        }

        public Result GetFileTimeStampRaw(out FileTimeStampRaw timeStamp, U8Span path)
        {
            return _fileSystem.GetFileTimeStampRaw(out timeStamp, path);
        }

        public Result QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, U8Span path)
        {
            return _fileSystem.QueryEntry(outBuffer, inBuffer, queryId, path);
        }

        public U8Span GetName()
        {
            return new U8Span(_mountName.Name);
        }

        public Result GetCommonMountName(Span<byte> nameBuffer)
        {
            if (_mountNameGenerator is null)
                return ResultFs.PreconditionViolation.Log();

            return _mountNameGenerator.GenerateCommonMountName(nameBuffer);
        }

        public Result GetSaveDataAttribute(out SaveDataAttribute attribute)
        {
            UnsafeHelpers.SkipParamInit(out attribute);

            if (_saveDataAttributeGetter is null)
                return ResultFs.PreconditionViolation.Log();

            return _saveDataAttributeGetter.GetSaveDataAttribute(out attribute);
        }

        public ReferenceCountedDisposable<IFileSystemSf> GetMultiCommitTarget()
        {
            return _multiCommitTarget?.GetMultiCommitTarget();
        }

        public void PurgeFileDataCache(FileDataCacheAccessor cacheAccessor)
        {
            cacheAccessor.Purge(_fileSystem);
        }

        public void NotifyCloseFile(FileAccessor file)
        {
            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _openListLock);
            Remove(_openFiles, file);
        }

        public void NotifyCloseDirectory(DirectoryAccessor directory)
        {
            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _openListLock);
            Remove(_openDirectories, directory);
        }

        private void DumpUnclosedAccessorList(OpenMode fileOpenModeMask, OpenDirectoryMode directoryOpenModeMask)
        {
            // Todo: Implement
        }
    }
}
