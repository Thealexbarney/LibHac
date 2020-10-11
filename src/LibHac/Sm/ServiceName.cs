using System;
using System.Diagnostics;
using LibHac.Common;
using LibHac.Util;

namespace LibHac.Sm
{
    [DebuggerDisplay("{ToString()}")]
    public readonly struct ServiceName : IEquatable<ServiceName>
    {
        private const int MaxLength = 8;

        // The Name should always be assigned in the below Encode method
        // ReSharper disable once UnassignedReadonlyField
        public readonly ulong Name;

        public static ServiceName Encode(ReadOnlySpan<char> name)
        {
            var outName = new ServiceName();
            int length = Math.Min(MaxLength, name.Length);

            for (int i = 0; i < length; i++)
            {
                SpanHelpers.AsByteSpan(ref outName)[i] = (byte)name[i];
            }

            return outName;
        }

        public override bool Equals(object obj) => obj is ServiceName name && Equals(name);
        public bool Equals(ServiceName other) => Name == other.Name;

        public override int GetHashCode() => Name.GetHashCode();

        public static bool operator ==(ServiceName left, ServiceName right) => left.Equals(right);
        public static bool operator !=(ServiceName left, ServiceName right) => !(left == right);

        public override string ToString()
        {
            ulong name = Name;
            return StringUtils.Utf8ZToString(SpanHelpers.AsReadOnlyByteSpan(in name));
        }
    }
}
