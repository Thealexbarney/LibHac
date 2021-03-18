using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Os;
using static LibHac.Fs.Impl.AccessLogStrings;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim
{
    [SkipLocalsInit]
    public static class SdCard
    {
        private static Result OpenSdCardFileSystem(FileSystemClient fs,
            out ReferenceCountedDisposable<IFileSystemSf> fileSystem)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            return OpenFileSystem(fs, fsProxy, out fileSystem);

            static Result OpenFileSystem(FileSystemClient fs, ReferenceCountedDisposable<IFileSystemProxy> fsProxy,
                out ReferenceCountedDisposable<IFileSystemSf> fileSystem)
            {
                UnsafeHelpers.SkipParamInit(out fileSystem);

                // Retry a few times if the storage device isn't ready yet
                const int maxRetries = 10;
                const int retryInterval = 1000;

                for (int i = 0; i < maxRetries; i++)
                {
                    Result rc = fsProxy.Target.OpenSdCardFileSystem(out fileSystem);

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
            ReferenceCountedDisposable<IFileSystemSf> fileSystem)
        {
            var fileSystemAdapter = new FileSystemServiceObjectAdapter(fileSystem);
            return fs.Register(mountName, fileSystemAdapter);
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
            ReferenceCountedDisposable<IFileSystemSf> fileSystem = null;
            try
            {
                if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
                {
                    Tick start = fs.Hos.Os.GetSystemTick();
                    rc = OpenSdCardFileSystem(fs, out fileSystem);
                    Tick end = fs.Hos.Os.GetSystemTick();

                    fs.Impl.OutputAccessLogUnlessResultSuccess(rc, start, end, null, new U8Span(logBuffer));
                }
                else
                {
                    rc = OpenSdCardFileSystem(fs, out fileSystem);
                }
                fs.Impl.AbortIfNeeded(rc);
                if (rc.IsFailure()) return rc;

                // Mount the file system
                if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
                {
                    Tick start = fs.Hos.Os.GetSystemTick();
                    rc = RegisterFileSystem(fs, mountName, fileSystem);
                    Tick end = fs.Hos.Os.GetSystemTick();

                    fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(logBuffer));
                }
                else
                {
                    rc = RegisterFileSystem(fs, mountName, fileSystem);
                }
                fs.Impl.AbortIfNeeded(rc);
                if (rc.IsFailure()) return rc;

                if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
                    fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

                return Result.Success;
            }
            finally
            {
                fileSystem?.Dispose();
            }
        }

        public static bool IsSdCardInserted(this FileSystemClient fs)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            ReferenceCountedDisposable<IDeviceOperator> deviceOperator = null;
            try
            {
                Result rc = fsProxy.Target.OpenDeviceOperator(out deviceOperator);
                fs.Impl.LogErrorMessage(rc);
                Abort.DoAbortUnless(rc.IsSuccess());

                rc = CheckIfInserted(fs, deviceOperator, out bool isInserted);
                fs.Impl.LogErrorMessage(rc);
                Abort.DoAbortUnless(rc.IsSuccess());

                return isInserted;
            }
            finally
            {
                deviceOperator?.Dispose();
            }

            static Result CheckIfInserted(FileSystemClient fs,
                ReferenceCountedDisposable<IDeviceOperator> deviceOperator, out bool isInserted)
            {
                UnsafeHelpers.SkipParamInit(out isInserted);

                // Retry a few times if the storage device isn't ready yet
                const int maxRetries = 10;
                const int retryInterval = 1000;

                for (int i = 0; i < maxRetries; i++)
                {
                    Result rc = deviceOperator.Target.IsSdCardInserted(out isInserted);

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
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.Target.SetSdCardEncryptionSeed(in seed);
            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static void SetSdCardAccessibility(this FileSystemClient fs, bool isAccessible)
        {
            Result rc = fs.Impl.SetSdCardAccessibility(isAccessible);
            fs.Impl.LogErrorMessage(rc);
            Abort.DoAbortUnless(rc.IsSuccess());
        }

        public static bool IsSdCardAccessible(this FileSystemClient fs)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.Target.IsSdCardAccessible(out bool isAccessible);
            fs.Impl.LogErrorMessage(rc);
            Abort.DoAbortUnless(rc.IsSuccess());

            return isAccessible;
        }

        public static Result SetSdCardAccessibility(this FileSystemClientImpl fs, bool isAccessible)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.Target.SetSdCardAccessibility(isAccessible);
            fs.AbortIfNeeded(rc);
            return rc;
        }
    }
}
