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
        private const string EmptyMountNameMessage = "Error: Mount failed because the mount name was empty.\n";
        private const string TooLongMountNameMessage = "Error: Mount failed because the mount name was too long. The mount name was \"{0}\".\n";
        private const string FileNotClosedMessage = "Error: Unmount failed because not all files were closed.\n";
        private const string DirectoryNotClosedMessage = "Error: Unmount failed because not all directories were closed.\n";
        private const string InvalidFsEntryObjectMessage = "Invalid file or directory object.";

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
        private PathFlags _pathFlags;
        private Optional<Ncm.DataId> _dataId;

        internal HorizonClient Hos { get; }

        public FileSystemAccessor(HorizonClient hosClient, U8Span name, IMultiCommitTarget multiCommitTarget,
            IFileSystem fileSystem, ICommonMountNameGenerator mountNameGenerator,
            ISaveDataAttributeGetter saveAttributeGetter)
        {
            Hos = hosClient;

            _fileSystem = fileSystem;
            _openFiles = new LinkedList<FileAccessor>();
            _openDirectories = new LinkedList<DirectoryAccessor>();
            _openListLock.Initialize();
            _mountNameGenerator = mountNameGenerator;
            _saveDataAttributeGetter = saveAttributeGetter;
            _multiCommitTarget = multiCommitTarget;

            if (name.IsEmpty())
            {
                Hos.Fs.Impl.LogErrorMessage(ResultFs.InvalidMountName.Value, EmptyMountNameMessage);
                Abort.DoAbort(ResultFs.InvalidMountName.Value);
            }

            int mountLength = StringUtils.Copy(_mountName.Name, name, PathTool.MountNameLengthMax + 1);

            if (mountLength > PathTool.MountNameLengthMax)
            {
                Hos.Fs.Impl.LogErrorMessage(ResultFs.InvalidMountName.Value, TooLongMountNameMessage,
                    name.ToString());
                Abort.DoAbort(ResultFs.InvalidMountName.Value);
            }

            if (StringUtils.Compare(_mountName.Name, CommonMountNames.HostRootFileSystemMountName) == 0)
            {
                _pathFlags.AllowWindowsPath();
            }
        }

        public void Dispose()
        {
            using (ScopedLock.Lock(ref _openListLock))
            {
                DumpUnclosedAccessorList(OpenMode.All, OpenDirectoryMode.All);

                if (_openFiles.Count != 0)
                {
                    Hos.Fs.Impl.LogErrorMessage(ResultFs.FileNotClosed.Value, FileNotClosedMessage);
                    Abort.DoAbort(ResultFs.FileNotClosed.Value);
                }

                if (_openDirectories.Count != 0)
                {
                    Hos.Fs.Impl.LogErrorMessage(ResultFs.DirectoryNotClosed.Value, DirectoryNotClosedMessage);
                    Abort.DoAbort(ResultFs.DirectoryNotClosed.Value);
                }

                if (_isPathCacheAttached)
                {
                    throw new NotImplementedException();
                }
            }

            ISaveDataAttributeGetter saveDataAttributeGetter = Shared.Move(ref _saveDataAttributeGetter);
            saveDataAttributeGetter?.Dispose();

            ICommonMountNameGenerator mountNameGenerator = Shared.Move(ref _mountNameGenerator);
            mountNameGenerator?.Dispose();

            IFileSystem fileSystem = Shared.Move(ref _fileSystem);
            fileSystem?.Dispose();
        }

        private static void Remove<T>(LinkedList<T> list, T item)
        {
            LinkedListNode<T> node = list.Find(item);

            if (node is not null)
            {
                list.Remove(node);
                return;
            }

            Assert.SdkAssert(false, InvalidFsEntryObjectMessage);
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

        public Optional<Ncm.DataId> GetDataId() => _dataId;
        public void SetDataId(Ncm.DataId dataId) => _dataId.Set(dataId);

        public Result SetUpPath(ref Path path, U8Span pathBuffer)
        {
            Result rc = PathFormatter.IsNormalized(out bool isNormalized, out _, pathBuffer, _pathFlags);

            if (rc.IsSuccess() && isNormalized)
            {
                path.SetShallowBuffer(pathBuffer);
            }
            else
            {
                if (_pathFlags.IsWindowsPathAllowed())
                {
                    rc = path.InitializeWithReplaceForwardSlashes(pathBuffer);
                    if (rc.IsFailure()) return rc;
                }
                else
                {
                    rc = path.InitializeWithReplaceBackslash(pathBuffer);
                    if (rc.IsFailure()) return rc;
                }

                rc = path.Normalize(_pathFlags);
                if (rc.IsFailure()) return rc;
            }

            if (path.GetLength() > PathTool.EntryNameLengthMax)
                return ResultFs.TooLongPath.Log();

            return Result.Success;
        }

        public Result CreateFile(U8Span path, long size, CreateFileOptions option)
        {
            using var pathNormalized = new Path();
            Result rc = SetUpPath(ref pathNormalized.Ref(), path);
            if (rc.IsFailure()) return rc;

            if (_isPathCacheAttached)
            {
                throw new NotImplementedException();
            }
            else
            {
                rc = _fileSystem.CreateFile(in pathNormalized, size, option);
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
        }

        public Result DeleteFile(U8Span path)
        {
            using var pathNormalized = new Path();
            Result rc = SetUpPath(ref pathNormalized.Ref(), path);
            if (rc.IsFailure()) return rc;

            rc = _fileSystem.DeleteFile(in pathNormalized);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        public Result CreateDirectory(U8Span path)
        {
            using var pathNormalized = new Path();
            Result rc = SetUpPath(ref pathNormalized.Ref(), path);
            if (rc.IsFailure()) return rc;

            rc = _fileSystem.CreateDirectory(in pathNormalized);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        public Result DeleteDirectory(U8Span path)
        {
            using var pathNormalized = new Path();
            Result rc = SetUpPath(ref pathNormalized.Ref(), path);
            if (rc.IsFailure()) return rc;

            rc = _fileSystem.CreateDirectory(in pathNormalized);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        public Result DeleteDirectoryRecursively(U8Span path)
        {
            using var pathNormalized = new Path();
            Result rc = SetUpPath(ref pathNormalized.Ref(), path);
            if (rc.IsFailure()) return rc;

            rc = _fileSystem.DeleteDirectoryRecursively(in pathNormalized);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        public Result CleanDirectoryRecursively(U8Span path)
        {
            using var pathNormalized = new Path();
            Result rc = SetUpPath(ref pathNormalized.Ref(), path);
            if (rc.IsFailure()) return rc;

            rc = _fileSystem.CleanDirectoryRecursively(in pathNormalized);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        public Result RenameFile(U8Span currentPath, U8Span newPath)
        {
            using var currentPathNormalized = new Path();
            Result rc = SetUpPath(ref currentPathNormalized.Ref(), currentPath);
            if (rc.IsFailure()) return rc;

            using var newPathNormalized = new Path();
            rc = SetUpPath(ref newPathNormalized.Ref(), newPath);
            if (rc.IsFailure()) return rc;

            if (_isPathCacheAttached)
            {
                throw new NotImplementedException();
            }
            else
            {
                rc = _fileSystem.RenameFile(in currentPathNormalized, in newPathNormalized);
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
        }

        public Result RenameDirectory(U8Span currentPath, U8Span newPath)
        {
            using var currentPathNormalized = new Path();
            Result rc = SetUpPath(ref currentPathNormalized.Ref(), currentPath);
            if (rc.IsFailure()) return rc;

            using var newPathNormalized = new Path();
            rc = SetUpPath(ref newPathNormalized.Ref(), newPath);
            if (rc.IsFailure()) return rc;

            if (_isPathCacheAttached)
            {
                throw new NotImplementedException();
            }
            else
            {
                rc = _fileSystem.RenameDirectory(in currentPathNormalized, in newPathNormalized);
                if (rc.IsFailure()) return rc;
            }
            return Result.Success;
        }

        public Result GetEntryType(out DirectoryEntryType entryType, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out entryType);

            using var pathNormalized = new Path();
            Result rc = SetUpPath(ref pathNormalized.Ref(), path);
            if (rc.IsFailure()) return rc;

            rc = _fileSystem.GetEntryType(out entryType, in pathNormalized);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        public Result GetFreeSpaceSize(out long freeSpace, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out freeSpace);

            using var pathNormalized = new Path();
            Result rc = SetUpPath(ref pathNormalized.Ref(), path);
            if (rc.IsFailure()) return rc;

            rc = _fileSystem.GetFreeSpaceSize(out freeSpace, in pathNormalized);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        public Result GetTotalSpaceSize(out long totalSpace, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out totalSpace);

            using var pathNormalized = new Path();
            Result rc = SetUpPath(ref pathNormalized.Ref(), path);
            if (rc.IsFailure()) return rc;

            rc = _fileSystem.GetTotalSpaceSize(out totalSpace, in pathNormalized);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        public Result OpenFile(out FileAccessor file, U8Span path, OpenMode mode)
        {
            UnsafeHelpers.SkipParamInit(out file);

            using var pathNormalized = new Path();
            Result rc = SetUpPath(ref pathNormalized.Ref(), path);
            if (rc.IsFailure()) return rc;

            IFile iFile = null;
            try
            {
                rc = _fileSystem.OpenFile(out iFile, in pathNormalized, mode);
                if (rc.IsFailure()) return rc;

                var fileAccessor = new FileAccessor(Hos, ref iFile, this, mode);

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

            using var pathNormalized = new Path();
            Result rc = SetUpPath(ref pathNormalized.Ref(), path);
            if (rc.IsFailure()) return rc;

            IDirectory iDirectory = null;
            try
            {
                rc = _fileSystem.OpenDirectory(out iDirectory, in pathNormalized, mode);
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
            static bool HasOpenWriteModeFiles(LinkedList<FileAccessor> list)
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
            UnsafeHelpers.SkipParamInit(out timeStamp);

            using var pathNormalized = new Path();
            Result rc = SetUpPath(ref pathNormalized.Ref(), path);
            if (rc.IsFailure()) return rc;

            rc = _fileSystem.GetFileTimeStampRaw(out timeStamp, in pathNormalized);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        public Result QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, U8Span path)
        {
            using var pathNormalized = new Path();
            Result rc = SetUpPath(ref pathNormalized.Ref(), path);
            if (rc.IsFailure()) return rc;

            rc = _fileSystem.QueryEntry(outBuffer, inBuffer, queryId, in pathNormalized);
            if (rc.IsFailure()) return rc;

            return Result.Success;
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

            Result rc = _saveDataAttributeGetter.GetSaveDataAttribute(out attribute);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        public ReferenceCountedDisposable<IFileSystemSf> GetMultiCommitTarget()
        {
            return _multiCommitTarget?.GetMultiCommitTarget();
        }

        public void PurgeFileDataCache(FileDataCacheAccessor cacheAccessor)
        {
            cacheAccessor.Purge(_fileSystem);
        }

        internal void NotifyCloseFile(FileAccessor file)
        {
            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _openListLock);
            Remove(_openFiles, file);
        }

        internal void NotifyCloseDirectory(DirectoryAccessor directory)
        {
            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _openListLock);
            Remove(_openDirectories, directory);
        }

        private static ReadOnlySpan<byte> LogFsModuleName => new[] { (byte)'$', (byte)'f', (byte)'s' }; // "$fs"

        private static ReadOnlySpan<byte> LogFsErrorInfo => // "------ FS ERROR INFORMATION ------\n"
            new[]
            {
                (byte)'-', (byte)'-', (byte)'-', (byte)'-', (byte)'-', (byte)'-', (byte)' ', (byte)'F',
                (byte)'S', (byte)' ', (byte)'E', (byte)'R', (byte)'R', (byte)'O', (byte)'R', (byte)' ',
                (byte)'I', (byte)'N', (byte)'F', (byte)'O', (byte)'R', (byte)'M', (byte)'A', (byte)'T',
                (byte)'I', (byte)'O', (byte)'N', (byte)' ', (byte)'-', (byte)'-', (byte)'-', (byte)'-',
                (byte)'-', (byte)'-', (byte)'\\', (byte)'n'
            };

        private static ReadOnlySpan<byte> LogFileNotClosed => // "Error: File not closed"
            new[]
            {
                (byte)'E', (byte)'r', (byte)'r', (byte)'o', (byte)'r', (byte)':', (byte)' ', (byte)'F',
                (byte)'i', (byte)'l', (byte)'e', (byte)' ', (byte)'n', (byte)'o', (byte)'t', (byte)' ',
                (byte)'c', (byte)'l', (byte)'o', (byte)'s', (byte)'e', (byte)'d'
            };

        private static ReadOnlySpan<byte> LogDirectoryNotClosed => // "Error: Directory not closed"
            new[]
            {
                (byte)'E', (byte)'r', (byte)'r', (byte)'o', (byte)'r', (byte)':', (byte)' ', (byte)'D',
                (byte)'i', (byte)'r', (byte)'e', (byte)'c', (byte)'t', (byte)'o', (byte)'r', (byte)'y',
                (byte)' ', (byte)'n', (byte)'o', (byte)'t', (byte)' ', (byte)'c', (byte)'l', (byte)'o',
                (byte)'s', (byte)'e', (byte)'d'
            };

        private static ReadOnlySpan<byte> LogMountName => // " (mount_name: ""
            new[]
            {
                (byte)' ', (byte)'(', (byte)'m', (byte)'o', (byte)'u', (byte)'n', (byte)'t', (byte)'_',
                (byte)'n', (byte)'a', (byte)'m', (byte)'e', (byte)':', (byte)' ', (byte)'"'
            };

        private static ReadOnlySpan<byte> LogCount => // "", count: "
            new[]
            {
                (byte)'"', (byte)',', (byte)' ', (byte)'c', (byte)'o', (byte)'u', (byte)'n', (byte)'t',
                (byte)':', (byte)' '
            };

        public static ReadOnlySpan<byte> LogLineEnd => new[] { (byte)')', (byte)'\\', (byte)'n' }; // ")\n"

        public static ReadOnlySpan<byte> LogOrOperator => new[] { (byte)' ', (byte)'|', (byte)' ' };  // " | "

        private static ReadOnlySpan<byte> LogOpenModeRead => // "OpenMode_Read"
            new[]
            {
                (byte)'O', (byte)'p', (byte)'e', (byte)'n', (byte)'M', (byte)'o', (byte)'d', (byte)'e',
                (byte)'_', (byte)'R', (byte)'e', (byte)'a', (byte)'d'
            };

        private static ReadOnlySpan<byte> LogOpenModeWrite => // "OpenMode_Write"
            new[]
            {
                (byte)'O', (byte)'p', (byte)'e', (byte)'n', (byte)'M', (byte)'o', (byte)'d', (byte)'e',
                (byte)'_', (byte)'W', (byte)'r', (byte)'i', (byte)'t', (byte)'e'
            };

        private static ReadOnlySpan<byte> LogOpenModeAppend => // "OpenMode_AllowAppend"
            new[]
            {
                (byte)'O', (byte)'p', (byte)'e', (byte)'n', (byte)'M', (byte)'o', (byte)'d', (byte)'e',
                (byte)'_', (byte)'A', (byte)'l', (byte)'l', (byte)'o', (byte)'w', (byte)'A', (byte)'p',
                (byte)'p', (byte)'e', (byte)'n', (byte)'d'
            };

        private static ReadOnlySpan<byte> LogHandle => // "     handle: 0x"
            new[]
            {
                (byte)' ', (byte)' ', (byte)' ', (byte)' ', (byte)' ', (byte)'h', (byte)'a', (byte)'n',
                (byte)'d', (byte)'l', (byte)'e', (byte)':', (byte)' ', (byte)'0', (byte)'x'
            };

        private static ReadOnlySpan<byte> LogOpenMode => // ", open_mode: "
            new[]
            {
                (byte)',', (byte)' ', (byte)'o', (byte)'p', (byte)'e', (byte)'n', (byte)'_', (byte)'m',
                (byte)'o', (byte)'d', (byte)'e', (byte)':', (byte)' '
            };

        private static ReadOnlySpan<byte> LogSize => // ", size: "
            new[]
            {
                (byte)',', (byte)' ', (byte)'s', (byte)'i', (byte)'z', (byte)'e', (byte)':', (byte)' '
            };

        private void DumpUnclosedAccessorList(OpenMode fileOpenModeMask, OpenDirectoryMode directoryOpenModeMask)
        {
            static int GetOpenFileCount(LinkedList<FileAccessor> list, OpenMode mask)
            {
                int count = 0;

                for (LinkedListNode<FileAccessor> file = list.First; file is not null; file = file.Next)
                {
                    if ((file.Value.GetOpenMode() & mask) != 0)
                        count++;
                }

                return count;
            }

            Span<byte> stringBuffer = stackalloc byte[0xA0];
            Span<byte> openModeStringBuffer = stackalloc byte[0x40];

            int openFileCount = GetOpenFileCount(_openFiles, fileOpenModeMask);

            if (openFileCount > 0 || directoryOpenModeMask != 0 && _openDirectories.Count != 0)
            {
                Hos.Diag.Impl.LogImpl(LogFsModuleName, LogSeverity.Error, LogFsErrorInfo);
            }

            if (openFileCount > 0)
            {
                var sb = new U8StringBuilder(stringBuffer, true);
                sb.Append(LogFileNotClosed).Append(LogMountName).Append(GetName()).Append(LogCount)
                    .AppendFormat(openFileCount).Append(LogLineEnd);

                Hos.Diag.Impl.LogImpl(LogFsModuleName, LogSeverity.Error, sb.Buffer);
                sb.Dispose();

                for (LinkedListNode<FileAccessor> file = _openFiles.First; file is not null; file = file.Next)
                {
                    OpenMode openMode = file.Value.GetOpenMode();

                    if ((openMode & fileOpenModeMask) == 0)
                        continue;

                    Result rc = file.Value.GetSize(out long fileSize);
                    if (rc.IsFailure())
                        fileSize = -1;

                    var openModeString = new U8StringBuilder(openModeStringBuffer);

                    ReadOnlySpan<byte> readModeString = openMode.HasFlag(OpenMode.Read) ? LogOpenModeRead : default;
                    openModeString.Append(readModeString);
                    Assert.SdkAssert(!openModeString.Overflowed);

                    if (openMode.HasFlag(OpenMode.Write))
                    {
                        if (openModeString.Length > 0)
                            sb.Append(LogOrOperator);

                        openModeString.Append(LogOpenModeWrite);
                        Assert.SdkAssert(!openModeString.Overflowed);
                    }

                    if (openMode.HasFlag(OpenMode.AllowAppend))
                    {
                        if (openModeString.Length > 0)
                            sb.Append(LogOrOperator);

                        openModeString.Append(LogOpenModeAppend);
                        Assert.SdkAssert(!openModeString.Overflowed);
                    }

                    var fileInfoString = new U8StringBuilder(stringBuffer, true);
                    fileInfoString.Append(LogHandle).AppendFormat(file.Value.GetHashCode(), 'x', 16).Append(LogOpenMode)
                        .Append(openModeString.Buffer).Append(LogSize).AppendFormat(fileSize).Append((byte)'\n');

                    Hos.Diag.Impl.LogImpl(LogFsModuleName, LogSeverity.Error, fileInfoString.Buffer);
                    fileInfoString.Dispose();
                }
            }

            if (directoryOpenModeMask != 0 && _openDirectories.Count != 0)
            {
                var sb = new U8StringBuilder(stringBuffer, true);
                sb.Append(LogDirectoryNotClosed).Append(LogMountName).Append(GetName()).Append(LogCount)
                    .AppendFormat(_openDirectories.Count).Append(LogLineEnd);

                Hos.Diag.Impl.LogImpl(LogFsModuleName, LogSeverity.Error, sb.Buffer);
                sb.Dispose();

                for (LinkedListNode<DirectoryAccessor> dir = _openDirectories.First; dir is not null; dir = dir.Next)
                {
                    var dirInfoString = new U8StringBuilder(stringBuffer, true);
                    dirInfoString.Append(LogHandle).AppendFormat(dir.Value.GetHashCode(), 'x', 16).Append((byte)'\n');

                    Hos.Diag.Impl.LogImpl(LogFsModuleName, LogSeverity.Error, dirInfoString.Buffer);
                    dirInfoString.Dispose();
                }
            }
        }
    }
}
