using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LibHac.Common;

namespace LibHac.Fs
{
    [DebuggerDisplay("{ToString()}")]
    [StructLayout(LayoutKind.Sequential, Size = 0x10)]
    public struct EncryptionSeed : IEquatable<EncryptionSeed>
    {
        private readonly Key128 Key;

        public readonly ReadOnlySpan<byte> Value => SpanHelpers.AsReadOnlyByteSpan(in this);

        public EncryptionSeed(ReadOnlySpan<byte> bytes)
        {
            Key = new Key128(bytes);
        }

        public override string ToString() => Key.ToString();

        public override bool Equals(object obj) => obj is EncryptionSeed key && Equals(key);
        public bool Equals(EncryptionSeed other) => Key.Equals(other.Key);
        public override int GetHashCode() => Key.GetHashCode();
        public static bool operator ==(EncryptionSeed left, EncryptionSeed right) => left.Equals(right);
        public static bool operator !=(EncryptionSeed left, EncryptionSeed right) => !(left == right);
    }
}
