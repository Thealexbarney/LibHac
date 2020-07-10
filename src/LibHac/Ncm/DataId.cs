using System;
using System.Diagnostics;

namespace LibHac.Ncm
{
    [DebuggerDisplay("{" + nameof(Value) + "}")]
    public readonly struct DataId : IEquatable<DataId>, IComparable<DataId>
    {
        public readonly ulong Value;

        public DataId(ulong value)
        {
            Value = value;
        }

        public static DataId InvalidId => default;

        public override string ToString() => $"{Value:X16}";

        public override bool Equals(object obj) => obj is DataId id && Equals(id);
        public bool Equals(DataId other) => Value == other.Value;
        public int CompareTo(DataId other) => Value.CompareTo(other.Value);
        public override int GetHashCode() => Value.GetHashCode();

        public static bool operator ==(DataId left, DataId right) => left.Equals(right);
        public static bool operator !=(DataId left, DataId right) => !(left == right);
        public static bool operator <(DataId left, DataId right) => left.CompareTo(right) < 0;
        public static bool operator >(DataId left, DataId right) => left.CompareTo(right) > 0;
        public static bool operator <=(DataId left, DataId right) => left.CompareTo(right) <= 0;
        public static bool operator >=(DataId left, DataId right) => left.CompareTo(right) >= 0;
    }
}
