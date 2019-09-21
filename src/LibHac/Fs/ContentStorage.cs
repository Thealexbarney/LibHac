using System;
using LibHac.Common;
using LibHac.FsService;

namespace LibHac.Fs
{
    public static class ContentStorage
    {
        private static readonly U8String ContentStorageMountNameSystem = new U8String("@SystemContent");
        private static readonly U8String ContentStorageMountNameUser = new U8String("@UserContent");
        private static readonly U8String ContentStorageMountNameSdCard = new U8String("@SdCardContent");

        public static Result MountContentStorage(this FileSystemClient fs, ContentStorageId storageId)
        {
            return MountContentStorage(fs, GetContentStorageMountName(storageId), storageId);
        }

        public static Result MountContentStorage(this FileSystemClient fs, U8Span mountName, ContentStorageId storageId)
        {
            Result rc = MountHelpers.CheckMountNameAcceptingReservedMountName(mountName);
            if (rc.IsFailure()) return rc;

            FileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            rc = fsProxy.OpenContentStorageFileSystem(out IFileSystem contentFs, storageId);
            if (rc.IsFailure()) return rc;

            var mountNameGenerator = new ContentStorageCommonMountNameGenerator(storageId);

            return fs.Register(mountName, contentFs, mountNameGenerator);
        }

        public static U8String GetContentStorageMountName(ContentStorageId storageId)
        {
            switch (storageId)
            {
                case ContentStorageId.System:
                    return ContentStorageMountNameSystem;
                case ContentStorageId.User:
                    return ContentStorageMountNameUser;
                case ContentStorageId.SdCard:
                    return ContentStorageMountNameSdCard;
                default:
                    throw new ArgumentOutOfRangeException(nameof(storageId), storageId, null);
            }
        }

        private class ContentStorageCommonMountNameGenerator : ICommonMountNameGenerator
        {
            private ContentStorageId StorageId { get; }

            public ContentStorageCommonMountNameGenerator(ContentStorageId storageId)
            {
                StorageId = storageId;
            }

            public Result Generate(Span<byte> nameBuffer)
            {
                U8String mountName = GetContentStorageMountName(StorageId);

                int length = StringUtils.Copy(nameBuffer, mountName);
                nameBuffer[length] = (byte)':';
                nameBuffer[length + 1] = 0;

                return Result.Success;
            }
        }
    }
}
