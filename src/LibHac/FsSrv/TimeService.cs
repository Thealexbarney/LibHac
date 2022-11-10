using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Impl;
using LibHac.Os;

namespace LibHac.FsSrv;

/// <summary>
/// Handles time-related calls for <see cref="FileSystemProxyImpl"/>.
/// </summary>
/// <remarks><para>This struct handles checking a process' permissions before forwarding
/// a request to the <see cref="TimeServiceImpl"/> object.</para>
/// <para>Based on nnSdk 13.4.0 (FS 13.1.0)</para></remarks>
public readonly struct TimeService
{
    private readonly TimeServiceImpl _serviceImpl;
    private readonly ulong _processId;

    public TimeService(TimeServiceImpl serviceImpl, ulong processId)
    {
        _serviceImpl = serviceImpl;
        _processId = processId;
    }

    public Result SetCurrentPosixTimeWithTimeDifference(long currentTime, int timeDifference)
    {
        using var programRegistry = new ProgramRegistryImpl(_serviceImpl.FsServer);
        Result res = programRegistry.GetProgramInfo(out ProgramInfo programInfo, _processId);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.SetCurrentPosixTime))
            return ResultFs.PermissionDenied.Log();

        _serviceImpl.SetCurrentPosixTimeWithTimeDifference(currentTime, timeDifference);
        return Result.Success;
    }
}

/// <summary>
/// Manages the current time used by the FS service.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public class TimeServiceImpl
{
    private long _basePosixTime;
    private int _timeDifferenceSeconds;
    private SdkMutexType _mutex;

    // LibHac addition
    internal FileSystemServer FsServer { get; }

    public TimeServiceImpl(FileSystemServer fsServer)
    {
        _basePosixTime = 0;
        _timeDifferenceSeconds = 0;
        _mutex = new SdkMutexType();

        FsServer = fsServer;
    }

    private long GetSystemSeconds()
    {
        OsState os = FsServer.Hos.Os;

        Tick tick = os.GetSystemTick();
        TimeSpan timeSpan = os.ConvertToTimeSpan(tick);
        return timeSpan.GetSeconds();
    }

    public Result GetCurrentPosixTime(out long currentTime)
    {
        return GetCurrentPosixTimeWithTimeDifference(out currentTime, out int _);
    }

    public Result GetCurrentPosixTimeWithTimeDifference(out long currentTime, out int timeDifference)
    {
        UnsafeHelpers.SkipParamInit(out currentTime, out timeDifference);

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        if (_basePosixTime == 0)
            return ResultFs.NotInitialized.Log();

        if (!Unsafe.IsNullRef(ref currentTime))
        {
            currentTime = _basePosixTime + GetSystemSeconds();
        }

        if (!Unsafe.IsNullRef(ref timeDifference))
        {
            timeDifference = _timeDifferenceSeconds;
        }

        return Result.Success;
    }

    public void SetCurrentPosixTimeWithTimeDifference(long currentTime, int timeDifference)
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        _basePosixTime = currentTime - GetSystemSeconds();
        _timeDifferenceSeconds = timeDifference;
    }
}