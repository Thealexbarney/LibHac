﻿using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Sf;

using IFile = LibHac.Fs.Fsa.IFile;
using IFileSf = LibHac.FsSrv.Sf.IFile;
using IDirectory = LibHac.Fs.Fsa.IDirectory;
using IDirectorySf = LibHac.FsSrv.Sf.IDirectory;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;
using PathSf = LibHac.FsSrv.Sf.Path;

namespace LibHac.FsSrv.Impl
{
    /// <summary>
    /// Wraps an <see cref="IFile"/> to allow interfacing with it via the <see cref="IFileSf"/> interface over IPC.
    /// </summary>
    /// <remarks>Based on FS 12.0.3 (nnSdk 12.3.1)</remarks>
    public class FileInterfaceAdapter : IFileSf
    {
        private ReferenceCountedDisposable<FileSystemInterfaceAdapter> _parentFs;
        private IFile _baseFile;
        private bool _allowAllOperations;

        public FileInterfaceAdapter(IFile baseFile,
            ref ReferenceCountedDisposable<FileSystemInterfaceAdapter> parentFileSystem, bool allowAllOperations)
        {
            _baseFile = baseFile;
            _parentFs = Shared.Move(ref parentFileSystem);
            _allowAllOperations = allowAllOperations;
        }

        public void Dispose()
        {
            _baseFile?.Dispose();
            _parentFs?.Dispose();
        }

        public Result Read(out long bytesRead, long offset, OutBuffer destination, long size, ReadOption option)
        {
            const int maxTryCount = 2;
            UnsafeHelpers.SkipParamInit(out bytesRead);

            if (offset < 0)
                return ResultFs.InvalidOffset.Log();

            if (destination.Size < 0 || destination.Size < (int)size)
                return ResultFs.InvalidSize.Log();

            Result rc = Result.Success;
            long tmpBytesRead = 0;

            for (int tryNum = 0; tryNum < maxTryCount; tryNum++)
            {
                rc = _baseFile.Read(out tmpBytesRead, offset, destination.Buffer.Slice(0, (int)size), option);

                // Retry on ResultDataCorrupted
                if (!ResultFs.DataCorrupted.Includes(rc))
                    break;
            }

            if (rc.IsFailure()) return rc;

            bytesRead = tmpBytesRead;
            return Result.Success;
        }

        public Result Write(long offset, InBuffer source, long size, WriteOption option)
        {
            if (offset < 0)
                return ResultFs.InvalidOffset.Log();

            if (source.Size < 0 || source.Size < (int)size)
                return ResultFs.InvalidSize.Log();

            using var scopedPriorityChanger =
                new ScopedThreadPriorityChangerByAccessPriority(ScopedThreadPriorityChangerByAccessPriority.AccessMode.Write);

            return _baseFile.Write(offset, source.Buffer.Slice(0, (int)size), option);
        }

        public Result Flush()
        {
            return _baseFile.Flush();
        }

        public Result SetSize(long size)
        {
            if (size < 0)
                return ResultFs.InvalidSize.Log();

            return _baseFile.SetSize(size);
        }

        public Result GetSize(out long size)
        {
            const int maxTryCount = 2;
            UnsafeHelpers.SkipParamInit(out size);

            Result rc = Result.Success;
            long tmpSize = 0;

            for (int tryNum = 0; tryNum < maxTryCount; tryNum++)
            {
                rc = _baseFile.GetSize(out tmpSize);

                // Retry on ResultDataCorrupted
                if (!ResultFs.DataCorrupted.Includes(rc))
                    break;
            }

            if (rc.IsFailure()) return rc;

            size = tmpSize;
            return Result.Success;
        }

        public Result OperateRange(out QueryRangeInfo rangeInfo, int operationId, long offset, long size)
        {
            UnsafeHelpers.SkipParamInit(out rangeInfo);
            rangeInfo.Clear();

            if (operationId == (int)OperationId.QueryRange)
            {
                Unsafe.SkipInit(out QueryRangeInfo info);

                Result rc = _baseFile.OperateRange(SpanHelpers.AsByteSpan(ref info), OperationId.QueryRange, offset,
                    size, ReadOnlySpan<byte>.Empty);
                if (rc.IsFailure()) return rc;

                rangeInfo.Merge(in info);
            }
            else if (operationId == (int)OperationId.InvalidateCache)
            {
                Result rc = _baseFile.OperateRange(Span<byte>.Empty, OperationId.InvalidateCache, offset, size,
                    ReadOnlySpan<byte>.Empty);
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
        }

        public Result OperateRangeWithBuffer(OutBuffer outBuffer, InBuffer inBuffer, int operationId, long offset, long size)
        {
            static Result PermissionCheck(OperationId operationId, FileInterfaceAdapter fileAdapter)
            {
                if (operationId == OperationId.QueryUnpreparedRange ||
                    operationId == OperationId.QueryLazyLoadCompletionRate ||
                    operationId == OperationId.SetLazyLoadPriority)
                {
                    return Result.Success;
                }

                if (!fileAdapter._allowAllOperations)
                    return ResultFs.PermissionDenied.Log();

                return Result.Success;
            }

            Result rc = PermissionCheck((OperationId)operationId, this);
            if (rc.IsFailure()) return rc;

            rc = _baseFile.OperateRange(outBuffer.Buffer, (OperationId)operationId, offset, size, inBuffer.Buffer);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }
    }

    /// <summary>
    /// Wraps an <see cref="IDirectory"/> to allow interfacing with it via the <see cref="IDirectorySf"/> interface over IPC.
    /// </summary>
    /// <remarks>Based on FS 12.0.3 (nnSdk 12.3.1)</remarks>
    public class DirectoryInterfaceAdapter : IDirectorySf
    {
        private ReferenceCountedDisposable<FileSystemInterfaceAdapter> _parentFs;
        private IDirectory _baseDirectory;

        public DirectoryInterfaceAdapter(IDirectory baseDirectory,
            ref ReferenceCountedDisposable<FileSystemInterfaceAdapter> parentFileSystem)
        {
            _baseDirectory = baseDirectory;
            _parentFs = Shared.Move(ref parentFileSystem);
        }

        public void Dispose()
        {
            _baseDirectory?.Dispose();
            _parentFs?.Dispose();
        }

        public Result Read(out long entriesRead, OutBuffer entryBuffer)
        {
            const int maxTryCount = 2;
            UnsafeHelpers.SkipParamInit(out entriesRead);

            Span<DirectoryEntry> entries = MemoryMarshal.Cast<byte, DirectoryEntry>(entryBuffer.Buffer);

            Result rc = Result.Success;
            long numRead = 0;

            for (int tryNum = 0; tryNum < maxTryCount; tryNum++)
            {
                rc = _baseDirectory.Read(out numRead, entries);

                // Retry on ResultDataCorrupted
                if (!ResultFs.DataCorrupted.Includes(rc))
                    break;
            }

            if (rc.IsFailure()) return rc;

            entriesRead = numRead;
            return Result.Success;
        }

        public Result GetEntryCount(out long entryCount)
        {
            UnsafeHelpers.SkipParamInit(out entryCount);

            Result rc = _baseDirectory.GetEntryCount(out long count);
            if (rc.IsFailure()) return rc;

            entryCount = count;
            return Result.Success;
        }
    }

    /// <summary>
    /// Wraps an <see cref="IFileSystem"/> to allow interfacing with it via the <see cref="IFileSystemSf"/> interface over IPC.
    /// All incoming paths are normalized before they are passed to the base <see cref="IFileSystem"/>.
    /// </summary>
    /// <remarks>Based on FS 12.0.3 (nnSdk 12.3.1)</remarks>
    public class FileSystemInterfaceAdapter : IFileSystemSf
    {
        private ReferenceCountedDisposable<IFileSystem> _baseFileSystem;
        private PathFlags _pathFlags;

        // This field is always false in FS 12.0.0. Not sure what it's actually used for.
        private bool _allowAllOperations;

        // In FS, FileSystemInterfaceAdapter is derived from ISharedObject, so that's used for ref-counting when
        // creating files and directories. We don't have an ISharedObject, so a self-reference is used instead.
        private ReferenceCountedDisposable<FileSystemInterfaceAdapter>.WeakReference _selfReference;

        private FileSystemInterfaceAdapter(ref ReferenceCountedDisposable<IFileSystem> fileSystem,
            bool allowAllOperations)
        {
            _baseFileSystem = Shared.Move(ref fileSystem);
            _allowAllOperations = allowAllOperations;
        }

        private FileSystemInterfaceAdapter(ref ReferenceCountedDisposable<IFileSystem> fileSystem, PathFlags flags,
            bool allowAllOperations)
        {
            _baseFileSystem = Shared.Move(ref fileSystem);
            _pathFlags = flags;
            _allowAllOperations = allowAllOperations;
        }

        public static ReferenceCountedDisposable<IFileSystemSf> CreateShared(
            ref ReferenceCountedDisposable<IFileSystem> baseFileSystem, bool allowAllOperations)
        {
            ReferenceCountedDisposable<FileSystemInterfaceAdapter> sharedAdapter = null;
            try
            {
                var adapter = new FileSystemInterfaceAdapter(ref baseFileSystem, allowAllOperations);
                sharedAdapter = new ReferenceCountedDisposable<FileSystemInterfaceAdapter>(adapter);

                adapter._selfReference =
                    new ReferenceCountedDisposable<FileSystemInterfaceAdapter>.WeakReference(sharedAdapter);

                return sharedAdapter.AddReference<IFileSystemSf>();
            }
            finally
            {
                sharedAdapter?.Dispose();
            }
        }

        public static ReferenceCountedDisposable<IFileSystemSf> CreateShared(
            ref ReferenceCountedDisposable<IFileSystem> baseFileSystem, PathFlags flags, bool allowAllOperations)
        {
            ReferenceCountedDisposable<FileSystemInterfaceAdapter> sharedAdapter = null;
            try
            {
                var adapter = new FileSystemInterfaceAdapter(ref baseFileSystem, flags, allowAllOperations);
                sharedAdapter = new ReferenceCountedDisposable<FileSystemInterfaceAdapter>(adapter);

                adapter._selfReference =
                    new ReferenceCountedDisposable<FileSystemInterfaceAdapter>.WeakReference(sharedAdapter);

                return sharedAdapter.AddReference<IFileSystemSf>();
            }
            finally
            {
                sharedAdapter?.Dispose();
            }
        }

        public void Dispose()
        {
            ReferenceCountedDisposable<IFileSystem> tempFs = Shared.Move(ref _baseFileSystem);
            tempFs?.Dispose();
        }

        private static ReadOnlySpan<byte> RootDir => new[] { (byte)'/' };

        private Result SetUpPath(ref Path fsPath, in PathSf sfPath)
        {
            Result rc;

            if (_pathFlags.IsWindowsPathAllowed())
            {
                rc = fsPath.InitializeWithReplaceUnc(sfPath.Str);
                if (rc.IsFailure()) return rc;
            }
            else
            {
                rc = fsPath.Initialize(sfPath.Str);
                if (rc.IsFailure()) return rc;
            }

            rc = fsPath.Normalize(_pathFlags);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        public Result CreateFile(in PathSf path, long size, int option)
        {
            if (size < 0)
                return ResultFs.InvalidSize.Log();

            var pathNormalized = new Path();
            Result rc = SetUpPath(ref pathNormalized, in path);
            if (rc.IsFailure()) return rc;

            rc = _baseFileSystem.Target.CreateFile(in pathNormalized, size, (CreateFileOptions)option);
            if (rc.IsFailure()) return rc;

            pathNormalized.Dispose();
            return Result.Success;
        }

        public Result DeleteFile(in PathSf path)
        {
            var pathNormalized = new Path();
            Result rc = SetUpPath(ref pathNormalized, in path);
            if (rc.IsFailure()) return rc;

            rc = _baseFileSystem.Target.DeleteFile(in pathNormalized);
            if (rc.IsFailure()) return rc;

            pathNormalized.Dispose();
            return Result.Success;
        }

        public Result CreateDirectory(in PathSf path)
        {
            var pathNormalized = new Path();
            Result rc = SetUpPath(ref pathNormalized, in path);
            if (rc.IsFailure()) return rc;

            if (pathNormalized == RootDir)
                return ResultFs.PathAlreadyExists.Log();

            rc = _baseFileSystem.Target.CreateDirectory(in pathNormalized);
            if (rc.IsFailure()) return rc;

            pathNormalized.Dispose();
            return Result.Success;
        }

        public Result DeleteDirectory(in PathSf path)
        {
            var pathNormalized = new Path();
            Result rc = SetUpPath(ref pathNormalized, in path);
            if (rc.IsFailure()) return rc;

            if (pathNormalized == RootDir)
                return ResultFs.DirectoryNotDeletable.Log();

            rc = _baseFileSystem.Target.DeleteDirectory(in pathNormalized);
            if (rc.IsFailure()) return rc;

            pathNormalized.Dispose();
            return Result.Success;
        }

        public Result DeleteDirectoryRecursively(in PathSf path)
        {
            var pathNormalized = new Path();
            Result rc = SetUpPath(ref pathNormalized, in path);
            if (rc.IsFailure()) return rc;

            if (pathNormalized == RootDir)
                return ResultFs.DirectoryNotDeletable.Log();

            rc = _baseFileSystem.Target.DeleteDirectoryRecursively(in pathNormalized);
            if (rc.IsFailure()) return rc;

            pathNormalized.Dispose();
            return Result.Success;
        }

        public Result CleanDirectoryRecursively(in PathSf path)
        {
            var pathNormalized = new Path();
            Result rc = SetUpPath(ref pathNormalized, in path);
            if (rc.IsFailure()) return rc;

            rc = _baseFileSystem.Target.CleanDirectoryRecursively(in pathNormalized);
            if (rc.IsFailure()) return rc;

            pathNormalized.Dispose();
            return Result.Success;
        }

        public Result RenameFile(in PathSf currentPath, in PathSf newPath)
        {
            var currentPathNormalized = new Path();
            Result rc = SetUpPath(ref currentPathNormalized, in currentPath);
            if (rc.IsFailure()) return rc;

            var newPathNormalized = new Path();
            rc = SetUpPath(ref newPathNormalized, in newPath);
            if (rc.IsFailure()) return rc;

            rc = _baseFileSystem.Target.RenameFile(in currentPathNormalized, in newPathNormalized);
            if (rc.IsFailure()) return rc;

            currentPathNormalized.Dispose();
            newPathNormalized.Dispose();
            return Result.Success;
        }

        public Result RenameDirectory(in PathSf currentPath, in PathSf newPath)
        {
            var currentPathNormalized = new Path();
            Result rc = SetUpPath(ref currentPathNormalized, in currentPath);
            if (rc.IsFailure()) return rc;

            var newPathNormalized = new Path();
            rc = SetUpPath(ref newPathNormalized, in newPath);
            if (rc.IsFailure()) return rc;

            if (PathUtility.IsSubPath(currentPathNormalized.GetString(), newPathNormalized.GetString()))
                return ResultFs.DirectoryNotRenamable.Log();

            rc = _baseFileSystem.Target.RenameDirectory(in currentPathNormalized, in newPathNormalized);
            if (rc.IsFailure()) return rc;

            currentPathNormalized.Dispose();
            newPathNormalized.Dispose();
            return Result.Success;
        }

        public Result GetEntryType(out uint entryType, in PathSf path)
        {
            UnsafeHelpers.SkipParamInit(out entryType);

            var pathNormalized = new Path();
            Result rc = SetUpPath(ref pathNormalized, in path);
            if (rc.IsFailure()) return rc;

            rc = _baseFileSystem.Target.GetEntryType(out DirectoryEntryType type, in pathNormalized);
            if (rc.IsFailure()) return rc;

            entryType = (uint)type;
            pathNormalized.Dispose();
            return Result.Success;
        }

        public Result GetFreeSpaceSize(out long freeSpace, in PathSf path)
        {
            UnsafeHelpers.SkipParamInit(out freeSpace);

            var pathNormalized = new Path();
            Result rc = SetUpPath(ref pathNormalized, in path);
            if (rc.IsFailure()) return rc;

            rc = _baseFileSystem.Target.GetFreeSpaceSize(out long space, in pathNormalized);
            if (rc.IsFailure()) return rc;

            freeSpace = space;
            pathNormalized.Dispose();
            return Result.Success;
        }

        public Result GetTotalSpaceSize(out long totalSpace, in PathSf path)
        {
            UnsafeHelpers.SkipParamInit(out totalSpace);

            var pathNormalized = new Path();
            Result rc = SetUpPath(ref pathNormalized, in path);
            if (rc.IsFailure()) return rc;

            rc = _baseFileSystem.Target.GetTotalSpaceSize(out long space, in pathNormalized);
            if (rc.IsFailure()) return rc;

            totalSpace = space;
            pathNormalized.Dispose();
            return Result.Success;
        }

        public Result OpenFile(out ReferenceCountedDisposable<IFileSf> file, in PathSf path, uint mode)
        {
            const int maxTryCount = 2;
            UnsafeHelpers.SkipParamInit(out file);

            var pathNormalized = new Path();
            Result rc = SetUpPath(ref pathNormalized, in path);
            if (rc.IsFailure()) return rc;

            IFile fileInterface = null;
            try
            {
                for (int tryNum = 0; tryNum < maxTryCount; tryNum++)
                {
                    rc = _baseFileSystem.Target.OpenFile(out fileInterface, in pathNormalized, (OpenMode)mode);

                    // Retry on ResultDataCorrupted
                    if (!ResultFs.DataCorrupted.Includes(rc))
                        break;
                }

                if (rc.IsFailure()) return rc;

                ReferenceCountedDisposable<FileSystemInterfaceAdapter> selfReference = _selfReference.AddReference();
                var adapter = new FileInterfaceAdapter(Shared.Move(ref fileInterface), ref selfReference, _allowAllOperations);
                file = new ReferenceCountedDisposable<IFileSf>(adapter);

                pathNormalized.Dispose();
                return Result.Success;
            }
            finally
            {
                fileInterface?.Dispose();
            }
        }

        public Result OpenDirectory(out ReferenceCountedDisposable<IDirectorySf> directory, in PathSf path, uint mode)
        {
            const int maxTryCount = 2;
            UnsafeHelpers.SkipParamInit(out directory);

            var pathNormalized = new Path();
            Result rc = SetUpPath(ref pathNormalized, in path);
            if (rc.IsFailure()) return rc;

            IDirectory dirInterface = null;
            try
            {
                for (int tryNum = 0; tryNum < maxTryCount; tryNum++)
                {
                    rc = _baseFileSystem.Target.OpenDirectory(out dirInterface, in pathNormalized,
                        (OpenDirectoryMode)mode);

                    // Retry on ResultDataCorrupted
                    if (!ResultFs.DataCorrupted.Includes(rc))
                        break;
                }

                if (rc.IsFailure()) return rc;

                ReferenceCountedDisposable<FileSystemInterfaceAdapter> selfReference = _selfReference.AddReference();
                var adapter = new DirectoryInterfaceAdapter(dirInterface, ref selfReference);
                directory = new ReferenceCountedDisposable<IDirectorySf>(adapter);

                pathNormalized.Dispose();
                return Result.Success;
            }
            finally
            {
                dirInterface?.Dispose();
            }
        }

        public Result Commit()
        {
            return _baseFileSystem.Target.Commit();
        }

        public Result GetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in PathSf path)
        {
            UnsafeHelpers.SkipParamInit(out timeStamp);

            var pathNormalized = new Path();
            Result rc = SetUpPath(ref pathNormalized, in path);
            if (rc.IsFailure()) return rc;

            rc = _baseFileSystem.Target.GetFileTimeStampRaw(out FileTimeStampRaw tempTimeStamp, in pathNormalized);
            if (rc.IsFailure()) return rc;

            timeStamp = tempTimeStamp;
            pathNormalized.Dispose();
            return Result.Success;
        }

        public Result QueryEntry(OutBuffer outBuffer, InBuffer inBuffer, int queryId, in PathSf path)
        {
            static Result PermissionCheck(QueryId queryId, FileSystemInterfaceAdapter fsAdapter)
            {
                if (queryId == QueryId.SetConcatenationFileAttribute ||
                    queryId == QueryId.IsSignedSystemPartitionOnSdCardValid ||
                    queryId == QueryId.QueryUnpreparedFileInformation)
                {
                    return Result.Success;
                }

                if (!fsAdapter._allowAllOperations)
                    return ResultFs.PermissionDenied.Log();

                return Result.Success;
            }

            Result rc = PermissionCheck((QueryId)queryId, this);
            if (rc.IsFailure()) return rc;

            var pathNormalized = new Path();
            rc = SetUpPath(ref pathNormalized, in path);
            if (rc.IsFailure()) return rc;

            rc = _baseFileSystem.Target.QueryEntry(outBuffer.Buffer, inBuffer.Buffer, (QueryId)queryId,
                in pathNormalized);
            if (rc.IsFailure()) return rc;

            pathNormalized.Dispose();
            return Result.Success;
        }

        public Result GetImpl(out ReferenceCountedDisposable<IFileSystem> fileSystem)
        {
            fileSystem = _baseFileSystem.AddReference();
            return Result.Success;
        }
    }
}
