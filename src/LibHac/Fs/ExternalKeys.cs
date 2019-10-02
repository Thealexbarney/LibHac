using LibHac.FsService;
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
