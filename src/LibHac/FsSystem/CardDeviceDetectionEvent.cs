using System;
using System.Collections.Generic;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSrv.Sf;
using LibHac.Os;
using LibHac.Sf;

namespace LibHac.FsSystem;

/// <summary>
/// Base class for classes that manage registering events and signaling them when a card device is inserted or removed.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
internal class CardDeviceDetectionEventManager : IDisposable
{
    private LinkedList<CardDeviceDetectionEvent> _events;
    private SdkMutex _mutex;
    protected CallbackArguments CallbackArgs;

    protected class CallbackArguments
    {
        public CardDeviceDetectionEventManager EventManager;
        public SdkMutex Mutex;
        public Sdmmc.Port Port;
    }

    public CardDeviceDetectionEventManager()
    {
        _events = new LinkedList<CardDeviceDetectionEvent>();
        _mutex = new SdkMutex();

        CallbackArgs = new CallbackArguments { EventManager = this, Mutex = _mutex };
    }

    public virtual void Dispose() { }

    public Result CreateDetectionEvent(ref SharedRef<IEventNotifier> outDetectionEvent)
    {
        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        var detectionEventImpl = new CardDeviceDetectionEvent(this);
        using var detectionEvent = new SharedRef<IEventNotifier>(detectionEventImpl);

        if (!detectionEvent.HasValue)
            return ResultFs.AllocationMemoryFailedInDeviceDetectionEventManagerA.Log();

        _events.AddLast(detectionEventImpl);
        outDetectionEvent.SetByMove(ref detectionEvent.Ref );

        return Result.Success;
    }

    public void Unlink(CardDeviceDetectionEvent detectionEvent)
    {
        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        _events.Remove(detectionEvent);
    }

    public void SignalAll()
    {
        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        // ReSharper disable once UnusedVariable
        foreach (CardDeviceDetectionEvent detectionEvent in _events)
        {
            // Todo: Signal event
        }
    }

    protected static void DetectionEventCallback(object args)
    {
        Abort.DoAbortUnless(args is CallbackArguments, "Invalid device detection callback arguments type.");

        var callbackArgs = (CallbackArguments)args;

        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref callbackArgs.Mutex);

        // ReSharper disable once UnusedVariable
        foreach (CardDeviceDetectionEvent detectionEvent in callbackArgs.EventManager._events)
        {
            // Todo: Signal event
        }
    }
}

internal class CardDeviceDetectionEvent : IEventNotifier
{
    private CardDeviceDetectionEventManager _eventManager;
    // Todo: SystemEvent

    public CardDeviceDetectionEvent(CardDeviceDetectionEventManager eventManager)
    {
        _eventManager = eventManager;
    }

    public void Dispose()
    {
        _eventManager?.Unlink(this);
        _eventManager = null;
    }

    public Result GetEventHandle(out NativeHandle handle)
    {
        throw new NotImplementedException();
    }
}