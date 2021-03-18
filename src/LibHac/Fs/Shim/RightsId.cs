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
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            return fsProxy.Target.GetRightsId(out rightsId, programId, storageId);
        }

        public static Result GetRightsId(this FileSystemClient fs, out FsRightsId rightsId, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out rightsId);

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            Result rc = FspPath.FromSpan(out FspPath sfPath, path);
            if (rc.IsFailure()) return rc;

            return fsProxy.Target.GetRightsIdByPath(out rightsId, in sfPath);
        }

        public static Result GetRightsId(this FileSystemClient fs, out FsRightsId rightsId, out byte keyGeneration, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out rightsId, out keyGeneration);

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            Result rc = FspPath.FromSpan(out FspPath sfPath, path);
            if (rc.IsFailure()) return rc;

            return fsProxy.Target.GetRightsIdAndKeyGenerationByPath(out rightsId, out keyGeneration, in sfPath);
        }

        public static Result RegisterExternalKey(this FileSystemClient fs, in FsRightsId rightsId, in AccessKey key)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            return fsProxy.Target.RegisterExternalKey(in rightsId, in key);
        }

        public static Result UnregisterExternalKey(this FileSystemClient fs, ref FsRightsId rightsId)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            return fsProxy.Target.UnregisterExternalKey(in rightsId);
        }

        public static Result UnregisterAllExternalKey(this FileSystemClient fs)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            return fsProxy.Target.UnregisterAllExternalKey();
        }
    }
}
