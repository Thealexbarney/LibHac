using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LibHac.Common;

namespace LibHac.Spl
{
    [DebuggerDisplay("{ToString()}")]
    [StructLayout(LayoutKind.Sequential, Size = 0x10)]
    public struct AccessKey : IEquatable<AccessKey>
    {
        private readonly Key128 Key;

        public ReadOnlySpan<byte> Value => SpanHelpers.AsByteSpan(ref this);

        public AccessKey(ReadOnlySpan<byte> bytes)
        {
            Key = new Key128(bytes);
        }

        public override string ToString() => Key.ToString();

        public override bool Equals(object obj) => obj is AccessKey key && Equals(key);
        public bool Equals(AccessKey other) => Key.Equals(other.Key);
        public override int GetHashCode() => Key.GetHashCode();
        public static bool operator ==(AccessKey left, AccessKey right) => left.Equals(right);
        public static bool operator !=(AccessKey left, AccessKey right) => !(left == right);
    }
}
