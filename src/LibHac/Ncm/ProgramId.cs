using System;
using System.Diagnostics;

namespace LibHac.Ncm
{
    [DebuggerDisplay("{" + nameof(Value) + "}")]
    public readonly struct ProgramId : IEquatable<ProgramId>, IComparable<ProgramId>
    {
        public readonly ulong Value;

        public ProgramId(ulong value)
        {
            Value = value;
        }

        public static ProgramId InvalidId => default;

        public override string ToString() => $"{Value:X16}";

        public override bool Equals(object obj) => obj is ProgramId id && Equals(id);
        public bool Equals(ProgramId other) => Value == other.Value;
        public int CompareTo(ProgramId other) => Value.CompareTo(other.Value);
        public override int GetHashCode() => Value.GetHashCode();

        public static bool operator ==(ProgramId left, ProgramId right) => left.Equals(right);
        public static bool operator !=(ProgramId left, ProgramId right) => !(left == right);
        public static bool operator <(ProgramId left, ProgramId right) => left.CompareTo(right) < 0;
        public static bool operator >(ProgramId left, ProgramId right) => left.CompareTo(right) > 0;
        public static bool operator <=(ProgramId left, ProgramId right) => left.CompareTo(right) <= 0;
        public static bool operator >=(ProgramId left, ProgramId right) => left.CompareTo(right) >= 0;
    }
}
