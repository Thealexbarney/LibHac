using System;
using System.Buffers.Binary;
using LibHac.Fs.Save;
using LibHac.Kvdb;

namespace LibHac.Fs
{
    public class SaveDataStruct : IComparable<SaveDataStruct>, IEquatable<SaveDataStruct>, IExportable
    {
        public ulong TitleId { get; private set; }
        public UserId UserId { get; private set; }
        public ulong SaveId { get; private set; }
        public SaveDataType Type { get; private set; }
        public byte Rank { get; private set; }
        public short Index { get; private set; }

        public int ExportSize => 0x40;
        private bool _isFrozen;

        public void ToBytes(Span<byte> output)
        {
            if (output.Length < ExportSize) throw new InvalidOperationException("Output buffer is too small.");

            BinaryPrimitives.WriteUInt64LittleEndian(output, TitleId);
            UserId.ToBytes(output.Slice(8));
            BinaryPrimitives.WriteUInt64LittleEndian(output.Slice(0x18), SaveId);
            output[0x20] = (byte)Type;
            output[0x21] = Rank;
            BinaryPrimitives.WriteInt16LittleEndian(output.Slice(0x22), Index);
        }

        public void FromBytes(ReadOnlySpan<byte> input)
        {
            if (_isFrozen) throw new InvalidOperationException("Unable to modify frozen object.");
            if (input.Length < ExportSize) throw new InvalidOperationException("Input data is too short.");

            TitleId = BinaryPrimitives.ReadUInt64LittleEndian(input);
            UserId = new UserId(input.Slice(8));
            SaveId = BinaryPrimitives.ReadUInt64LittleEndian(input.Slice(0x18));
            Type = (SaveDataType)input[0x20];
            Rank = input[0x21];
            Index = BinaryPrimitives.ReadInt16LittleEndian(input.Slice(0x22));
        }

        public void Freeze() => _isFrozen = true;

        public bool Equals(SaveDataStruct other)
        {
            return other != null && TitleId == other.TitleId && UserId.Equals(other.UserId) && SaveId == other.SaveId &&
                   Type == other.Type && Rank == other.Rank && Index == other.Index;
        }

        public override bool Equals(object obj)
        {
            return obj is SaveDataStruct other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                // ReSharper disable NonReadonlyMemberInGetHashCode
                int hashCode = TitleId.GetHashCode();
                hashCode = (hashCode * 397) ^ UserId.GetHashCode();
                hashCode = (hashCode * 397) ^ SaveId.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Type;
                hashCode = (hashCode * 397) ^ Rank.GetHashCode();
                hashCode = (hashCode * 397) ^ Index.GetHashCode();
                return hashCode;
                // ReSharper restore NonReadonlyMemberInGetHashCode
            }
        }

        public int CompareTo(SaveDataStruct other)
        {
            int titleIdComparison = TitleId.CompareTo(other.TitleId);
            if (titleIdComparison != 0) return titleIdComparison;
            int typeComparison = Type.CompareTo(other.Type);
            if (typeComparison != 0) return typeComparison;
            int userIdComparison = UserId.CompareTo(other.UserId);
            if (userIdComparison != 0) return userIdComparison;
            int saveIdComparison = SaveId.CompareTo(other.SaveId);
            if (saveIdComparison != 0) return saveIdComparison;
            int rankComparison = Rank.CompareTo(other.Rank);
            if (rankComparison != 0) return rankComparison;
            return Index.CompareTo(other.Index);
        }

        public int CompareTo(object obj)
        {
            if (obj is null) return 1;
            return obj is SaveDataStruct other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(SaveDataStruct)}");
        }
    }
}
