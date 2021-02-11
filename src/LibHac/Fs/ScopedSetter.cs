using LibHac.Common;

namespace LibHac.Fs
{
    public ref struct ScopedSetter<T>
    {
        private Ref<T> _ref;
        private T _value;

        public ScopedSetter(ref T reference, T value)
        {
            _ref = new Ref<T>(ref reference);
            _value = value;
        }

        public void Dispose()
        {
            if (!_ref.IsNull)
                _ref.Value = _value;
        }

        public void Set(T value) => _value = value;

        public static ScopedSetter<T> MakeScopedSetter(ref T reference, T value)
        {
            return new ScopedSetter<T>(ref reference, value);
        }
    }
}
