using System;

namespace LibHac.Os.Impl;

internal struct InternalConditionVariable : IDisposable
{
    private InternalConditionVariableImpl _impl;

    public InternalConditionVariable()
    {
        _impl = new InternalConditionVariableImpl();
    }

    public void Dispose()
    {
        _impl.Dispose();
    }

    public void Initialize() => _impl.Initialize();
    public void Signal() => _impl.Signal();
    public void Broadcast() => _impl.Broadcast();
    public void Wait(ref InternalCriticalSection cs) => _impl.Wait(ref cs);

    public void TimedWait(ref InternalCriticalSection cs, in TimeoutHelper timeoutHelper) =>
        _impl.TimedWait(ref cs, in timeoutHelper);
}