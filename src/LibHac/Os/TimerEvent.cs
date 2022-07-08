using System;

namespace LibHac.Os;

public struct TimerEventType
{
}

public class TimerEvent : IDisposable
{
    private TimerEventType _event;

    private readonly OsState _os;

    public TimerEvent(OsState os, EventClearMode clearMode)
    {
        _os = os;
        _os.InitializeTimerEvent(ref _event, clearMode);
    }

    public void Dispose() => _os.FinalizeTimerEvent(ref _event);
    public void StartOneShot(TimeSpan firstTime) => _os.StartOneShotTimerEvent(ref _event, firstTime);
    public void StartPeriodic(TimeSpan firstTime, TimeSpan interval) => _os.StartPeriodicTimerEvent(ref _event, firstTime, interval);
    public void Stop() => _os.StopTimerEvent(ref _event);
    public void Wait() => _os.WaitTimerEvent(ref _event);
    public bool TryWait() => _os.TryWaitTimerEvent(ref _event);
    public void Signal() => _os.SignalTimerEvent(ref _event);
    public void Clear() => _os.ClearTimerEvent(ref _event);
    public ref TimerEventType GetBase() => ref _event;
}

public static class TimerEventApi
{
    public static void InitializeTimerEvent(this OsState os, ref TimerEventType eventType, EventClearMode clearMode)
    {
        throw new NotImplementedException();
    }

    public static void FinalizeTimerEvent(this OsState os, ref TimerEventType eventType)
    {
        throw new NotImplementedException();
    }

    public static void StartOneShotTimerEvent(this OsState os, ref TimerEventType eventType, TimeSpan firstTime)
    {
        throw new NotImplementedException();
    }

    public static void StartPeriodicTimerEvent(this OsState os, ref TimerEventType eventType, TimeSpan firstTime,
        TimeSpan interval)
    {
        throw new NotImplementedException();
    }

    public static void StopTimerEvent(this OsState os, ref TimerEventType eventType)
    {
        throw new NotImplementedException();
    }

    public static void SignalTimerEvent(this OsState os, ref TimerEventType eventType)
    {
        throw new NotImplementedException();
    }

    public static void ClearTimerEvent(this OsState os, ref TimerEventType eventType)
    {
        throw new NotImplementedException();
    }

    public static void WaitTimerEvent(this OsState os, ref TimerEventType eventType)
    {
        throw new NotImplementedException();
    }

    public static bool TryWaitTimerEvent(this OsState os, ref TimerEventType eventType)
    {
        throw new NotImplementedException();
    }

    public static void InitializeMultiWaitHolder(this OsState os, ref MultiWaitHolderType multiWaitHolder,
        ref TimerEventType eventType)
    {
        throw new NotImplementedException();
    }
}