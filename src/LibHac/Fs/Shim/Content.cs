using System;
using LibHac.Common;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Ncm;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim
{
    public static class Content
    {
        // todo: add logging
        public static Result MountContent(this FileSystemClient fs, U8Span mountName, U8Span path, ContentType type)
        {
            if (type == ContentType.Meta)
                return ResultFs.InvalidArgument.Log();

            return MountContent(fs, mountName, path, ProgramId.InvalidId, type);
        }

        public static Result MountContent(this FileSystemClient fs, U8Span mountName, ProgramId programId, ContentType type)
        {
            Result rc = MountHelpers.CheckMountNameAcceptingReservedMountName(mountName);
            if (rc.IsFailure()) return rc;

            FileSystemProxyType fspType = ConvertToFileSystemProxyType(type);

            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            rc = fsProxy.OpenFileSystemWithPatch(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, programId,
                fspType);
            if (rc.IsFailure()) return rc;

            using (fileSystem)
            {
                var fileSystemAdapter = new FileSystemServiceObjectAdapter(fileSystem);

                return fs.Register(mountName, fileSystemAdapter);
            }
        }

        public static Result MountContent(this FileSystemClient fs, U8Span mountName, U8Span path, ProgramId programId, ContentType type)
        {
            Result rc = MountHelpers.CheckMountNameAcceptingReservedMountName(mountName);
            if (rc.IsFailure()) return rc;

            FileSystemProxyType fspType = ConvertToFileSystemProxyType(type);

            return MountContentImpl(fs, mountName, path, programId.Value, fspType);
        }

        public static Result MountContent(this FileSystemClient fs, U8Span mountName, U8Span path, DataId dataId, ContentType type)
        {
            Result rc = MountHelpers.CheckMountNameAcceptingReservedMountName(mountName);
            if (rc.IsFailure()) return rc;

            FileSystemProxyType fspType = ConvertToFileSystemProxyType(type);

            return MountContentImpl(fs, mountName, path, dataId.Value, fspType);
        }

        private static Result MountContentImpl(FileSystemClient fs, U8Span mountName, U8Span path, ulong id, FileSystemProxyType type)
        {
            FspPath.FromSpan(out FspPath fsPath, path);

            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.OpenFileSystemWithId(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, in fsPath,
                id, type);
            if (rc.IsFailure()) return rc;

            using (fileSystem)
            {
                var fileSystemAdapter = new FileSystemServiceObjectAdapter(fileSystem);

                return fs.Register(mountName, fileSystemAdapter);
            }
        }

        private static FileSystemProxyType ConvertToFileSystemProxyType(ContentType type) => type switch
        {
            ContentType.Meta => FileSystemProxyType.Meta,
            ContentType.Control => FileSystemProxyType.Control,
            ContentType.Manual => FileSystemProxyType.Manual,
            ContentType.Data => FileSystemProxyType.Data,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}
