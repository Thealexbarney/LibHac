using System;

namespace LibHac.Os;

public struct EventType
{
}

public class Event : IDisposable
{
    private EventType _event;

    private readonly OsState _os;

    public Event(OsState os, EventClearMode clearMode)
    {
        _os = os;
        _os.InitializeEvent(ref _event, signaled: false, clearMode);
    }

    public void Dispose() => _os.FinalizeEvent(ref _event);
    public void Wait() => _os.WaitEvent(ref _event);
    public bool TryWait() => _os.TryWaitEvent(ref _event);
    public bool TimedWait(TimeSpan timeout) => _os.TimedWaitEvent(ref _event, timeout);
    public void Signal() => _os.SignalEvent(ref _event);
    public void Clear() => _os.ClearEvent(ref _event);
    public ref EventType GetBase() => ref _event;
}

public static class EventApi
{
    public static void InitializeEvent(this OsState os, ref EventType eventType, bool signaled,
        EventClearMode clearMode)
    {
        throw new NotImplementedException();
    }

    public static void FinalizeEvent(this OsState os, ref EventType eventType)
    {
        throw new NotImplementedException();
    }

    public static void SignalEvent(this OsState os, ref EventType eventType)
    {
        throw new NotImplementedException();
    }

    public static void WaitEvent(this OsState os, ref EventType eventType)
    {
        throw new NotImplementedException();
    }

    public static bool TryWaitEvent(this OsState os, ref EventType eventType)
    {
        throw new NotImplementedException();
    }

    public static bool TimedWaitEvent(this OsState os, ref EventType eventType, TimeSpan timeout)
    {
        throw new NotImplementedException();
    }

    public static void ClearEvent(this OsState os, ref EventType eventType)
    {
        throw new NotImplementedException();
    }

    public static void InitializeMultiWaitHolder(this OsState os, ref MultiWaitHolderType multiWaitHolder,
        ref EventType eventType)
    {
        throw new NotImplementedException();
    }
}