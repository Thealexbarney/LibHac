using System;
using System.Diagnostics;

namespace LibHac.Ncm
{
    [DebuggerDisplay("{" + nameof(Value) + "}")]
    public struct TitleId : IEquatable<TitleId>, IComparable<TitleId>, IComparable
    {
        public static TitleId Zero => default;

        public readonly ulong Value;

        public TitleId(ulong value)
        {
            Value = value;
        }

        public static explicit operator ulong(TitleId titleId) => titleId.Value;

        public override string ToString() => $"{Value:X16}";

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return 1;
            return obj is TitleId other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(TitleId)}");
        }

        public int CompareTo(TitleId other) => Value.CompareTo(other.Value);
        public bool Equals(TitleId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is TitleId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public static bool operator ==(TitleId left, TitleId right) => left.Equals(right);
        public static bool operator !=(TitleId left, TitleId right) => !left.Equals(right);
    }
}
