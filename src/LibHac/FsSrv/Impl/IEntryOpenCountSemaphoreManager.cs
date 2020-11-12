using System;
using LibHac.FsSystem;

namespace LibHac.FsSrv.Impl
{
    public interface IEntryOpenCountSemaphoreManager : IDisposable
    {
        Result TryAcquireEntryOpenCountSemaphore(out IUniqueLock semaphore);
    }
}