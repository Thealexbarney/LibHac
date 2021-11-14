using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Os;
using static LibHac.Fs.Impl.AccessLogStrings;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim;

[SkipLocalsInit]
public static class SdCard
{
    private static Result OpenSdCardFileSystem(FileSystemClient fs, ref SharedRef<IFileSystemSf> outFileSystem)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        return OpenFileSystem(fs, ref fileSystemProxy.Ref(), ref outFileSystem);

        static Result OpenFileSystem(FileSystemClient fs, ref SharedRef<IFileSystemProxy> fileSystemProxy,
            ref SharedRef<IFileSystemSf> outFileSystem)
        {
            // Retry a few times if the storage device isn't ready yet
            const int maxRetries = 10;
            const int retryInterval = 1000;

            for (int i = 0; i < maxRetries; i++)
            {
                Result rc = fileSystemProxy.Get.OpenSdCardFileSystem(ref outFileSystem);

                if (rc.IsSuccess())
                    break;

                if (!ResultFs.StorageDeviceNotReady.Includes(rc))
                    return rc;

                if (i == maxRetries - 1)
                    return rc;

                fs.Hos.Os.SleepThread(TimeSpan.FromMilliSeconds(retryInterval));
            }

            return Result.Success;
        }
    }

    private static Result RegisterFileSystem(FileSystemClient fs, U8Span mountName,
        ref SharedRef<IFileSystemSf> fileSystem)
    {
        using var fileSystemAdapter =
            new UniqueRef<IFileSystem>(new FileSystemServiceObjectAdapter(ref fileSystem));

        if (!fileSystemAdapter.HasValue)
            return ResultFs.AllocationMemoryFailedInSdCardA.Log();

        return fs.Register(mountName, ref fileSystemAdapter.Ref());
    }

    public static Result MountSdCard(this FileSystemClient fs, U8Span mountName)
    {
        Result rc;
        Span<byte> logBuffer = stackalloc byte[0x30];

        // Check if the mount name is valid
        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            rc = fs.Impl.CheckMountName(mountName);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogName).Append(mountName);
            logBuffer = sb.Buffer;

            fs.Impl.OutputAccessLogUnlessResultSuccess(rc, start, end, null, new U8Span(logBuffer));
        }
        else
        {
            rc = fs.Impl.CheckMountName(mountName);
        }

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc;

        // Open the SD card file system
        using var fileSystem = new SharedRef<IFileSystemSf>();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            rc = OpenSdCardFileSystem(fs, ref fileSystem.Ref());
            Tick end = fs.Hos.Os.GetSystemTick();

            fs.Impl.OutputAccessLogUnlessResultSuccess(rc, start, end, null, new U8Span(logBuffer));
        }
        else
        {
            rc = OpenSdCardFileSystem(fs, ref fileSystem.Ref());
        }

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc;

        // Mount the file system
        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            rc = RegisterFileSystem(fs, mountName, ref fileSystem.Ref());
            Tick end = fs.Hos.Os.GetSystemTick();

            fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(logBuffer));
        }
        else
        {
            rc = RegisterFileSystem(fs, mountName, ref fileSystem.Ref());
        }

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc;

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;
    }

    public static bool IsSdCardInserted(this FileSystemClient fs)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.LogResultErrorMessage(rc);
        Abort.DoAbortUnless(rc.IsSuccess());

        rc = CheckIfInserted(fs, ref deviceOperator.Ref(), out bool isInserted);
        fs.Impl.LogResultErrorMessage(rc);
        Abort.DoAbortUnless(rc.IsSuccess());

        return isInserted;

        static Result CheckIfInserted(FileSystemClient fs, ref SharedRef<IDeviceOperator> deviceOperator,
            out bool isInserted)
        {
            UnsafeHelpers.SkipParamInit(out isInserted);

            // Retry a few times if the storage device isn't ready yet
            const int maxRetries = 10;
            const int retryInterval = 1000;

            for (int i = 0; i < maxRetries; i++)
            {
                Result rc = deviceOperator.Get.IsSdCardInserted(out isInserted);

                if (rc.IsSuccess())
                    break;

                if (!ResultFs.StorageDeviceNotReady.Includes(rc))
                    return rc;

                if (i == maxRetries - 1)
                    return rc;

                fs.Hos.Os.SleepThread(TimeSpan.FromMilliSeconds(retryInterval));
            }

            return Result.Success;
        }
    }

    public static Result SetSdCardEncryptionSeed(this FileSystemClient fs, in EncryptionSeed seed)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result rc = fileSystemProxy.Get.SetSdCardEncryptionSeed(in seed);
        fs.Impl.AbortIfNeeded(rc);
        return rc;
    }

    public static void SetSdCardAccessibility(this FileSystemClient fs, bool isAccessible)
    {
        Result rc = fs.Impl.SetSdCardAccessibility(isAccessible);
        fs.Impl.LogResultErrorMessage(rc);
        Abort.DoAbortUnless(rc.IsSuccess());
    }

    public static bool IsSdCardAccessible(this FileSystemClient fs)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result rc = fileSystemProxy.Get.IsSdCardAccessible(out bool isAccessible);
        fs.Impl.LogResultErrorMessage(rc);
        Abort.DoAbortUnless(rc.IsSuccess());

        return isAccessible;
    }

    public static Result SetSdCardAccessibility(this FileSystemClientImpl fs, bool isAccessible)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();

        Result rc = fileSystemProxy.Get.SetSdCardAccessibility(isAccessible);
        fs.AbortIfNeeded(rc);
        return rc;
    }
}
