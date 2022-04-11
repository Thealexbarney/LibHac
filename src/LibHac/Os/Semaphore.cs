using System;
using LibHac.Diag;
using LibHac.Os.Impl;

namespace LibHac.Os;

public class Semaphore : IDisposable
{
    private SemaphoreType _semaphore;

    public Semaphore(OsState os, int initialCount, int maxCount) =>
        _semaphore = new SemaphoreType(os, initialCount, maxCount);

    public void Dispose() => _semaphore.Dispose();
    public void Acquire() => _semaphore.Acquire();
    public bool TryAcquire() => _semaphore.TryAcquire();
    public bool TimedAcquire(TimeSpan timeout) => _semaphore.TimedAcquire(timeout);
    public void Release() => _semaphore.Release();
    public void Release(int count) => _semaphore.Release(count);
    public int GetCurrentCount() => _semaphore.GetCurrentCount();
    public ref SemaphoreType GetBase() => ref _semaphore;
}

public struct SemaphoreType : IDisposable
{
    public enum State : byte
    {
        NotInitialized = 0,
        Initialized = 1
    }

    internal MultiWaitObjectList WaitList;
    internal State CurrentState;
    internal int Count;
    internal int MaxCount;

    internal InternalCriticalSection CsSemaphore;
    internal InternalConditionVariable CvNotZero;

    private readonly OsState _os;

    public SemaphoreType(OsState os, int initialCount, int maxCount)
    {
        Assert.SdkRequires(maxCount >= 1);
        Assert.SdkRequires(initialCount >= 0 && initialCount <= maxCount);

        CsSemaphore = new InternalCriticalSection();
        CvNotZero = new InternalConditionVariable();

        WaitList = new MultiWaitObjectList();

        Count = initialCount;
        MaxCount = maxCount;
        CurrentState = State.Initialized;
        _os = os;
    }

    public void Dispose()
    {
        Assert.SdkRequires(CurrentState == State.Initialized);
        Assert.SdkRequires(WaitList.IsEmpty());

        CurrentState = State.NotInitialized;

        CsSemaphore.Dispose();
        CvNotZero.Dispose();
    }

    public void Acquire()
    {
        Assert.SdkRequires(CurrentState == State.Initialized);

        using ScopedLock<InternalCriticalSection> lk = ScopedLock.Lock(ref CsSemaphore);

        while (Count == 0)
        {
            CvNotZero.Wait(ref CsSemaphore);
        }

        Count--;
    }

    public bool TryAcquire()
    {
        Assert.SdkRequires(CurrentState == State.Initialized);

        using ScopedLock<InternalCriticalSection> lk = ScopedLock.Lock(ref CsSemaphore);

        if (Count == 0)
        {
            return false;
        }

        Count--;

        return true;
    }

    public bool TimedAcquire(TimeSpan timeout)
    {
        Assert.SdkRequires(CurrentState == State.Initialized);
        Assert.SdkRequires(timeout.GetNanoSeconds() >= 0);

        var timeoutHelper = new TimeoutHelper(_os, timeout);
        using ScopedLock<InternalCriticalSection> lk = ScopedLock.Lock(ref CsSemaphore);

        while (Count == 0)
        {
            if (timeoutHelper.TimedOut())
                return false;

            CvNotZero.TimedWait(ref CsSemaphore, in timeoutHelper);
        }

        Count--;

        return true;
    }

    public void Release()
    {
        Assert.SdkRequires(CurrentState == State.Initialized);

        using ScopedLock<InternalCriticalSection> lk = ScopedLock.Lock(ref CsSemaphore);

        Assert.SdkAssert(Count + 1 <= MaxCount);

        Count++;

        CvNotZero.Signal();
        WaitList.WakeupAllMultiWaitThreadsUnsafe();
    }

    public void Release(int count)
    {
        Assert.SdkRequires(CurrentState == State.Initialized);
        Assert.SdkRequires(Count >= 1);

        using ScopedLock<InternalCriticalSection> lk = ScopedLock.Lock(ref CsSemaphore);

        Assert.SdkAssert(Count + count <= MaxCount);

        Count += count;

        CvNotZero.Broadcast();
        WaitList.WakeupAllMultiWaitThreadsUnsafe();
    }

    public readonly int GetCurrentCount()
    {
        Assert.SdkRequires(CurrentState == State.Initialized);

        return Count;
    }
}

public static class SemaphoreApi
{
    public static void InitializeSemaphore(this OsState os, out SemaphoreType semaphore, int initialCount, int maxCount)
    {
        semaphore = new SemaphoreType(os, initialCount, maxCount);
    }

    public static void FinalizeSemaphore(this OsState os, ref SemaphoreType semaphore)
    {
        semaphore.Dispose();
    }

    public static void AcquireSemaphore(this OsState os, ref SemaphoreType semaphore)
    {
        semaphore.Acquire();
    }

    public static bool TryAcquireSemaphore(this OsState os, ref SemaphoreType semaphore)
    {
        return semaphore.TryAcquire();
    }

    public static bool TimedAcquireSemaphore(this OsState os, ref SemaphoreType semaphore, TimeSpan timeout)
    {
        return semaphore.TimedAcquire(timeout);
    }

    public static void ReleaseSemaphore(this OsState os, ref SemaphoreType semaphore)
    {
        semaphore.Release();
    }

    public static void ReleaseSemaphore(this OsState os, ref SemaphoreType semaphore, int count)
    {
        semaphore.Release(count);
    }

    public static int GetCurrentSemaphoreCount(this OsState os, in SemaphoreType semaphore)
    {
        return semaphore.GetCurrentCount();
    }

    public static void InitializeMultiWaitHolder(this OsState os, MultiWaitHolderType holder, Semaphore semaphore)
    {
        Assert.SdkRequires(semaphore.GetBase().CurrentState == SemaphoreType.State.Initialized);

        holder.Impl = new MultiWaitHolderImpl(new MultiWaitHolderOfSemaphore(semaphore));
        holder.UserData = null;
    }
}