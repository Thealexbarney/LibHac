using System;
using System.Threading;
using LibHac.Diag;
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

    public bool TryLock(out int outAcquiredCount, int count)
    {
        Assert.SdkRequiresLess(0, count);

        for (int i = 0; i < count; i++)
        {
            if (!_semaphore.Wait(System.TimeSpan.Zero))
            {
                outAcquiredCount = i;
                return false;
            }
        }

        outAcquiredCount = count;
        return true;
    }

    public void Lock()
    {
        _semaphore.Wait();
    }

    public void Unlock()
    {
        _semaphore.Release();
    }

    public void Unlock(int count)
    {
        if (count > 0)
        {
            _semaphore.Release(count);
        }
    }

    public void Dispose()
    {
        _semaphore?.Dispose();
    }
}