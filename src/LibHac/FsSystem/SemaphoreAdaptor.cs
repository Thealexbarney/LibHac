using System;
using System.Threading;

namespace LibHac.FsSystem
{
    public class SemaphoreAdaptor : IDisposable
    {
        private SemaphoreSlim _semaphore;

        public SemaphoreAdaptor(int initialCount, int maxCount)
        {
            _semaphore = new SemaphoreSlim(initialCount, maxCount);
        }

        public bool TryLock()
        {
            return _semaphore.Wait(System.TimeSpan.Zero);
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
}
