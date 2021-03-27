using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;

namespace LibHac.Util
{
    public struct Optional<T>
    {
        private bool _hasValue;
        private T _value;

        public readonly bool HasValue => _hasValue;
        public ref T Value
        {
            get
            {
                Assert.SdkRequires(_hasValue);
                // It's beautiful, working around C# rules
                return ref MemoryMarshal.GetReference(SpanHelpers.CreateSpan(ref _value, 1));
            }
        }
        public readonly ref readonly T ValueRo
        {
            get
            {
                Assert.SdkRequires(_hasValue);
                return ref MemoryMarshal.GetReference(SpanHelpers.CreateReadOnlySpan(in _value, 1));
            }
        }

        public Optional(in T value)
        {
            _value = value;
            _hasValue = true;
        }

        public static implicit operator Optional<T>(in T value) => new Optional<T>(in value);

        public void Set(in T value)
        {
            _value = value;
            _hasValue = true;
        }

        public void Clear()
        {
            _hasValue = false;
            _value = default;
        }
    }
}
