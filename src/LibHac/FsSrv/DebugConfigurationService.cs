using System;
using LibHac.Common.FixedArrays;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Shim;
using LibHac.FsSrv.Impl;
using LibHac.Os;

namespace LibHac.FsSrv;

/// <summary>
/// Handles debug configuration calls for <see cref="FileSystemProxyImpl"/>.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public struct DebugConfigurationService
{
    private DebugConfigurationServiceImpl _serviceImpl;
    private ulong _processId;

    // LibHac addition
    private readonly FileSystemServer _fsServer;

    public DebugConfigurationService(FileSystemServer fsServer, DebugConfigurationServiceImpl serviceImpl,
        ulong processId)
    {
        _serviceImpl = serviceImpl;
        _processId = processId;
        _fsServer = fsServer;
    }

    private Result GetProgramInfo(out ProgramInfo programInfo, ulong processId)
    {
        using var programRegistry = new ProgramRegistryImpl(_fsServer);

        return programRegistry.GetProgramInfo(out programInfo, processId);
    }

    public Result Register(uint key, long value)
    {
        Result rc = GetProgramInfo(out ProgramInfo programInfo, _processId);
        if (rc.IsFailure()) return rc.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.SetDebugConfiguration))
            return ResultFs.PermissionDenied.Log();

        _serviceImpl.Register(key, value);
        return Result.Success;
    }

    public Result Unregister(uint key)
    {
        Result rc = GetProgramInfo(out ProgramInfo programInfo, _processId);
        if (rc.IsFailure()) return rc.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.SetDebugConfiguration))
            return ResultFs.PermissionDenied.Log();

        _serviceImpl.Unregister(key);
        return Result.Success;
    }
}

/// <summary>
/// Manages a key-value list of debug settings.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public class DebugConfigurationServiceImpl : IDisposable
{
    private Configuration _config;
    private Array4<Entry> _entries;
    private SdkMutexType _mutex;

    public struct Configuration
    {
        public bool IsDisabled;
    }

    private struct Entry
    {
        public uint Key;
        public long Value;
    }

    public DebugConfigurationServiceImpl(in Configuration config)
    {
        _config = config;
        _mutex = new SdkMutexType();
    }

    public void Dispose() { }

    public void Register(uint key, long value)
    {
        Abort.DoAbortUnless(key != 0);

        if (_config.IsDisabled)
            return;

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        for (int i = 0; i < _entries.ItemsRo.Length; i++)
        {
            // Update the existing value if the key is already registered
            if (_entries[i].Key == key)
            {
                _entries[i].Value = value;
                return;
            }
        }

        for (int i = 0; i < _entries.ItemsRo.Length; i++)
        {
            if (_entries[i].Key == 0)
            {
                _entries[i].Key = key;
                _entries[i].Value = value;
                return;
            }
        }

        Abort.DoAbort();
    }

    public void Unregister(uint key)
    {
        Abort.DoAbortUnless(key != 0);

        if (_config.IsDisabled)
            return;

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        for (int i = 0; i < _entries.ItemsRo.Length; i++)
        {
            if (_entries[i].Key == key)
            {
                _entries[i].Key = 0;
                _entries[i].Value = 0;
                break;
            }
        }
    }

    public long Get(DebugOptionKey key, long defaultValue)
    {
        Abort.DoAbortUnless(key != 0);

        if (_config.IsDisabled)
            return defaultValue;

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        for (int i = 0; i < _entries.ItemsRo.Length; i++)
        {
            if (_entries[i].Key == (uint)key)
            {
                return _entries[i].Value;
            }
        }

        return defaultValue;
    }
}