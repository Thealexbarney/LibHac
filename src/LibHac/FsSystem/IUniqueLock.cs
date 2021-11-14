using System;
using LibHac.Common;
using LibHac.Os;

namespace LibHac.FsSystem;

public interface IUniqueLock : IDisposable { }

public class UniqueLockWithPin<T> : IUniqueLock where T : class, IDisposable
{
    private UniqueLock<SemaphoreAdapter> _semaphore;
    private SharedRef<T> _pinnedObject;

    public UniqueLockWithPin(ref UniqueLock<SemaphoreAdapter> semaphore, ref SharedRef<T> pinnedObject)
    {
        _semaphore = new UniqueLock<SemaphoreAdapter>(ref semaphore);
        _pinnedObject = SharedRef<T>.CreateMove(ref pinnedObject);
    }

    public void Dispose()
    {
        using (var emptyLock = new UniqueLock<SemaphoreAdapter>())
        {
            _semaphore.Set(ref emptyLock.Ref());
        }

        _pinnedObject.Destroy();
        _semaphore.Dispose();
    }
}
