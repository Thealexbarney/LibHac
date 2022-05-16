using System;
using System.Runtime.CompilerServices;
using System.Threading;
using LibHac.Common;

namespace LibHac.Os;

public static class SharedLock
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SharedLockRef<TMutex> Lock<TMutex>(ref TMutex lockable) where TMutex : struct, ISharedMutex
    {
        return new SharedLockRef<TMutex>(ref lockable);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SharedLock<TMutex> Lock<TMutex>(TMutex lockable) where TMutex : class, ISharedMutex
    {
        return new SharedLock<TMutex>(lockable);
    }

#pragma warning disable LH0001 // DoNotCopyValue
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
    public static unsafe ref SharedLockRef<T> Ref<T>(this scoped in SharedLockRef<T> value) where T : struct, ISharedMutex
    {
        fixed (SharedLockRef<T>* p = &value)
        {
            return ref *p;
        }
    }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
#pragma warning restore LH0001 // DoNotCopyValue

    public static ref SharedLock<T> Ref<T>(this in SharedLock<T> value) where T : class, ISharedMutex
    {
        return ref Unsafe.AsRef(in value);
    }
}

[NonCopyableDisposable]
public ref struct SharedLockRef<TMutex> where TMutex : struct, ISharedMutex
{
    private Ref<TMutex> _mutex;
    private bool _ownsLock;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SharedLockRef(ref TMutex mutex)
    {
        _mutex = new Ref<TMutex>(ref mutex);
        mutex.LockShared();
        _ownsLock = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SharedLockRef(ref TMutex mutex, DeferLock tag)
    {
        _mutex = new Ref<TMutex>(ref mutex);
        _ownsLock = false;
    }

    public SharedLockRef(ref SharedLockRef<TMutex> other)
    {
        _mutex = other._mutex;
        _ownsLock = other._ownsLock;

        other = default;
    }

    public void Set(ref SharedLockRef<TMutex> other)
    {
        if (_ownsLock)
            _mutex.Value.UnlockShared();

        _mutex = default;
        _ownsLock = false;

        _mutex = other._mutex;
        _ownsLock = other._ownsLock;

        other = default;
    }

    public void Lock()
    {
        if (_mutex.IsNull)
            throw new SynchronizationLockException("SharedLock.Lock: References null mutex");

        if (_ownsLock)
            throw new SynchronizationLockException("SharedLock.Lock: Already locked");

        _mutex.Value.LockShared();
        _ownsLock = true;
    }

    public bool TryLock()
    {
        if (_mutex.IsNull)
            throw new SynchronizationLockException("SharedLock.TryLock: References null mutex");

        if (_ownsLock)
            throw new SynchronizationLockException("SharedLock.TryLock: Already locked");

        _ownsLock = _mutex.Value.TryLockShared();
        return _ownsLock;
    }

    public void Unlock()
    {
        if (_ownsLock)
            throw new SynchronizationLockException("SharedLock.Unlock: Not locked");

        _mutex.Value.UnlockShared();
        _ownsLock = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_ownsLock)
            _mutex.Value.UnlockShared();

        this = default;
    }
}

[NonCopyableDisposable]
public struct SharedLock<TMutex> : IDisposable where TMutex : class, ISharedMutex
{
    private TMutex _mutex;
    private bool _ownsLock;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SharedLock(TMutex mutex)
    {
        _mutex = mutex;
        mutex.LockShared();
        _ownsLock = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SharedLock(TMutex mutex, DeferLock tag)
    {
        _mutex = mutex;
        _ownsLock = false;
    }

    public SharedLock(ref SharedLock<TMutex> other)
    {
        _mutex = other._mutex;
        _ownsLock = other._ownsLock;

        other = default;
    }

    public void Set(ref SharedLock<TMutex> other)
    {
        if (_ownsLock)
            _mutex.UnlockShared();

        _mutex = null;
        _ownsLock = false;

        _mutex = other._mutex;
        _ownsLock = other._ownsLock;

        other = default;
    }

    public void Reset(TMutex mutex)
    {
        if (_ownsLock)
            _mutex.UnlockShared();

        _mutex = null;
        _ownsLock = false;

        _mutex = mutex;
        mutex.LockShared();
        _ownsLock = true;
    }

    public void Lock()
    {
        if (_mutex is null)
            throw new SynchronizationLockException("SharedLock.Lock: References null mutex");

        if (_ownsLock)
            throw new SynchronizationLockException("SharedLock.Lock: Already locked");

        _mutex.LockShared();
        _ownsLock = true;
    }

    public bool TryLock()
    {
        if (_mutex is null)
            throw new SynchronizationLockException("SharedLock.TryLock: References null mutex");

        if (_ownsLock)
            throw new SynchronizationLockException("SharedLock.TryLock: Already locked");

        _ownsLock = _mutex.TryLockShared();
        return _ownsLock;
    }

    public void Unlock()
    {
        if (_ownsLock)
            throw new SynchronizationLockException("SharedLock.Unlock: Not locked");

        _mutex.UnlockShared();
        _ownsLock = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_ownsLock)
            _mutex.UnlockShared();

        this = default;
    }
}