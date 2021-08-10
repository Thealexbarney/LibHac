using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.Sf;
using LibHac.Util;

using IFile = LibHac.Fs.Fsa.IFile;
using IFileSf = LibHac.FsSrv.Sf.IFile;
using IDirectory = LibHac.Fs.Fsa.IDirectory;
using IDirectorySf = LibHac.FsSrv.Sf.IDirectory;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;
using PathSf = LibHac.FsSrv.Sf.Path;

// ReSharper disable CheckNamespace
namespace LibHac.Fs.Impl
{
    /// <summary>
    /// An adapter for using an <see cref="IFileSf"/> service object as an <see cref="IFile"/>. Used
    /// when receiving a Horizon IPC file object so it can be used as an <see cref="IFile"/> locally.
    /// </summary>
    /// <remarks>Based on FS 12.0.3 (nnSdk 12.3.1)</remarks>
    internal class FileServiceObjectAdapter : IFile
    {
        private ReferenceCountedDisposable<IFileSf> BaseFile { get; }

        public FileServiceObjectAdapter(ReferenceCountedDisposable<IFileSf> baseFile)
        {
            BaseFile = baseFile.AddReference();
        }

        public override void Dispose()
        {
            BaseFile?.Dispose();

            base.Dispose();
        }

        protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination, in ReadOption option)
        {
            return BaseFile.Target.Read(out bytesRead, offset, new OutBuffer(destination), destination.Length, option);
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
        {
            return BaseFile.Target.Write(offset, new InBuffer(source), source.Length, option);
        }

        protected override Result DoFlush()
        {
            return BaseFile.Target.Flush();
        }

        protected override Result DoSetSize(long size)
        {
            return BaseFile.Target.SetSize(size);
        }

        protected override Result DoGetSize(out long size)
        {
            return BaseFile.Target.GetSize(out size);
        }

        protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            switch (operationId)
            {
                case OperationId.InvalidateCache:
                    return BaseFile.Target.OperateRange(out _, (int)OperationId.InvalidateCache, offset, size);
                case OperationId.QueryRange:
                    if (outBuffer.Length != Unsafe.SizeOf<QueryRangeInfo>())
                        return ResultFs.InvalidSize.Log();

                    ref QueryRangeInfo info = ref SpanHelpers.AsStruct<QueryRangeInfo>(outBuffer);

                    return BaseFile.Target.OperateRange(out info, (int)OperationId.QueryRange, offset, size);
                default:
                    return BaseFile.Target.OperateRangeWithBuffer(new OutBuffer(outBuffer), new InBuffer(inBuffer),
                        (int)operationId, offset, size);
            }
        }
    }

    /// <summary>
    /// An adapter for using an <see cref="IDirectorySf"/> service object as an <see cref="IDirectory"/>. Used
    /// when receiving a Horizon IPC directory object so it can be used as an <see cref="IDirectory"/> locally.
    /// </summary>
    /// <remarks>Based on FS 12.0.3 (nnSdk 12.3.1)</remarks>
    internal class DirectoryServiceObjectAdapter : IDirectory
    {
        private ReferenceCountedDisposable<IDirectorySf> BaseDirectory { get; }

        public DirectoryServiceObjectAdapter(ReferenceCountedDisposable<IDirectorySf> baseDirectory)
        {
            BaseDirectory = baseDirectory.AddReference();
        }

        public override void Dispose()
        {
            BaseDirectory?.Dispose();

            base.Dispose();
        }

        protected override Result DoRead(out long entriesRead, Span<DirectoryEntry> entryBuffer)
        {
            Span<byte> buffer = MemoryMarshal.Cast<DirectoryEntry, byte>(entryBuffer);
            return BaseDirectory.Target.Read(out entriesRead, new OutBuffer(buffer));
        }

        protected override Result DoGetEntryCount(out long entryCount)
        {
            return BaseDirectory.Target.GetEntryCount(out entryCount);
        }
    }

    /// <summary>
    /// An adapter for using an <see cref="IFileSystemSf"/> service object as an <see cref="IFileSystem"/>. Used
    /// when receiving a Horizon IPC file system object so it can be used as an <see cref="IFileSystem"/> locally.
    /// </summary>
    /// <remarks>Based on FS 12.0.3 (nnSdk 12.3.1)</remarks>
    internal class FileSystemServiceObjectAdapter : IFileSystem, IMultiCommitTarget
    {
        private ReferenceCountedDisposable<IFileSystemSf> BaseFs { get; }

        private static Result GetPathForServiceObject(out PathSf sfPath, in Path path)
        {
            UnsafeHelpers.SkipParamInit(out sfPath);

            int length = StringUtils.Copy(SpanHelpers.AsByteSpan(ref sfPath), path.GetString(),
                PathTool.EntryNameLengthMax + 1);

            if (length > PathTool.EntryNameLengthMax)
                return ResultFs.TooLongPath.Log();

            return Result.Success;
        }

        public FileSystemServiceObjectAdapter(ReferenceCountedDisposable<IFileSystemSf> baseFileSystem)
        {
            BaseFs = baseFileSystem.AddReference();
        }

        public override void Dispose()
        {
            BaseFs?.Dispose();
            base.Dispose();
        }

        protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option)
        {
            Result rc = GetPathForServiceObject(out PathSf sfPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFs.Target.CreateFile(in sfPath, size, (int)option);
        }

        protected override Result DoDeleteFile(in Path path)
        {
            Result rc = GetPathForServiceObject(out PathSf sfPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFs.Target.DeleteFile(in sfPath);
        }

        protected override Result DoCreateDirectory(in Path path)
        {
            Result rc = GetPathForServiceObject(out PathSf sfPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFs.Target.DeleteFile(in sfPath);
        }

        protected override Result DoDeleteDirectory(in Path path)
        {
            Result rc = GetPathForServiceObject(out PathSf sfPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFs.Target.DeleteDirectory(in sfPath);
        }

        protected override Result DoDeleteDirectoryRecursively(in Path path)
        {
            Result rc = GetPathForServiceObject(out PathSf sfPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFs.Target.DeleteDirectoryRecursively(in sfPath);
        }

        protected override Result DoCleanDirectoryRecursively(in Path path)
        {
            Result rc = GetPathForServiceObject(out PathSf sfPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFs.Target.CleanDirectoryRecursively(in sfPath);
        }

        protected override Result DoRenameFile(in Path currentPath, in Path newPath)
        {
            Result rc = GetPathForServiceObject(out PathSf currentSfPath, currentPath);
            if (rc.IsFailure()) return rc;

            rc = GetPathForServiceObject(out PathSf newSfPath, newPath);
            if (rc.IsFailure()) return rc;

            return BaseFs.Target.RenameFile(in currentSfPath, in newSfPath);
        }

        protected override Result DoRenameDirectory(in Path currentPath, in Path newPath)
        {
            Result rc = GetPathForServiceObject(out PathSf currentSfPath, currentPath);
            if (rc.IsFailure()) return rc;

            rc = GetPathForServiceObject(out PathSf newSfPath, newPath);
            if (rc.IsFailure()) return rc;

            return BaseFs.Target.RenameDirectory(in currentSfPath, in newSfPath);
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
        {
            UnsafeHelpers.SkipParamInit(out entryType);

            Result rc = GetPathForServiceObject(out PathSf sfPath, path);
            if (rc.IsFailure()) return rc;

            ref uint sfEntryType = ref Unsafe.As<DirectoryEntryType, uint>(ref entryType);

            return BaseFs.Target.GetEntryType(out sfEntryType, in sfPath);
        }

        protected override Result DoGetFreeSpaceSize(out long freeSpace, in Path path)
        {
            UnsafeHelpers.SkipParamInit(out freeSpace);

            Result rc = GetPathForServiceObject(out PathSf sfPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFs.Target.GetFreeSpaceSize(out freeSpace, in sfPath);
        }

        protected override Result DoGetTotalSpaceSize(out long totalSpace, in Path path)
        {
            UnsafeHelpers.SkipParamInit(out totalSpace);

            Result rc = GetPathForServiceObject(out PathSf sfPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFs.Target.GetTotalSpaceSize(out totalSpace, in sfPath);
        }

        protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
        {
            Result rc = GetPathForServiceObject(out PathSf sfPath, path);
            if (rc.IsFailure()) return rc;

            ReferenceCountedDisposable<IFileSf> sfFile = null;
            try
            {
                rc = BaseFs.Target.OpenFile(out sfFile, in sfPath, (uint)mode);
                if (rc.IsFailure()) return rc;

                outFile.Reset(new FileServiceObjectAdapter(sfFile));
                return Result.Success;
            }
            finally
            {
                sfFile?.Dispose();
            }
        }

        protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
            OpenDirectoryMode mode)
        {
            Result rc = GetPathForServiceObject(out PathSf sfPath, path);
            if (rc.IsFailure()) return rc;

            ReferenceCountedDisposable<IDirectorySf> sfDir = null;
            try
            {
                rc = BaseFs.Target.OpenDirectory(out sfDir, in sfPath, (uint)mode);
                if (rc.IsFailure()) return rc;

                outDirectory.Reset(new DirectoryServiceObjectAdapter(sfDir));
                return Result.Success;
            }
            finally
            {
                sfDir?.Dispose();
            }
        }

        protected override Result DoCommit()
        {
            return BaseFs.Target.Commit();
        }

        protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in Path path)
        {
            UnsafeHelpers.SkipParamInit(out timeStamp);

            Result rc = GetPathForServiceObject(out PathSf sfPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFs.Target.GetFileTimeStampRaw(out timeStamp, in sfPath);
        }

        protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
            in Path path)
        {
            Result rc = GetPathForServiceObject(out PathSf sfPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFs.Target.QueryEntry(new OutBuffer(outBuffer), new InBuffer(inBuffer), (int)queryId, in sfPath);
        }

        public ReferenceCountedDisposable<IFileSystemSf> GetFileSystem()
        {
            return BaseFs.AddReference();
        }

        public ReferenceCountedDisposable<IFileSystemSf> GetMultiCommitTarget()
        {
            return GetFileSystem();
        }
    }
}
