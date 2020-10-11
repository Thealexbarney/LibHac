using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LibHac.Util;

namespace LibHac.Common
{
    [DebuggerDisplay("{ToString()}")]
    [StructLayout(LayoutKind.Sequential, Size = 0x10)]
    public struct Key128 : IEquatable<Key128>
    {
        private readonly ulong _dummy1;
        private readonly ulong _dummy2;

        public Span<byte> Value => SpanHelpers.AsByteSpan(ref this);

        public Key128(ReadOnlySpan<byte> bytes)
        {
            ReadOnlySpan<ulong> longs = MemoryMarshal.Cast<byte, ulong>(bytes);

            _dummy1 = longs[0];
            _dummy2 = longs[1];
        }

        public override string ToString() => Value.ToHexString();

        public override bool Equals(object obj)
        {
            return obj is Key128 key && Equals(key);
        }

        public bool Equals(Key128 other)
        {
            return _dummy1 == other._dummy1 &&
                   _dummy2 == other._dummy2;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_dummy1, _dummy2);
        }

        public static bool operator ==(Key128 left, Key128 right) => left.Equals(right);
        public static bool operator !=(Key128 left, Key128 right) => !(left == right);
    }
}
