using System;
using LibHac.Common;
using LibHac.FsSystem;

namespace LibHac.FsSrv.Impl;

public interface IEntryOpenCountSemaphoreManager : IDisposable
{
    Result TryAcquireEntryOpenCountSemaphore(ref UniqueRef<IUniqueLock> outSemaphore);
}
