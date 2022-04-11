namespace LibHac.Os.Impl;

public class MultiWaitHolderOfSemaphore : MultiWaitHolderOfUserWaitObject
{
    private Semaphore _semaphore;

    public MultiWaitHolderOfSemaphore(Semaphore semaphore)
    {
        _semaphore = semaphore;
    }

    public override TriBool IsSignaled()
    {
        using ScopedLock<InternalCriticalSection> lk = ScopedLock.Lock(ref _semaphore.GetBase().CsSemaphore);

        return IsSignaledUnsafe();
    }

    public override TriBool AddToObjectList()
    {
        using ScopedLock<InternalCriticalSection> lk = ScopedLock.Lock(ref _semaphore.GetBase().CsSemaphore);

        _semaphore.GetBase().WaitList.PushBackToList(this);
        return IsSignaledUnsafe();
    }

    public override void RemoveFromObjectList()
    {
        using ScopedLock<InternalCriticalSection> lk = ScopedLock.Lock(ref _semaphore.GetBase().CsSemaphore);

        _semaphore.GetBase().WaitList.EraseFromList(this);
    }

    private TriBool IsSignaledUnsafe()
    {
        return _semaphore.GetBase().Count > 0 ? TriBool.True : TriBool.False;
    }
}