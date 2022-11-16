using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using LibHac.Diag;

namespace LibHac.Util;

public struct Optional<T>
{
    private bool _hasValue;
    private T _value;

    public readonly bool HasValue => _hasValue;

    public ref T Value
    {
        [UnscopedRef]
        get
        {
            Assert.SdkRequires(_hasValue);
            return ref _value;
        }
    }

    public readonly ref readonly T ValueRo
    {
        [UnscopedRef]
        get
        {
            Assert.SdkRequires(_hasValue);
            return ref _value;
        }
    }

    public Optional(in T value)
    {
        _value = value;
        _hasValue = true;
    }

    public Optional(T value)
    {
        _value = value;
        _hasValue = true;
    }

    public static implicit operator Optional<T>(in T value) => new Optional<T>(in value);

    public void Set()
    {
        _hasValue = true;
    }

    public void Set(T value)
    {
        _value = value;
        _hasValue = true;
    }

    public void Set(in T value)
    {
        _value = value;
        _hasValue = true;
    }

    public void Clear()
    {
        _hasValue = false;

        // Clear types with references so the GC doesn't think we still need any contained objects
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            _value = default;
        }
    }
}