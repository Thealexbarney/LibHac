using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Os;
using LibHac.Util;
using static LibHac.Fs.Impl.AccessLogStrings;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim
{
    [SkipLocalsInit]
    public static class ContentStorage
    {
        private class ContentStorageCommonMountNameGenerator : ICommonMountNameGenerator
        {
            private ContentStorageId StorageId { get; }

            public ContentStorageCommonMountNameGenerator(ContentStorageId storageId)
            {
                StorageId = storageId;
            }

            public void Dispose() { }

            public Result GenerateCommonMountName(Span<byte> nameBuffer)
            {
                // Determine how much space we need.
                int neededSize =
                    StringUtils.GetLength(GetContentStorageMountName(StorageId), PathTool.MountNameLengthMax) + 2;

                Assert.SdkRequiresGreaterEqual(nameBuffer.Length, neededSize);

                // Generate the name.
                var sb = new U8StringBuilder(nameBuffer);
                sb.Append(GetContentStorageMountName(StorageId))
                    .Append(StringTraits.DriveSeparator);

                Assert.SdkEqual(sb.Length, neededSize - 1);

                return Result.Success;
            }
        }

        public static Result MountContentStorage(this FileSystemClient fs, ContentStorageId storageId)
        {
            return MountContentStorage(fs, new U8Span(GetContentStorageMountName(storageId)), storageId);
        }

        public static Result MountContentStorage(this FileSystemClient fs, U8Span mountName, ContentStorageId storageId)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x40];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = Mount(fs, mountName, storageId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogName).Append(mountName).Append(LogQuote)
                    .Append(LogContentStorageId).Append(idString.ToString(storageId));

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = Mount(fs, mountName, storageId);
            }
            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
                fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

            return Result.Success;

            static Result Mount(FileSystemClient fs, U8Span mountName, ContentStorageId storageId)
            {
                // It can take some time for the system partition to be ready (if it's on the SD card).
                // Thus, we will retry up to 10 times, waiting one second each time.
                const int maxRetries = 10;
                const int retryInterval = 1000;

                Result rc = fs.Impl.CheckMountNameAcceptingReservedMountName(mountName);
                if (rc.IsFailure()) return rc;

                using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

                ReferenceCountedDisposable<IFileSystemSf> fileSystem = null;
                try
                {
                    for (int i = 0; i < maxRetries; i++)
                    {
                        rc = fsProxy.Target.OpenContentStorageFileSystem(out fileSystem, storageId);

                        if (rc.IsSuccess())
                            break;

                        if (!ResultFs.SystemPartitionNotReady.Includes(rc))
                            return rc;

                        if (i == maxRetries - 1)
                            return rc;

                        fs.Hos.Os.SleepThread(TimeSpan.FromMilliSeconds(retryInterval));
                    }

                    var fileSystemAdapter = new FileSystemServiceObjectAdapter(fileSystem);
                    var mountNameGenerator = new ContentStorageCommonMountNameGenerator(storageId);
                    return fs.Register(mountName, fileSystemAdapter, mountNameGenerator);
                }
                finally
                {
                    fileSystem?.Dispose();
                }
            }
        }

        public static ReadOnlySpan<byte> GetContentStorageMountName(ContentStorageId storageId)
        {
            switch (storageId)
            {
                case ContentStorageId.System:
                    return CommonMountNames.ContentStorageSystemMountName;
                case ContentStorageId.User:
                    return CommonMountNames.ContentStorageUserMountName;
                case ContentStorageId.SdCard:
                    return CommonMountNames.ContentStorageSdCardMountName;
                default:
                    Abort.UnexpectedDefault();
                    return default;
            }
        }
    }
}
