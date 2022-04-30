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
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions for mounting the directories where content is stored.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
[SkipLocalsInit]
public static class ContentStorage
{
    private class ContentStorageCommonMountNameGenerator : ICommonMountNameGenerator
    {
        private ContentStorageId _storageId;

        public ContentStorageCommonMountNameGenerator(ContentStorageId storageId)
        {
            _storageId = storageId;
        }

        public void Dispose() { }

        public Result GenerateCommonMountName(Span<byte> nameBuffer)
        {
            ReadOnlySpan<byte> mountName = GetContentStorageMountName(_storageId);

            // Add 2 for the mount name separator and null terminator
            int requiredNameBufferSize = StringUtils.GetLength(mountName, PathTool.MountNameLengthMax) + 2;

            Assert.SdkRequiresGreaterEqual(nameBuffer.Length, requiredNameBufferSize);

            var sb = new U8StringBuilder(nameBuffer);
            sb.Append(mountName).Append(StringTraits.DriveSeparator);

            Assert.SdkEqual(sb.Length, requiredNameBufferSize - 1);

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

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
            using var fileSystem = new SharedRef<IFileSystemSf>();

            for (int i = 0; i < maxRetries; i++)
            {
                rc = fileSystemProxy.Get.OpenContentStorageFileSystem(ref fileSystem.Ref(), storageId);

                if (rc.IsSuccess())
                    break;

                if (!ResultFs.SystemPartitionNotReady.Includes(rc))
                    return rc;

                // Note: Nintendo has an off-by-one error where they check if
                // "i == maxRetries" instead of "i == maxRetries - 1"
                if (i == maxRetries - 1)
                    return rc;

                fs.Hos.Os.SleepThread(TimeSpan.FromMilliSeconds(retryInterval));
            }

            using var fileSystemAdapter =
                new UniqueRef<IFileSystem>(new FileSystemServiceObjectAdapter(ref fileSystem.Ref()));

            if (!fileSystemAdapter.HasValue)
                return ResultFs.AllocationMemoryFailedInContentStorageA.Log();

            using var mountNameGenerator =
                new UniqueRef<ICommonMountNameGenerator>(new ContentStorageCommonMountNameGenerator(storageId));

            if (!mountNameGenerator.HasValue)
                return ResultFs.AllocationMemoryFailedInContentStorageB.Log();

            rc = fs.Register(mountName, ref fileSystemAdapter.Ref(), ref mountNameGenerator.Ref());
            if (rc.IsFailure()) return rc.Miss();

            return Result.Success;
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