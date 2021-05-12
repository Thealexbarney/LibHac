using System;

namespace LibHac
{
    public readonly struct ApplicationId : IEquatable<ApplicationId>
    {
        public static ApplicationId InvalidId => default;

        public readonly ulong Value;

        public ApplicationId(ulong value)
        {
            Value = value;
        }

        public static bool operator ==(ApplicationId left, ApplicationId right) => left.Value == right.Value;
        public static bool operator !=(ApplicationId left, ApplicationId right) => left.Value != right.Value;

        public override bool Equals(object obj) => obj is ApplicationId id && Equals(id);
        public bool Equals(ApplicationId other) => Value == other.Value;
        public override int GetHashCode() => HashCode.Combine(Value);
    }
}
