using LibHac.Common;
using LibHac.FsSrv.Sf;
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

            Result rc = FspPath.FromSpan(out FspPath sfPath, path);
            if (rc.IsFailure()) return rc;

            return fsProxy.GetRightsIdByPath(out rightsId, in sfPath);
        }

        public static Result GetRightsId(this FileSystemClient fs, out FsRightsId rightsId, out byte keyGeneration, U8Span path)
        {
            rightsId = default;
            keyGeneration = default;

            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = FspPath.FromSpan(out FspPath sfPath, path);
            if (rc.IsFailure()) return rc;

            return fsProxy.GetRightsIdAndKeyGenerationByPath(out rightsId, out keyGeneration, in sfPath);
        }

        public static Result RegisterExternalKey(this FileSystemClient fs, in FsRightsId rightsId, in AccessKey key)
        {
            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            return fsProxy.RegisterExternalKey(in rightsId, in key);
        }

        public static Result UnregisterExternalKey(this FileSystemClient fs, ref FsRightsId rightsId)
        {
            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            return fsProxy.UnregisterExternalKey(in rightsId);
        }

        public static Result UnregisterAllExternalKey(this FileSystemClient fs)
        {
            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            return fsProxy.UnregisterAllExternalKey();
        }
    }
}
