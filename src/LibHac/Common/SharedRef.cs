using System;
using System.Runtime.CompilerServices;
using System.Threading;
using InlineIL;
using LibHac.Diag;

#pragma warning disable LH0001

namespace LibHac.Common;

public static class SharedRefExtensions
{
    // ReSharper disable once EntityNameCapturedOnly.Global
    public static ref SharedRef<T> Ref<T>(this in SharedRef<T> value) where T : class, IDisposable
    {
        IL.Emit.Ldarg(nameof(value));
        IL.Emit.Ret();
        throw IL.Unreachable();
    }

    // ReSharper disable once EntityNameCapturedOnly.Global
    public static ref WeakRef<T> Ref<T>(this in WeakRef<T> value) where T : class, IDisposable
    {
        IL.Emit.Ldarg(nameof(value));
        IL.Emit.Ret();
        throw IL.Unreachable();
    }
}

internal class RefCount
{
    private int _count;
    private int _weakCount;
    private IDisposable _value;

    public RefCount(IDisposable value)
    {
        _count = 1;
        _weakCount = 1;
        _value = value;
    }

    public int UseCount()
    {
        return _count;
    }

    public void Increment()
    {
        // This function shouldn't ever be called after the ref count is reduced to zero.
        // SharedRef clearing their RefCountBase field after decrementing the ref count *should* already
        // prevent this if everything works properly.
        Assert.SdkRequiresGreater(_count, 0);

        Interlocked.Increment(ref _count);
    }

    public void IncrementWeak()
    {
        Assert.SdkRequiresGreater(_count, 0);

        Interlocked.Increment(ref _weakCount);
    }

    public bool IncrementIfNotZero()
    {
        int count = Volatile.Read(ref _count);

        while (count != 0)
        {
            int oldValue = Interlocked.CompareExchange(ref _count, count + 1, count);
            if (oldValue == count)
            {
                return true;
            }

            count = oldValue;
        }

        return false;
    }

    public void Decrement()
    {
        if (Interlocked.Decrement(ref _count) == 0)
        {
            Destroy();
            DecrementWeak();
        }
    }

    public void DecrementWeak()
    {
        if (Interlocked.Decrement(ref _weakCount) == 0)
        {
            // Deallocate
        }
    }

    private void Destroy()
    {
        if (_value is not null)
        {
            _value.Dispose();
            _value = null;
        }
    }
}

[NonCopyableDisposable]
public struct SharedRef<T> : IDisposable where T : class, IDisposable
{
    // SharedRef and WeakRef should share a base type, but struct inheritance doesn't exist in C#.
    // This presents a problem because C# also doesn't have friend classes, these two types need to
    // access each other's fields and we'd rather the fields' visibility stay private. Because the
    // two types have the same layout we can hack around this with some Unsafe.As shenanigans.
    private T _value;
    private RefCount _refCount;

    public SharedRef(T value)
    {
        _value = value;
        _refCount = new RefCount(value);
    }

    [Obsolete("This method should never be manually called. Use the Destroy method instead.", true)]
    public void Dispose()
    {
        // This function shouldn't be called manually and should always be called at the end of a using block.
        // This means we don't need to clear any fields because we're going out of scope anyway.
        _refCount?.Decrement();
    }

    /// <summary>
    /// Used to manually dispose the <see cref="SharedRef{T}"/> from the Dispose methods of other types.
    /// </summary>
    public void Destroy()
    {
        Reset();
    }

    public readonly T Get => _value;
    public readonly bool HasValue => Get is not null;
    public readonly int UseCount => _refCount?.UseCount() ?? 0;

    public static SharedRef<T> CreateMove<TFrom>(ref SharedRef<TFrom> other) where TFrom : class, T
    {
        var sharedRef = new SharedRef<T>();

        sharedRef._value = Unsafe.As<TFrom, T>(ref other._value);
        sharedRef._refCount = other._refCount;

        other._value = null;
        other._refCount = null;

        return sharedRef;
    }

    public static SharedRef<T> CreateCopy<TFrom>(in SharedRef<TFrom> other) where TFrom : class, T
    {
        var sharedRef = new SharedRef<T>();

        sharedRef._value = Unsafe.As<TFrom, T>(ref other.Ref()._value);
        sharedRef._refCount = other._refCount;

        sharedRef._refCount?.Increment();

        return sharedRef;
    }

    public static SharedRef<T> Create<TFrom>(in WeakRef<TFrom> other) where TFrom : class, T
    {
        ref SharedRef<TFrom> otherShared = ref Unsafe.As<WeakRef<TFrom>, SharedRef<TFrom>>(ref other.Ref());

        var sharedRef = new SharedRef<T>();

        if (otherShared._refCount is not null && otherShared._refCount.IncrementIfNotZero())
        {
            sharedRef._value = Unsafe.As<TFrom, T>(ref otherShared._value);
            sharedRef._refCount = otherShared._refCount;
        }
        else
        {
            ThrowBadWeakPtr();
        }

        return sharedRef;
    }

    public static SharedRef<T> Create<TFrom>(ref UniqueRef<TFrom> other) where TFrom : class, T
    {
        var sharedRef = new SharedRef<T>();
        TFrom value = other.Get;

        if (value is not null)
        {
            sharedRef._value = value;
            sharedRef._refCount = new RefCount(value);
            other.Release();
        }
        else
        {
            sharedRef._value = null;
            sharedRef._refCount = null;
        }

        return sharedRef;
    }

    public void Swap(ref SharedRef<T> other)
    {
        (other._value, _value) = (_value, other._value);
        (other._refCount, _refCount) = (_refCount, other._refCount);
    }

    public void Reset()
    {
        _value = null;
        RefCount oldRefCount = _refCount;
        _refCount = null;

        oldRefCount?.Decrement();
    }

    public void Reset(T value)
    {
        _value = value;
        RefCount oldRefCount = _refCount;
        _refCount = new RefCount(value);

        oldRefCount?.Decrement();
    }

    public void SetByMove<TFrom>(ref SharedRef<TFrom> other) where TFrom : class, T
    {
        RefCount oldRefCount = _refCount;

        _value = Unsafe.As<TFrom, T>(ref other._value);
        _refCount = other._refCount;

        other._value = null;
        other._refCount = null;

        oldRefCount?.Decrement();
    }

    public void SetByCopy<TFrom>(in SharedRef<TFrom> other) where TFrom : class, T
    {
        RefCount oldRefCount = _refCount;
        RefCount otherRef = other._refCount;

        otherRef?.Increment();

        _value = Unsafe.As<TFrom, T>(ref other.Ref()._value);
        _refCount = otherRef;

        oldRefCount?.Decrement();
    }

    public void Set<TFrom>(ref UniqueRef<TFrom> other) where TFrom : class, T
    {
        RefCount oldRefCount = _refCount;
        TFrom otherValue = other.Release();

        if (otherValue is not null)
        {
            _value = Unsafe.As<TFrom, T>(ref otherValue);
            _refCount = new RefCount(otherValue);
        }
        else
        {
            _value = null;
            _refCount = null;
        }

        oldRefCount?.Decrement();
    }

    public bool TryCastSet<TFrom>(ref SharedRef<TFrom> other) where TFrom : class, IDisposable
    {
        RefCount oldRefCount = _refCount;
        TFrom otherValue = other.Get;

        if (otherValue is T)
        {
            _value = Unsafe.As<TFrom, T>(ref other._value);
            _refCount = other._refCount;

            other._value = null;
            other._refCount = null;

            oldRefCount?.Decrement();

            return true;
        }

        return false;
    }

    private static void ThrowBadWeakPtr()
    {
        throw new ObjectDisposedException(string.Empty, "bad_weak_ptr");
    }
}

[NonCopyableDisposable]
public struct WeakRef<T> : IDisposable where T : class, IDisposable
{
    private T _value;
    private RefCount _refCount;

    public WeakRef(in SharedRef<T> other)
    {
        this = Create(in other);
    }

    [Obsolete("This method should never be manually called. Use the Destroy method instead.", true)]
    public void Dispose()
    {
        _refCount?.DecrementWeak();
    }

    // A copy of Dispose so we can call it ourselves inside the struct
    private void DisposeInternal()
    {
        _refCount?.DecrementWeak();
    }

    /// <summary>
    /// Used to manually dispose the <see cref="WeakRef{T}"/> from the Dispose methods of other types.
    /// </summary>
    public void Destroy()
    {
        Reset();
    }

    public readonly int UseCount => _refCount?.UseCount() ?? 0;
    public readonly bool Expired => UseCount == 0;

    public static WeakRef<T> CreateMove<TFrom>(ref WeakRef<TFrom> other) where TFrom : class, T
    {
        var weakRef = new WeakRef<T>();

        weakRef._value = Unsafe.As<TFrom, T>(ref other._value);
        weakRef._refCount = other._refCount;

        other._value = null;
        other._refCount = null;

        return weakRef;
    }

    public static WeakRef<T> CreateCopy<TFrom>(in WeakRef<TFrom> other) where TFrom : class, T
    {
        var weakRef = new WeakRef<T>();

        if (other._refCount is not null)
        {
            weakRef._refCount = other._refCount;
            weakRef._refCount.IncrementWeak();

            if (weakRef._refCount.IncrementIfNotZero())
            {
                weakRef._value = Unsafe.As<TFrom, T>(ref other.Ref()._value);
                weakRef._refCount.Decrement();
            }
        }

        return weakRef;
    }

    public static WeakRef<T> Create<TFrom>(in SharedRef<TFrom> other) where TFrom : class, T
    {
        ref WeakRef<TFrom> otherWeak = ref Unsafe.As<SharedRef<TFrom>, WeakRef<TFrom>>(ref other.Ref());

        var weakRef = new WeakRef<T>();

        if (otherWeak._refCount is not null)
        {
            weakRef._value = Unsafe.As<TFrom, T>(ref otherWeak._value);
            weakRef._refCount = otherWeak._refCount;

            weakRef._refCount.IncrementWeak();
        }
        else
        {
            weakRef._value = null;
            weakRef._refCount = null;
        }

        return weakRef;
    }

    public void Swap(ref WeakRef<T> other)
    {
        (other._value, _value) = (_value, other._value);
        (other._refCount, _refCount) = (_refCount, other._refCount);
    }

    public void Reset()
    {
        var temp = new WeakRef<T>();
        Swap(ref temp);
        temp.DisposeInternal();
    }

    public void SetMove<TFrom>(ref WeakRef<TFrom> other) where TFrom : class, T
    {
        WeakRef<T> temp = CreateMove(ref other);
        Swap(ref temp);
        temp.DisposeInternal();
    }

    public void SetCopy<TFrom>(in WeakRef<TFrom> other) where TFrom : class, T
    {
        WeakRef<T> temp = CreateCopy(in other);
        Swap(ref temp);
        temp.DisposeInternal();
    }

    public void Set<TFrom>(in SharedRef<TFrom> other) where TFrom : class, T
    {
        WeakRef<T> temp = Create(in other);
        Swap(ref temp);
        temp.DisposeInternal();
    }

    public readonly SharedRef<T> Lock()
    {
        var sharedRef = new SharedRef<T>();

        if (_refCount is not null && _refCount.IncrementIfNotZero())
        {
            Unsafe.As<SharedRef<T>, WeakRef<T>>(ref sharedRef)._value = _value;
            Unsafe.As<SharedRef<T>, WeakRef<T>>(ref sharedRef)._refCount = _refCount;
        }

        return sharedRef;
    }
}