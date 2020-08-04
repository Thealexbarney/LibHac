using System;
using System.Buffers.Binary;

namespace LibHac.Ncm
{
    public class ContentMetaKey : IComparable<ContentMetaKey>, IComparable, IEquatable<ContentMetaKey>
    {
        public ulong TitleId { get; private set; }
        public uint Version { get; private set; }
        public ContentMetaType Type { get; private set; }
        public ContentMetaAttribute Attributes { get; private set; }

        public int ExportSize => 0x10;
        private bool _isFrozen;

        public void ToBytes(Span<byte> output)
        {
            if (output.Length < ExportSize) throw new InvalidOperationException("Output buffer is too small.");

            BinaryPrimitives.WriteUInt64LittleEndian(output, TitleId);
            BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(8), Version);
            output[0xC] = (byte)Type;
            output[0xD] = (byte)Attributes;
        }

        public void FromBytes(ReadOnlySpan<byte> input)
        {
            if (_isFrozen) throw new InvalidOperationException("Unable to modify frozen object.");
            if (input.Length < ExportSize) throw new InvalidOperationException("Input data is too short.");

            TitleId = BinaryPrimitives.ReadUInt64LittleEndian(input);
            Version = BinaryPrimitives.ReadUInt32LittleEndian(input.Slice(8));
            Type = (ContentMetaType)input[0xC];
            Attributes = (ContentMetaAttribute)input[0xD];
        }

        public void Freeze() => _isFrozen = true;

        public bool Equals(ContentMetaKey other)
        {
            return other != null && TitleId == other.TitleId && Version == other.Version &&
                   Type == other.Type && Attributes == other.Attributes;
        }

        public override bool Equals(object obj)
        {
            return obj is ContentMetaKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            // ReSharper disable NonReadonlyMemberInGetHashCode
            return HashCode.Combine(TitleId, Version, Type, Attributes);
            // ReSharper restore NonReadonlyMemberInGetHashCode
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
            return Attributes.CompareTo(other.Attributes);
        }

        public int CompareTo(object obj)
        {
            if (obj is null) return 1;
            return obj is ContentMetaKey other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(ContentMetaKey)}");
        }
    }
}
