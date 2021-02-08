using System;
using LibHac.Fs.Impl;

namespace LibHac.GcSrv
{
    internal interface IGameCardDeviceManager
    {
        Result AcquireReadLock(out UniqueLock locker, uint handle);
        Result AcquireReadLockSecureMode(out UniqueLock locker, ref uint handle, ReadOnlySpan<byte> cardDeviceId, ReadOnlySpan<byte> cardImageHash);
        Result AcquireWriteLock(out SharedLock locker);
        Result HandleGameCardAccessResult(Result result);
        Result GetHandle(out uint handle);
        bool IsSecureMode();
    }
}
