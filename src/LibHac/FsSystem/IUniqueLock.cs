using System;
using LibHac.Common;
using LibHac.Os;

namespace LibHac.FsSystem
{
    public interface IUniqueLock : IDisposable { }

    public class UniqueLockWithPin<T> : IUniqueLock where T : class, IDisposable
    {
        private UniqueLock<SemaphoreAdapter> _semaphore;
        private ReferenceCountedDisposable<T> _pinnedObject;

        public UniqueLockWithPin(ref UniqueLock<SemaphoreAdapter> semaphore, ref ReferenceCountedDisposable<T> pinnedObject)
        {
            Shared.Move(out _semaphore, ref semaphore);
            Shared.Move(out _pinnedObject, ref pinnedObject);
        }

        public void Dispose()
        {
            if (_pinnedObject != null)
            {
                _semaphore.Dispose();
                _pinnedObject.Dispose();

                _pinnedObject = null;
            }
        }
    }
}
