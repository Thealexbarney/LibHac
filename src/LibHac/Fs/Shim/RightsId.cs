using LibHac.Common;
using LibHac.FsService;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Spl;
using FsRightsId = LibHac.Fs.RightsId;

namespace LibHac.Fs.Shim
{
    public static class RightsId
    {
        public static Result GetRightsId(this FileSystemClient fs, out FsRightsId rightsId, ProgramId programId,
            StorageId storageId)
        {
            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            return fsProxy.GetRightsId(out rightsId, programId, storageId);
        }

        public static Result GetRightsId(this FileSystemClient fs, out FsRightsId rightsId, U8Span path)
        {
            rightsId = default;

            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = FsPath.FromSpan(out FsPath fsPath, path);
            if (rc.IsFailure()) return rc;

            return fsProxy.GetRightsIdByPath(out rightsId, ref fsPath);
        }

        public static Result GetRightsId(this FileSystemClient fs, out FsRightsId rightsId, out byte keyGeneration, U8Span path)
        {
            rightsId = default;
            keyGeneration = default;

            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = FsPath.FromSpan(out FsPath fsPath, path);
            if (rc.IsFailure()) return rc;

            return fsProxy.GetRightsIdAndKeyGenerationByPath(out rightsId, out keyGeneration, ref fsPath);
        }

        public static Result RegisterExternalKey(this FileSystemClient fs, ref FsRightsId rightsId, ref AccessKey key)
        {
            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            return fsProxy.RegisterExternalKey(ref rightsId, ref key);
        }

        public static Result UnregisterExternalKey(this FileSystemClient fs, ref FsRightsId rightsId)
        {
            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            return fsProxy.UnregisterExternalKey(ref rightsId);
        }

        public static Result UnregisterAllExternalKey(this FileSystemClient fs)
        {
            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            return fsProxy.UnregisterAllExternalKey();
        }
    }
}
