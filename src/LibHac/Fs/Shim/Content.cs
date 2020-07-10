using System;
using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.FsService;
using LibHac.FsSystem;
using LibHac.Ncm;

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

            rc = fsProxy.OpenFileSystemWithPatch(out IFileSystem fileSystem, programId, fspType);
            if (rc.IsFailure()) return rc;

            return fs.Register(mountName, fileSystem);
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
            FsPath.FromSpan(out FsPath fsPath, path);

            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.OpenFileSystemWithId(out IFileSystem fileSystem, ref fsPath, id, type);
            if (rc.IsFailure()) return rc;

            return fs.Register(mountName, fileSystem);
        }

        private static FileSystemProxyType ConvertToFileSystemProxyType(ContentType type) => type switch
        {
            ContentType.Meta => FileSystemProxyType.Meta,
            ContentType.Control => FileSystemProxyType.Control,
            ContentType.Manual => FileSystemProxyType.Manual,
            ContentType.Data => FileSystemProxyType.Data,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }
}
