using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Os;
using LibHac.Sf;
using static LibHac.Fs.Impl.AccessLogStrings;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions used for mounting and interacting with the SD card.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
[SkipLocalsInit]
public static class SdCard
{
    private static Result OpenSdCardFileSystem(FileSystemClient fs, ref SharedRef<IFileSystemSf> outFileSystem)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        return OpenFileSystem(fs, in fileSystemProxy, ref outFileSystem);

        static Result OpenFileSystem(FileSystemClient fs, in SharedRef<IFileSystemProxy> fileSystemProxy,
            ref SharedRef<IFileSystemSf> outFileSystem)
        {
            // Retry a few times if the storage device isn't ready yet
            const int maxRetries = 10;
            const int retryInterval = 1000;

            for (int i = 0; i < maxRetries; i++)
            {
                Result res = fileSystemProxy.Get.OpenSdCardFileSystem(ref outFileSystem);

                if (res.IsSuccess())
                    break;

                if (!ResultFs.StorageDeviceNotReady.Includes(res))
                    return res;

                if (i == maxRetries - 1)
                    return res;

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
        Result res;
        Span<byte> logBuffer = stackalloc byte[0x30];

        // Check if the mount name is valid
        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = fs.Impl.CheckMountName(mountName);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogName).Append(mountName);
            logBuffer = sb.Buffer;

            fs.Impl.OutputAccessLogUnlessResultSuccess(res, start, end, null, new U8Span(logBuffer));
        }
        else
        {
            res = fs.Impl.CheckMountName(mountName);
        }

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        // Open the SD card file system
        using var fileSystem = new SharedRef<IFileSystemSf>();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = OpenSdCardFileSystem(fs, ref fileSystem.Ref());
            Tick end = fs.Hos.Os.GetSystemTick();

            fs.Impl.OutputAccessLogUnlessResultSuccess(res, start, end, null, new U8Span(logBuffer));
        }
        else
        {
            res = OpenSdCardFileSystem(fs, ref fileSystem.Ref());
        }

        if (res.IsFailure()) return res.Miss();

        // Mount the file system
        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = RegisterFileSystem(fs, mountName, ref fileSystem.Ref());
            Tick end = fs.Hos.Os.GetSystemTick();

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(logBuffer));
        }
        else
        {
            res = RegisterFileSystem(fs, mountName, ref fileSystem.Ref());
        }

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;
    }

    public static Result MountSdCardForDebug(this FileSystemClient fs, U8Span mountName)
    {
        Result res;
        Span<byte> logBuffer = stackalloc byte[0x30];

        // Check if the mount name is valid
        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = fs.Impl.CheckMountName(mountName);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogName).Append(mountName);
            logBuffer = sb.Buffer;

            fs.Impl.OutputAccessLogUnlessResultSuccess(res, start, end, null, new U8Span(logBuffer));
        }
        else
        {
            res = fs.Impl.CheckMountName(mountName);
        }

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        // Open the SD card file system
        using var fileSystem = new SharedRef<IFileSystemSf>();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = OpenSdCardFileSystem(fs, ref fileSystem.Ref());
            Tick end = fs.Hos.Os.GetSystemTick();

            fs.Impl.OutputAccessLogUnlessResultSuccess(res, start, end, null, new U8Span(logBuffer));
        }
        else
        {
            res = OpenSdCardFileSystem(fs, ref fileSystem.Ref());
        }

        if (res.IsFailure()) return res.Miss();

        // Mount the file system
        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = RegisterFileSystem(fs, mountName, ref fileSystem.Ref());
            Tick end = fs.Hos.Os.GetSystemTick();

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(logBuffer));
        }
        else
        {
            res = RegisterFileSystem(fs, mountName, ref fileSystem.Ref());
        }

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;
    }

    public static bool IsSdCardInserted(this FileSystemClient fs)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.LogResultErrorMessage(res);
        Abort.DoAbortUnless(res.IsSuccess());

        res = CheckIfInserted(fs, ref deviceOperator.Ref(), out bool isInserted);
        fs.Impl.LogResultErrorMessage(res);
        Abort.DoAbortUnless(res.IsSuccess());

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
                Result res = deviceOperator.Get.IsSdCardInserted(out isInserted);

                if (res.IsSuccess())
                    break;

                if (!ResultFs.StorageDeviceNotReady.Includes(res))
                    return res;

                if (i == maxRetries - 1)
                    return res;

                fs.Hos.Os.SleepThread(TimeSpan.FromMilliSeconds(retryInterval));
            }

            return Result.Success;
        }
    }

    public static Result GetSdCardSpeedMode(this FileSystemClient fs, out SdCardSpeedMode outMode)
    {
        UnsafeHelpers.SkipParamInit(out outMode);

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = GetSpeedMode(fs, in deviceOperator, out long speedMode);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        outMode = (SdCardSpeedMode)speedMode;
        return Result.Success;

        static Result GetSpeedMode(FileSystemClient fs, in SharedRef<IDeviceOperator> deviceOperator,
            out long outSpeedMode)
        {
            outSpeedMode = 0;

            // Retry a few times if the storage device isn't ready yet
            const int maxRetries = 10;
            const int retryInterval = 1000;

            for (int i = 0; i < maxRetries; i++)
            {
                Result res = deviceOperator.Get.GetSdCardSpeedMode(out outSpeedMode);

                if (res.IsSuccess())
                    break;

                if (!ResultFs.StorageDeviceNotReady.Includes(res))
                    return res;

                if (i == maxRetries - 1)
                    return res;

                fs.Hos.Os.SleepThread(TimeSpan.FromMilliSeconds(retryInterval));
            }

            return Result.Success;
        }
    }

    public static Result GetSdCardCid(this FileSystemClient fs, Span<byte> outCidBuffer)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = GetCid(fs, in deviceOperator, outCidBuffer);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;

        static Result GetCid(FileSystemClient fs, in SharedRef<IDeviceOperator> deviceOperator, Span<byte> outCidBuffer)
        {
            // Retry a few times if the storage device isn't ready yet
            const int maxRetries = 10;
            const int retryInterval = 1000;

            for (int i = 0; i < maxRetries; i++)
            {
                Result res = deviceOperator.Get.GetSdCardCid(new OutBuffer(outCidBuffer), outCidBuffer.Length);

                if (res.IsSuccess())
                    break;

                if (!ResultFs.StorageDeviceNotReady.Includes(res))
                    return res;

                if (i == maxRetries - 1)
                    return res;

                fs.Hos.Os.SleepThread(TimeSpan.FromMilliSeconds(retryInterval));
            }

            return Result.Success;
        }
    }

    public static Result GetSdCardUserAreaSize(this FileSystemClient fs, out long outSize)
    {
        UnsafeHelpers.SkipParamInit(out outSize);

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = GetUserAreaSize(fs, in deviceOperator, out outSize);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;

        static Result GetUserAreaSize(FileSystemClient fs, in SharedRef<IDeviceOperator> deviceOperator,
            out long outSize)
        {
            outSize = 0;

            // Retry a few times if the storage device isn't ready yet
            const int maxRetries = 10;
            const int retryInterval = 1000;

            for (int i = 0; i < maxRetries; i++)
            {
                Result res = deviceOperator.Get.GetSdCardUserAreaSize(out outSize);

                if (res.IsSuccess())
                    break;

                if (!ResultFs.StorageDeviceNotReady.Includes(res))
                    return res;

                if (i == maxRetries - 1)
                    return res;

                fs.Hos.Os.SleepThread(TimeSpan.FromMilliSeconds(retryInterval));
            }

            return Result.Success;
        }
    }

    public static Result GetSdCardProtectedAreaSize(this FileSystemClient fs, out long outSize)
    {
        UnsafeHelpers.SkipParamInit(out outSize);

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = GetProtectedAreaSize(fs, in deviceOperator, out outSize);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;

        static Result GetProtectedAreaSize(FileSystemClient fs, in SharedRef<IDeviceOperator> deviceOperator,
            out long outSize)
        {
            outSize = 0;

            // Retry a few times if the storage device isn't ready yet
            const int maxRetries = 10;
            const int retryInterval = 1000;

            for (int i = 0; i < maxRetries; i++)
            {
                Result res = deviceOperator.Get.GetSdCardProtectedAreaSize(out outSize);

                if (res.IsSuccess())
                    break;

                if (!ResultFs.StorageDeviceNotReady.Includes(res))
                    return res;

                if (i == maxRetries - 1)
                    return res;

                fs.Hos.Os.SleepThread(TimeSpan.FromMilliSeconds(retryInterval));
            }

            return Result.Success;
        }
    }

    public static Result GetAndClearMmcErrorInfo(this FileSystemClient fs, out StorageErrorInfo outErrorInfo,
        out long outLogSize, Span<byte> logBuffer)
    {
        UnsafeHelpers.SkipParamInit(out outErrorInfo, out outLogSize);

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = GetErrorInfo(fs, in deviceOperator, out outErrorInfo, out long logSize, logBuffer);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        outLogSize = logSize;
        return Result.Success;

        static Result GetErrorInfo(FileSystemClient fs, in SharedRef<IDeviceOperator> deviceOperator,
            out StorageErrorInfo outErrorInfo, out long outLogSize, Span<byte> logBuffer)
        {
            UnsafeHelpers.SkipParamInit(out outErrorInfo, out outLogSize);

            // Retry a few times if the storage device isn't ready yet
            const int maxRetries = 10;
            const int retryInterval = 1000;

            for (int i = 0; i < maxRetries; i++)
            {
                Result res = deviceOperator.Get.GetAndClearSdCardErrorInfo(out outErrorInfo, out outLogSize,
                    new OutBuffer(logBuffer), logBuffer.Length);

                if (res.IsSuccess())
                    break;

                if (!ResultFs.StorageDeviceNotReady.Includes(res))
                    return res;

                if (i == maxRetries - 1)
                    return res;

                fs.Hos.Os.SleepThread(TimeSpan.FromMilliSeconds(retryInterval));
            }

            return Result.Success;
        }
    }

    public static Result FormatSdCard(this FileSystemClient fs)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result res = Format(fs, in fileSystemProxy);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;

        static Result Format(FileSystemClient fs, in SharedRef<IFileSystemProxy> fileSystemProxy)
        {
            // Retry a few times if the storage device isn't ready yet
            const int maxRetries = 10;
            const int retryInterval = 1000;

            for (int i = 0; i < maxRetries; i++)
            {
                Result res = fileSystemProxy.Get.FormatSdCardFileSystem();

                if (res.IsSuccess())
                    break;

                if (!ResultFs.StorageDeviceNotReady.Includes(res))
                    return res;

                if (i == maxRetries - 1)
                    return res;

                fs.Hos.Os.SleepThread(TimeSpan.FromMilliSeconds(retryInterval));
            }

            return Result.Success;
        }
    }

    public static Result FormatSdCardDryRun(this FileSystemClient fs)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result res = fileSystemProxy.Get.FormatSdCardDryRun();
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static bool IsExFatSupported(this FileSystemClient fs)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result res = fileSystemProxy.Get.IsExFatSupported(out bool isSupported);
        fs.Impl.LogResultErrorMessage(res);
        Abort.DoAbortUnless(res.IsSuccess());

        return isSupported;
    }

    public static Result SetSdCardEncryptionSeed(this FileSystemClient fs, in EncryptionSeed seed)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result res = fileSystemProxy.Get.SetSdCardEncryptionSeed(in seed);
        fs.Impl.AbortIfNeeded(res);
        return res;
    }

    public static void SetSdCardAccessibility(this FileSystemClient fs, bool isAccessible)
    {
        Result res = fs.Impl.SetSdCardAccessibility(isAccessible);
        fs.Impl.LogResultErrorMessage(res);
        Abort.DoAbortUnless(res.IsSuccess());
    }

    public static bool IsSdCardAccessible(this FileSystemClient fs)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result res = fileSystemProxy.Get.IsSdCardAccessible(out bool isAccessible);
        fs.Impl.LogResultErrorMessage(res);
        Abort.DoAbortUnless(res.IsSuccess());

        return isAccessible;
    }

    private static Result SetSdCardSimulationEvent(FileSystemClient fs,
        SimulatingDeviceTargetOperation simulatedOperationType,
        SimulatingDeviceAccessFailureEventType simulatedFailureType, Result failureResult, bool autoClearEvent)
    {
        throw new NotImplementedException();
    }

    public static Result SimulateSdCardDetectionEvent(this FileSystemClient fs, SimulatingDeviceDetectionMode mode,
        bool signalEvent)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result res = fileSystemProxy.Get.SimulateDeviceDetectionEvent(SdmmcPort.SdCard, mode, signalEvent);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result SetSdCardSimulationEvent(this FileSystemClient fs,
        SimulatingDeviceTargetOperation simulatedOperationType,
        SimulatingDeviceAccessFailureEventType simulatedFailureType)
    {
        Result res = SetSdCardSimulationEvent(fs, simulatedOperationType, simulatedFailureType, Result.Success,
            autoClearEvent: false);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result SetSdCardSimulationEvent(this FileSystemClient fs,
        SimulatingDeviceTargetOperation simulatedOperationType,
        SimulatingDeviceAccessFailureEventType simulatedFailureType, bool autoClearEvent)
    {
        Result res = SetSdCardSimulationEvent(fs, simulatedOperationType, simulatedFailureType, Result.Success,
            autoClearEvent);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result SetSdCardSimulationEvent(this FileSystemClient fs,
        SimulatingDeviceTargetOperation simulatedOperationType, Result failureResult, bool autoClearEvent)
    {
        Result res = SetSdCardSimulationEvent(fs, simulatedOperationType,
            SimulatingDeviceAccessFailureEventType.AccessFailure, failureResult, autoClearEvent);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result ClearSdCardSimulationEvent(this FileSystemClient fs)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.ClearDeviceSimulationEvent((uint)SdmmcPort.SdCard);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result SetSdCardAccessibility(this FileSystemClientImpl fs, bool isAccessible)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();

        Result res = fileSystemProxy.Get.SetSdCardAccessibility(isAccessible);
        fs.AbortIfNeeded(res);
        return res;
    }
}