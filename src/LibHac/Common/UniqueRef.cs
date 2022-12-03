using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace LibHac.Common;

[NonCopyableDisposable]
public struct UniqueRef<T> : IDisposable where T : class, IDisposable
{
    private T _value;

    [UnscopedRef]
    public readonly ref readonly T Get => ref _value;

    [UnscopedRef]
    public readonly ref UniqueRef<T> Ref => ref Unsafe.AsRef(in this);

    public readonly bool HasValue => Get is not null;

    public UniqueRef(T value)
    {
        _value = value;
    }

    public UniqueRef(ref UniqueRef<T> other)
    {
        _value = other.Release();
    }

    [Obsolete("This method should never be manually called. Use the Destroy method instead.", true)]
    public void Dispose()
    {
        _value?.Dispose();
    }

    /// <summary>
    /// Used to manually dispose the <see cref="UniqueRef{T}"/> from the Dispose methods of other types.
    /// </summary>
    public void Destroy()
    {
        Reset();
    }

    public static UniqueRef<T> Create<TFrom>(ref UniqueRef<TFrom> other) where TFrom : class, T
    {
        return new UniqueRef<T>(other.Release());
    }

    public void Swap(ref UniqueRef<T> other)
    {
        (other._value, _value) = (_value, other._value);
    }

    public void Reset() => Reset(null);

    public void Reset(T value)
    {
        T oldValue = _value;
        _value = value;

        oldValue?.Dispose();
    }

    public void Set(ref UniqueRef<T> other)
    {
        if (Unsafe.AreSame(ref this, ref other))
            return;

        Reset(other.Release());
    }

    public void Set<TFrom>(ref UniqueRef<TFrom> other) where TFrom : class, T
    {
        Reset(other.Release());
    }

    public T Release()
    {
        T oldValue = _value;
        _value = null;

        return oldValue;
    }
}