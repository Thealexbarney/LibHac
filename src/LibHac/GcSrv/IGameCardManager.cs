using System;
using LibHac.Os;

namespace LibHac.GcSrv;

internal interface IGameCardManager : IDisposable
{
    Result AcquireReadLock(ref SharedLock<ReaderWriterLock> outLock, GameCardHandle handle);
    Result AcquireSecureLock(ref SharedLock<ReaderWriterLock> outLock, ref GameCardHandle handle, ReadOnlySpan<byte> cardDeviceId, ReadOnlySpan<byte> cardImageHash);
    Result AcquireWriteLock(ref UniqueLock<ReaderWriterLock> outLock);
    Result HandleGameCardAccessResult(Result result);
    Result GetHandle(out GameCardHandle outHandle);
    bool IsSecureMode();
}