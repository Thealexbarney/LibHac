using System;
using LibHac.Os;

namespace LibHac.GcSrv;

/// <summary>
/// Handles granting access to the game card, and keeps track of the current game card handle.
/// </summary>
/// <remarks>Based on nnSdk 15.3.0 (FS 15.0.0)</remarks>
internal interface IGameCardManager : IDisposable
{
    Result AcquireReadLock(ref SharedLock<ReaderWriterLock> outLock, GameCardHandle handle);
    Result AcquireSecureLock(ref SharedLock<ReaderWriterLock> outLock, ref GameCardHandle inOutHandle, ReadOnlySpan<byte> cardDeviceId, ReadOnlySpan<byte> cardImageHash);
    Result AcquireWriteLock(ref UniqueLock<ReaderWriterLock> outLock);
    Result HandleGameCardAccessResult(Result result);
    Result GetHandle(out GameCardHandle outHandle);
    bool IsSecureMode();
}