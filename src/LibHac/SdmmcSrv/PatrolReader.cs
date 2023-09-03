using System;
using LibHac.Common;
using LibHac.Crypto;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Os;
using LibHac.Sdmmc;
using static LibHac.SdmmcSrv.Common;
using MmcPartition = LibHac.Sdmmc.MmcPartition;

namespace LibHac.SdmmcSrv;

/// <summary>
/// Performs regular patrol reads on the internal MMC.
/// </summary>
/// <remarks><para>The patrol reader will read sequential half-megabyte blocks of data from the internal MMC,
/// starting at the beginning of the storage, progressing to the end, and then looping back to the beginning.
/// This helps with data integrity by ensuring the entire MMC is regularly read.</para>
/// <para>Every 6 seconds the patrol reader will read a half-megabyte block from the MMC.
/// Every 2 hours it will save the current state of the patrol read to Boot Partition 1 on the MMC.
/// This state contains the next sector index to be read and the number of times the MMC has been patrolled
/// from start to finish.</para>
/// <para>Based on nnSdk 16.2.0 (FS 16.0.0)</para></remarks>
internal class PatrolReader
{
    // Note: This class won't work until events and timer events are properly implemented.

    private const int MacSize = HmacSha256.HashSize;

    /// <summary>
    /// The patrol reader state that's stored in Boot Partition 1.
    /// </summary>
    public struct State
    {
        public uint CurrentSector;
        public uint PatrolCount;
    }

    public enum ThreadState
    {
        Stop,
        Active,
        Sleep
    }

    private readonly int _macSize;
    private readonly MmcPartition _statePartition;
    private readonly int _patrolStateOffset;
    private readonly int _patrolStateSize;
    private readonly int _saveStateIntervalSeconds;
    private readonly int _patrolReadSize;
    private readonly int _waitTimeAfterAllocationSuccess;
    private readonly int _waitTimeAfterAllocationFailure;
    private readonly int _firstRunDelaySeconds;
    private uint _deviceCapacitySectors;
    private State _patrolState;
    private bool _isPatrolStateLoaded;
    private SdkMutex _mutex;
    private bool _areEventsInitialized;
    private ThreadState _threadState;
    // private Thread _patrolReaderThread;
    private Event _stateChangeRequestedEvent;
    private Event _stateChangeCompletedEvent;
    private ulong _allocateSuccessCount;
    private ulong _allocateFailureCount;

    // LibHac addition
    private SdmmcApi _sdmmc;

    private static ReadOnlySpan<byte> PatrolStateKey => "U{W5>1Kq#Gt`f6r86o`9|*||hTy9U2C\0"u8;

    public PatrolReader(SdkMutex mutex, SdmmcApi sdmmc)
    {
        _macSize = MacSize;
        _statePartition = MmcPartition.BootPartition1;
        _patrolStateOffset = 0x184000;
        _patrolStateSize = 0x200;
        _saveStateIntervalSeconds = 7200;
        _patrolReadSize = 0x80000;
        _waitTimeAfterAllocationSuccess = 6;
        _waitTimeAfterAllocationFailure = 1;
        _firstRunDelaySeconds = 18;
        _isPatrolStateLoaded = false;
        _mutex = mutex;
        _areEventsInitialized = false;
        _threadState = ThreadState.Stop;
        _allocateSuccessCount = 0;
        _allocateFailureCount = 0;

        _sdmmc = sdmmc;
    }

    public void Dispose()
    {
        FinalizeObject();
    }

    public static void PatrolReaderThreadEntry(object args)
    {
        if (args is PatrolReader reader)
        {
            reader.PatrolReaderThread();
        }
        else
        {
            Abort.DoAbort($"Expected an argument of type {nameof(PatrolReader)} in {nameof(PatrolReaderThreadEntry)}");
        }
    }

    private void FinalizeObject()
    {
        if (_areEventsInitialized)
        {
            if (_threadState == ThreadState.Sleep)
            {
                Resume();
            }

            if (_threadState == ThreadState.Active)
            {
                Stop();
            }

            _stateChangeRequestedEvent.Dispose();
            _stateChangeCompletedEvent.Dispose();

            _areEventsInitialized = false;
        }
    }

    public Result GetPatrolCount(out uint outCount)
    {
        UnsafeHelpers.SkipParamInit(out outCount);

        if (!_isPatrolStateLoaded)
            return ResultFs.HasNotGottenPatrolCount.Log();

        outCount = _patrolState.PatrolCount;
        return Result.Success;
    }

    public void GetAndClearAllocateCount(out ulong outSuccessCount, out ulong outFailureCount)
    {
        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        outSuccessCount = _allocateSuccessCount;
        outFailureCount = _allocateFailureCount;

        _allocateSuccessCount = 0;
        _allocateFailureCount = 0;
    }

    private bool LoadState()
    {
        using var pooledBuffer = new PooledBuffer(_patrolStateSize, 1);
        if (pooledBuffer.GetSize() < _patrolStateSize)
            return false;

        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        Abort.DoAbortUnlessSuccess(_sdmmc.SelectMmcPartition(Port.Mmc0, _statePartition));

        try
        {
            Result res = _sdmmc.GetDeviceMemoryCapacity(out _deviceCapacitySectors, Port.Mmc0);
            if (res.IsFailure())
            {
                return false;
            }

            Span<byte> patrolStateBuffer = pooledBuffer.GetBuffer().Slice(0, _patrolStateSize);

            res = _sdmmc.Read(patrolStateBuffer, Port.Mmc0, BytesToSectors(_patrolStateOffset),
                BytesToSectors(_patrolStateSize));

            if (res.IsFailure())
            {
                return false;
            }

            Span<byte> mac = stackalloc byte[MacSize];

            // Load an empty state if the verification fails.
            _patrolState = default;

            HmacSha256.GenerateHmacSha256(mac, patrolStateBuffer.Slice(_macSize), PatrolStateKey);

            if (CryptoUtil.IsSameBytes(mac, patrolStateBuffer, _macSize))
            {
                ref State readState = ref SpanHelpers.AsStruct<State>(patrolStateBuffer.Slice(_macSize));

                if (readState.CurrentSector + BytesToSectors(_patrolReadSize) <= _deviceCapacitySectors)
                {
                    _patrolState = readState;
                }
            }

            return true;
        }
        finally
        {
            Abort.DoAbortUnlessSuccess(_sdmmc.SelectMmcPartition(Port.Mmc0, MmcPartition.UserData));
        }
    }

    private void SaveState()
    {
        using var pooledBuffer = new PooledBuffer(_patrolStateSize, 1);

        if (pooledBuffer.GetSize() < _patrolStateSize)
            return;

        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        Span<byte> patrolStateBuffer = pooledBuffer.GetBuffer().Slice(0, _patrolStateSize);
        patrolStateBuffer.Clear();

        SpanHelpers.AsStruct<State>(patrolStateBuffer) = _patrolState;

        HmacSha256.GenerateHmacSha256(patrolStateBuffer.Slice(0, _macSize), patrolStateBuffer.Slice(_macSize),
            PatrolStateKey);

        Abort.DoAbortUnlessSuccess(_sdmmc.SelectMmcPartition(Port.Mmc0, _statePartition));

        try
        {
            _sdmmc.Write(Port.Mmc0, BytesToSectors(_patrolStateOffset), BytesToSectors(_patrolStateSize),
                patrolStateBuffer);
        }
        finally
        {
            Abort.DoAbortUnlessSuccess(_sdmmc.SelectMmcPartition(Port.Mmc0, MmcPartition.UserData));
        }
    }

    private bool DoPatrolRead()
    {
        using var pooledBuffer = new PooledBuffer(_patrolReadSize, 1);
        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        if (pooledBuffer.GetSize() < _patrolReadSize)
        {
            if (_allocateFailureCount != ulong.MaxValue)
                _allocateFailureCount++;

            return false;
        }

        if (_allocateSuccessCount != ulong.MaxValue)
            _allocateSuccessCount++;

        _sdmmc.Read(pooledBuffer.GetBuffer().Slice(0, _patrolReadSize), Port.Mmc0, _patrolState.CurrentSector,
            BytesToSectors(_patrolReadSize)).IgnoreResult();

        _patrolState.CurrentSector += BytesToSectors(_patrolReadSize);

        if (_patrolState.CurrentSector + BytesToSectors(_patrolReadSize) > _deviceCapacitySectors)
        {
            _patrolState.CurrentSector = 0;
            _patrolState.PatrolCount++;
        }

        return true;
    }

    public void Start()
    {
        Assert.SdkAssert(_threadState is ThreadState.Active or ThreadState.Stop);

        if (_threadState == ThreadState.Stop)
        {
            if (!_areEventsInitialized)
            {
                _areEventsInitialized = true;
                _threadState = ThreadState.Stop;

                _stateChangeRequestedEvent = new Event(_sdmmc.Hos.Os, EventClearMode.AutoClear);
                _stateChangeCompletedEvent = new Event(_sdmmc.Hos.Os, EventClearMode.AutoClear);
            }

            _threadState = ThreadState.Active;

            // CreateThread(_patrolReaderThread, PatrolReaderThreadEntry, this, pPatrolReaderStack, priority);
            // SetThreadNamePointer(_patrolReaderThread, "nn.fs.PatrolReader"u8);
            // StartThread(_patrolReaderThread);
        }
    }

    public void Stop()
    {
        Assert.SdkAssert(_threadState is ThreadState.Active or ThreadState.Stop or ThreadState.Sleep);

        if (_threadState != ThreadState.Stop)
        {
            _threadState = ThreadState.Stop;
            _stateChangeRequestedEvent.Signal();

            // WaitThread(_patrolReaderThread);
            // DestroyThread(_patrolReaderThread);
        }
    }

    public void Sleep()
    {
        Assert.SdkAssert(_threadState is ThreadState.Active or ThreadState.Sleep);

        if (_threadState == ThreadState.Active)
        {
            _threadState = ThreadState.Sleep;

            _stateChangeRequestedEvent.Signal();
            _stateChangeCompletedEvent.Wait();
        }
    }

    public void Resume()
    {
        Assert.SdkAssert(_threadState is ThreadState.Active or ThreadState.Sleep);

        if (_threadState == ThreadState.Sleep)
        {
            _threadState = ThreadState.Active;

            _stateChangeRequestedEvent.Signal();
            _stateChangeCompletedEvent.Wait();
        }
    }

    private void PatrolReaderThread()
    {
        // Missing: SetServiceContext()

        using var timer = new TimerEvent(_sdmmc.Hos.Os, EventClearMode.AutoClear);

        timer.StartPeriodic(TimeSpan.FromSeconds(_firstRunDelaySeconds), TimeSpan.FromSeconds(_saveStateIntervalSeconds));

        int currentWaitTime = _waitTimeAfterAllocationSuccess;

        while (true)
        {
            // Wait until the next thread state change or until the next patrol read should be done.
            if (_stateChangeRequestedEvent.TimedWait(TimeSpan.FromSeconds(currentWaitTime)))
            {
                if (_threadState == ThreadState.Stop)
                {
                    break;
                }

                if (_threadState == ThreadState.Sleep)
                {
                    // Acknowledge the request to sleep and wait for the next state change.
                    _stateChangeCompletedEvent.Signal();
                    _stateChangeRequestedEvent.Wait();

                    if (_threadState == ThreadState.Stop)
                    {
                        break;
                    }

                    if (_threadState == ThreadState.Active)
                    {
                        _stateChangeCompletedEvent.Signal();
                        continue;
                    }

                    Assert.SdkAssert(false);
                }
            }

            if (!_isPatrolStateLoaded)
            {
                // The patrol state will be loaded a single time when the console is booted.
                // Don't load the patrol state or do patrol reads until the specified amount
                // of time has past since creating the Patrol Reader.
                if (!timer.TryWait() || !LoadState())
                {
                    continue;
                }

                _isPatrolStateLoaded = true;
            }

            if (DoPatrolRead())
            {
                currentWaitTime = _waitTimeAfterAllocationSuccess;
            }
            else
            {
                currentWaitTime = _waitTimeAfterAllocationFailure;
            }

            // Save the patrol state periodically once the specified amount of time has passed since the last save.
            if (timer.TryWait())
            {
                SaveState();
            }
        }

        // Save the patrol state if necessary when shutting down.
        if (_isPatrolStateLoaded)
            SaveState();
    }
}