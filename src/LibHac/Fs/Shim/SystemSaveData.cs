using LibHac.Common;
using LibHac.FsService;

namespace LibHac.Fs.Shim
{
    public static class SystemSaveData
    {
        public static Result MountSystemSaveData(this FileSystemClient fs, U8Span mountName, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            return MountSystemSaveData(fs, mountName, spaceId, saveDataId, UserId.Zero);
        }

        public static Result MountSystemSaveData(this FileSystemClient fs, U8Span mountName,
            SaveDataSpaceId spaceId, ulong saveDataId, UserId userId)
        {
            Result rc = MountHelpers.CheckMountName(mountName);
            if (rc.IsFailure()) return rc;

            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            SaveDataAttribute attribute = default;
            attribute.UserId = userId;
            attribute.SaveDataId = saveDataId;

            rc = fsProxy.OpenSaveDataFileSystemBySystemSaveDataId(out IFileSystem fileSystem, spaceId, ref attribute);
            if (rc.IsFailure()) return rc;

            return fs.Register(mountName, fileSystem);
        }
    }
}
