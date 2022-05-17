using System;
using System.Collections.Generic;
using LibHac.Common;
using LibHac.FsSrv.Sf;
using LibHac.Os;
using LibHac.Sf;

namespace LibHac.FsSystem;

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

    public virtual void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result CreateDetectionEvent(ref SharedRef<IEventNotifier> outDetectionEvent)
    {
        throw new NotImplementedException();
    }

    public void Unlink(CardDeviceDetectionEvent detectionEvent)
    {
        throw new NotImplementedException();
    }

    public void SignalAll()
    {
        throw new NotImplementedException();
    }

    protected static void DetectionEventCallback(object args)
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }

    public Result GetEventHandle(out NativeHandle handle)
    {
        throw new NotImplementedException();
    }
}