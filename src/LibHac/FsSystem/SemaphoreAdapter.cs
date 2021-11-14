using System;
using System.Threading;
using LibHac.Os;

namespace LibHac.FsSystem;

public class SemaphoreAdapter : IDisposable, ILockable
{
    private SemaphoreSlim _semaphore;

    public SemaphoreAdapter(int initialCount, int maxCount)
    {
        _semaphore = new SemaphoreSlim(initialCount, maxCount);
    }

    public bool TryLock()
    {
        return _semaphore.Wait(System.TimeSpan.Zero);
    }

    public void Lock()
    {
        _semaphore.Wait();
    }

    public void Unlock()
    {
        _semaphore.Release();
    }

    public void Dispose()
    {
        _semaphore?.Dispose();
    }
}
