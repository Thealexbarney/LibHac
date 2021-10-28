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

    [NonCopyableDisposable]
    public struct UniqueRef<T> : IDisposable where T : class, IDisposable
    {
        private T _value;

        public readonly T Get => _value;
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
}
