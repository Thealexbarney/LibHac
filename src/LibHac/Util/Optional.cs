using System.Runtime.InteropServices;
using LibHac.Diag;

namespace LibHac.Util
{
    public struct Optional<T>
    {
        private bool _hasValue;
        private T _value;

        public bool HasValue => _hasValue;
        public ref T Value
        {
            get
            {
                Assert.AssertTrue(_hasValue);
                // It's beautiful
                return ref MemoryMarshal.CreateSpan(ref _value, 1)[0];
            }
        }

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
