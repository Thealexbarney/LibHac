using System;

namespace LibHac.Kvdb
{
    /// <summary>
    /// A class for handling any value used by <see cref="KeyValueDatabase{TKey,TValue}"/>
    /// </summary>
    public class GenericValue : IExportable
    {
        private bool _isFrozen;
        private byte[] _value;

        public int ExportSize => _value?.Length ?? 0;

        public void ToBytes(Span<byte> output)
        {
            if (output.Length < ExportSize) throw new InvalidOperationException("Output buffer is too small.");

            _value.CopyTo(output);
        }

        public void FromBytes(ReadOnlySpan<byte> input)
        {
            if (_isFrozen) throw new InvalidOperationException("Unable to modify frozen object.");

            _value = input.ToArray();
        }

        public void Freeze() => _isFrozen = true;
    }
}
