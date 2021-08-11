using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.FsSrv.Sf;
using LibHac.Util;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;
using IFileSf = LibHac.FsSrv.Sf.IFile;
using IDirectorySf = LibHac.FsSrv.Sf.IDirectory;

namespace LibHac.Fs.Impl
{
    /// <summary>
    /// An adapter for using an <see cref="IFileSystemSf"/> service object as an <see cref="Fsa.IFileSystem"/>. Used
    /// when receiving a Horizon IPC file system object so it can be used as an <see cref="Fsa.IFileSystem"/> locally.
    /// </summary>
    internal class FileSystemServiceObjectAdapter : Fsa.IFileSystem, IMultiCommitTarget
    {
        private ReferenceCountedDisposable<IFileSystemSf> BaseFs { get; }

        public FileSystemServiceObjectAdapter(ReferenceCountedDisposable<IFileSystemSf> baseFileSystem)
        {
            BaseFs = baseFileSystem.AddReference();
        }

        protected override Result DoCreateFile(U8Span path, long size, CreateFileOptions option)
        {
            Result rc = GetPathForServiceObject(out Path sfPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFs.Target.CreateFile(in sfPath, size, (int)option);
        }

        protected override Result DoDeleteFile(U8Span path)
        {
            Result rc = GetPathForServiceObject(out Path sfPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFs.Target.DeleteFile(in sfPath);
        }

        protected override Result DoCreateDirectory(U8Span path)
        {
            Result rc = GetPathForServiceObject(out Path sfPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFs.Target.CreateDirectory(in sfPath);
        }

        protected override Result DoDeleteDirectory(U8Span path)
        {
            Result rc = GetPathForServiceObject(out Path sfPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFs.Target.DeleteDirectory(in sfPath);
        }

        protected override Result DoDeleteDirectoryRecursively(U8Span path)
        {
            Result rc = GetPathForServiceObject(out Path sfPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFs.Target.DeleteDirectoryRecursively(in sfPath);
        }

        protected override Result DoCleanDirectoryRecursively(U8Span path)
        {
            Result rc = GetPathForServiceObject(out Path sfPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFs.Target.CleanDirectoryRecursively(in sfPath);
        }

        protected override Result DoRenameFile(U8Span oldPath, U8Span newPath)
        {
            Result rc = GetPathForServiceObject(out Path oldSfPath, oldPath);
            if (rc.IsFailure()) return rc;

            rc = GetPathForServiceObject(out Path newSfPath, newPath);
            if (rc.IsFailure()) return rc;

            return BaseFs.Target.RenameFile(in oldSfPath, in newSfPath);
        }

        protected override Result DoRenameDirectory(U8Span oldPath, U8Span newPath)
        {
            Result rc = GetPathForServiceObject(out Path oldSfPath, oldPath);
            if (rc.IsFailure()) return rc;

            rc = GetPathForServiceObject(out Path newSfPath, newPath);
            if (rc.IsFailure()) return rc;

            return BaseFs.Target.RenameDirectory(in oldSfPath, in newSfPath);
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out entryType);

            Result rc = GetPathForServiceObject(out Path sfPath, path);
            if (rc.IsFailure()) return rc;

            ref uint sfEntryType = ref Unsafe.As<DirectoryEntryType, uint>(ref entryType);

            return BaseFs.Target.GetEntryType(out sfEntryType, in sfPath);
        }

        protected override Result DoGetFreeSpaceSize(out long freeSpace, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out freeSpace);

            Result rc = GetPathForServiceObject(out Path sfPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFs.Target.GetFreeSpaceSize(out freeSpace, in sfPath);
        }

        protected override Result DoGetTotalSpaceSize(out long totalSpace, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out totalSpace);

            Result rc = GetPathForServiceObject(out Path sfPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFs.Target.GetTotalSpaceSize(out totalSpace, in sfPath);
        }

        protected override Result DoOpenFile(out Fsa.IFile file, U8Span path, OpenMode mode)
        {
            UnsafeHelpers.SkipParamInit(out file);

            Result rc = GetPathForServiceObject(out Path sfPath, path);
            if (rc.IsFailure()) return rc;

            ReferenceCountedDisposable<IFileSf> sfFile = null;
            try
            {
                rc = BaseFs.Target.OpenFile(out sfFile, in sfPath, (uint)mode);
                if (rc.IsFailure()) return rc;

                file = new FileServiceObjectAdapter(sfFile);
                return Result.Success;
            }
            finally
            {
                sfFile?.Dispose();
            }
        }

        protected override Result DoOpenDirectory(out Fsa.IDirectory directory, U8Span path, OpenDirectoryMode mode)
        {
            UnsafeHelpers.SkipParamInit(out directory);

            Result rc = GetPathForServiceObject(out Path sfPath, path);
            if (rc.IsFailure()) return rc;

            ReferenceCountedDisposable<IDirectorySf> sfDir = null;
            try
            {
                rc = BaseFs.Target.OpenDirectory(out sfDir, in sfPath, (uint)mode);
                if (rc.IsFailure()) return rc;

                directory = new DirectoryServiceObjectAdapter(sfDir);
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

        protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out timeStamp);

            Result rc = GetPathForServiceObject(out Path sfPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFs.Target.GetFileTimeStampRaw(out timeStamp, in sfPath);
        }

        protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
            U8Span path)
        {
            Result rc = GetPathForServiceObject(out Path sfPath, path);
            if (rc.IsFailure()) return rc;

            return BaseFs.Target.QueryEntry(outBuffer, inBuffer, (int)queryId, in sfPath);
        }

        public ReferenceCountedDisposable<IFileSystemSf> GetMultiCommitTarget()
        {
            return BaseFs.AddReference();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                BaseFs?.Dispose();
            }

            base.Dispose(disposing);
        }

        private Result GetPathForServiceObject(out Path sfPath, U8Span path)
        {
            // This is the function used to create Sf.Path structs. Get an unsafe byte span for init only.
            UnsafeHelpers.SkipParamInit(out sfPath);
            Span<byte> outPath = SpanHelpers.AsByteSpan(ref sfPath);

            // Copy and null terminate
            StringUtils.Copy(outPath, path);
            outPath[Unsafe.SizeOf<Path>() - 1] = StringTraits.NullTerminator;

            // Replace directory separators
            PathUtility.Replace(outPath, StringTraits.AltDirectorySeparator, StringTraits.DirectorySeparator);

            // Get lengths
            int windowsSkipLength = WindowsPath.GetWindowsPathSkipLength(path);
            var nonWindowsPath = new U8Span(sfPath.Str.Slice(windowsSkipLength));
            int maxLength = PathTool.EntryNameLengthMax - windowsSkipLength;
            return PathUtility.VerifyPath(null, nonWindowsPath, maxLength, maxLength);
        }
    }
}
