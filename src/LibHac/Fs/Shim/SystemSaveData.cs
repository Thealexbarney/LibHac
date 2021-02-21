using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Ncm;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim
{
    public static class SystemSaveData
    {
        public static Result MountSystemSaveData(this FileSystemClient fs, U8Span mountName, SaveDataSpaceId spaceId,
            ulong saveDataId)
        {
            return MountSystemSaveData(fs, mountName, spaceId, saveDataId, UserId.InvalidId);
        }

        public static Result MountSystemSaveData(this FileSystemClient fs, U8Span mountName,
            SaveDataSpaceId spaceId, ulong saveDataId, UserId userId)
        {
            Result rc = MountHelpers.CheckMountName(mountName);
            if (rc.IsFailure()) return rc;

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            var attribute = new SaveDataAttribute(ProgramId.InvalidId, SaveDataType.System, userId, saveDataId);

            ReferenceCountedDisposable<IFileSystemSf> saveFs = null;

            try
            {
                rc = fsProxy.Target.OpenSaveDataFileSystemBySystemSaveDataId(out saveFs, spaceId, in attribute);
                if (rc.IsFailure()) return rc;

                var fileSystemAdapter = new FileSystemServiceObjectAdapter(saveFs);
                return fs.Register(mountName, fileSystemAdapter);
            }
            finally
            {
                saveFs?.Dispose();
            }
        }
    }
}
