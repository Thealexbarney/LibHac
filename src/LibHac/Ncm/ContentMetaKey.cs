using System;
using System.Buffers.Binary;
using LibHac.Kvdb;

namespace LibHac.Ncm
{
    public class ContentMetaKey : IComparable<ContentMetaKey>, IComparable, IEquatable<ContentMetaKey>, IExportable
    {
        public ulong TitleId { get; private set; }
        public uint Version { get; private set; }
        public byte Type { get; private set; }
        public byte Flags { get; private set; }

        public int ExportSize => 0x10;
        private bool _isFrozen;

        public void ToBytes(Span<byte> output)
        {
            if (output.Length < ExportSize) throw new InvalidOperationException("Output buffer is too small.");

            BinaryPrimitives.WriteUInt64LittleEndian(output, TitleId);
            BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(8), Version);
            output[0xC] = Type;
            output[0xD] = Flags;
        }

        public void FromBytes(ReadOnlySpan<byte> input)
        {
            if (_isFrozen) throw new InvalidOperationException("Unable to modify frozen object.");
            if (input.Length < ExportSize) throw new InvalidOperationException("Input data is too short.");

            TitleId = BinaryPrimitives.ReadUInt64LittleEndian(input);
            Version = BinaryPrimitives.ReadUInt32LittleEndian(input.Slice(8));
            Type = input[0xC];
            Flags = input[0xD];
        }

        public void Freeze() => _isFrozen = true;

        public bool Equals(ContentMetaKey other)
        {
            return other != null && TitleId == other.TitleId && Version == other.Version &&
                   Type == other.Type && Flags == other.Flags;
        }

        public override bool Equals(object obj)
        {
            return obj is ContentMetaKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                // ReSharper disable NonReadonlyMemberInGetHashCode
                int hashCode = TitleId.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Version;
                hashCode = (hashCode * 397) ^ Type.GetHashCode();
                hashCode = (hashCode * 397) ^ Flags.GetHashCode();
                return hashCode;
                // ReSharper restore NonReadonlyMemberInGetHashCode
            }
        }

        public int CompareTo(ContentMetaKey other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            int titleIdComparison = TitleId.CompareTo(other.TitleId);
            if (titleIdComparison != 0) return titleIdComparison;
            int versionComparison = Version.CompareTo(other.Version);
            if (versionComparison != 0) return versionComparison;
            int typeComparison = Type.CompareTo(other.Type);
            if (typeComparison != 0) return typeComparison;
            return Flags.CompareTo(other.Flags);
        }

        public int CompareTo(object obj)
        {
            if (obj is null) return 1;
            return obj is ContentMetaKey other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(ContentMetaKey)}");
        }
    }
}
