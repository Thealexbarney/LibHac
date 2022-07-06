using System;
using System.Runtime.CompilerServices;
using System.Threading;
using LibHac.Common;

namespace LibHac.Os;

/// <summary>
/// Specifies that a constructed <see cref="UniqueLock{TMutex}"/> should not be automatically locked upon construction.<br/>
/// Used only to differentiate between <see cref="UniqueLock{TMutex}"/> constructor signatures.
/// </summary>
public struct DeferLock { }

public static class UniqueLock
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UniqueLockRef<TMutex> Lock<TMutex>(ref TMutex lockable) where TMutex : struct, ILockable
    {
        return new UniqueLockRef<TMutex>(ref lockable);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UniqueLock<TMutex> Lock<TMutex>(TMutex lockable) where TMutex : class, ILockable
    {
        return new UniqueLock<TMutex>(lockable);
    }

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
    public static unsafe ref UniqueLockRef<T> Ref<T>(this scoped in UniqueLockRef<T> value) where T : struct, ILockable
    {
        fixed (UniqueLockRef<T>* p = &value)
        {
            return ref *p;
        }
    }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

    public static ref UniqueLock<T> Ref<T>(this in UniqueLock<T> value) where T : class, ILockable
    {
        return ref Unsafe.AsRef(in value);
    }
}

[NonCopyableDisposable]
public ref struct UniqueLockRef<TMutex> where TMutex : struct, ILockable
{
    private Ref<TMutex> _mutex;
    private bool _ownsLock;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UniqueLockRef(ref TMutex mutex)
    {
        _mutex = new Ref<TMutex>(ref mutex);
        mutex.Lock();
        _ownsLock = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UniqueLockRef(ref TMutex mutex, DeferLock tag)
    {
        _mutex = new Ref<TMutex>(ref mutex);
        _ownsLock = false;
    }

    public UniqueLockRef(ref UniqueLockRef<TMutex> other)
    {
        _mutex = other._mutex;
        _ownsLock = other._ownsLock;

        other = default;
    }

    public void Set(scoped ref UniqueLockRef<TMutex> other)
    {
        if (_ownsLock)
            _mutex.Value.Unlock();

        _mutex = default;
        _ownsLock = false;

        _mutex = other._mutex;
        _ownsLock = other._ownsLock;

        other = default;
    }

    public void Lock()
    {
        if (_mutex.IsNull)
            throw new SynchronizationLockException("UniqueLock.Lock: References null mutex");

        if (_ownsLock)
            throw new SynchronizationLockException("UniqueLock.Lock: Already locked");

        _mutex.Value.Lock();
        _ownsLock = true;
    }

    public bool TryLock()
    {
        if (_mutex.IsNull)
            throw new SynchronizationLockException("UniqueLock.TryLock: References null mutex");

        if (_ownsLock)
            throw new SynchronizationLockException("UniqueLock.TryLock: Already locked");

        _ownsLock = _mutex.Value.TryLock();
        return _ownsLock;
    }

    public void Unlock()
    {
        if (_ownsLock)
            throw new SynchronizationLockException("UniqueLock.Unlock: Not locked");

        _mutex.Value.Unlock();
        _ownsLock = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_ownsLock)
            _mutex.Value.Unlock();

        this = default;
    }
}

[NonCopyableDisposable]
public struct UniqueLock<TMutex> : IDisposable where TMutex : class, ILockable
{
    private TMutex _mutex;
    private bool _ownsLock;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UniqueLock(TMutex mutex)
    {
        _mutex = mutex;
        mutex.Lock();
        _ownsLock = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UniqueLock(TMutex mutex, DeferLock tag)
    {
        _mutex = mutex;
        _ownsLock = false;
    }

    public UniqueLock(ref UniqueLock<TMutex> other)
    {
        _mutex = other._mutex;
        _ownsLock = other._ownsLock;

        other = default;
    }

    public void Set(ref UniqueLock<TMutex> other)
    {
        if (_ownsLock)
            _mutex.Unlock();

        _mutex = null;
        _ownsLock = false;

        _mutex = other._mutex;
        _ownsLock = other._ownsLock;

        other = default;
    }

    public void Reset(TMutex mutex)
    {
        if (_ownsLock)
            _mutex.Unlock();

        _mutex = null;
        _ownsLock = false;

        _mutex = mutex;
        mutex.Lock();
        _ownsLock = true;
    }

    public void Lock()
    {
        if (_mutex is null)
            throw new SynchronizationLockException("UniqueLock.Lock: References null mutex");

        if (_ownsLock)
            throw new SynchronizationLockException("UniqueLock.Lock: Already locked");

        _mutex.Lock();
        _ownsLock = true;
    }

    public bool TryLock()
    {
        if (_mutex is null)
            throw new SynchronizationLockException("UniqueLock.TryLock: References null mutex");

        if (_ownsLock)
            throw new SynchronizationLockException("UniqueLock.TryLock: Already locked");

        _ownsLock = _mutex.TryLock();
        return _ownsLock;
    }

    public void Unlock()
    {
        if (_ownsLock)
            throw new SynchronizationLockException("UniqueLock.Unlock: Not locked");

        _mutex.Unlock();
        _ownsLock = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_ownsLock)
            _mutex.Unlock();

        this = default;
    }
}