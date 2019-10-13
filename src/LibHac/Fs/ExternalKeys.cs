using LibHac.Common;
using LibHac.FsService;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Spl;

namespace LibHac.Fs
{
    public static class ExternalKeys
    {
        public static Result GetRightsId(this FileSystemClient fs, out RightsId rightsId, TitleId programId,
            StorageId storageId)
        {
            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            return fsProxy.GetRightsId(out rightsId, programId, storageId);
        }

        public static Result GetRightsId(this FileSystemClient fs, out RightsId rightsId, U8Span path)
        {
            rightsId = default;

            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = FsPath.FromSpan(out FsPath fsPath, path);
            if (rc.IsFailure()) return rc;

            return fsProxy.GetRightsIdByPath(out rightsId, ref fsPath);
        }

        public static Result GetRightsId(this FileSystemClient fs, out RightsId rightsId, out byte keyGeneration, U8Span path)
        {
            rightsId = default;
            keyGeneration = default;

            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = FsPath.FromSpan(out FsPath fsPath, path);
            if (rc.IsFailure()) return rc;

            return fsProxy.GetRightsIdAndKeyGenerationByPath(out rightsId, out keyGeneration, ref fsPath);
        }

        public static Result RegisterExternalKey(this FileSystemClient fs, ref RightsId rightsId, ref AccessKey key)
        {
            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            return fsProxy.RegisterExternalKey(ref rightsId, ref key);
        }

        public static Result UnregisterExternalKey(this FileSystemClient fs, ref RightsId rightsId)
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
