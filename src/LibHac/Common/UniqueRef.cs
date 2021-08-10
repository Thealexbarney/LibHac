using System;
using System.Runtime.CompilerServices;
using static InlineIL.IL.Emit;

namespace LibHac.Common
{
    public static class UniqueRefExtensions
    {
        // ReSharper disable once EntityNameCapturedOnly.Global
        public static ref UniqueRef<T> Ref<T>(this in UniqueRef<T> value) where T : class, IDisposable
        {
            Ldarg(nameof(value));
            Ret();
            throw InlineIL.IL.Unreachable();
        }
    }

    public struct UniqueRef<T> : IDisposable where T : class, IDisposable
    {
        private T _value;

        public void Dispose()
        {
            Release()?.Dispose();
        }

        public UniqueRef(T value)
        {
            _value = value;
        }

        public UniqueRef(ref UniqueRef<T> other)
        {
            _value = other.Release();
        }

        public readonly T Get => _value;

        public readonly bool HasValue => Get is not null;

        public void Reset() => Reset(null);

        public void Reset(T value)
        {
            T oldValue = _value;
            _value = value;

            oldValue?.Dispose();
        }

        public void Reset<TFrom>(TFrom value) where TFrom : class, T
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
}
