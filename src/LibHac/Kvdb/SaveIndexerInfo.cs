using System;
using System.Buffers.Binary;

namespace LibHac.Kvdb
{
    public class SaveIndexerInfo : IExportable
    {
        public ulong SaveId { get; private set; }
        public ulong Size { get; private set; }
        public byte SpaceId { get; private set; }
        public byte Field19 { get; private set; }

        public int ExportSize => 0x40;
        private bool _isFrozen;

        public void ToBytes(Span<byte> output)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(output, SaveId);
            BinaryPrimitives.WriteUInt64LittleEndian(output.Slice(8), Size);
            output[0x18] = SpaceId;
            output[0x19] = Field19;
        }

        public void FromBytes(ReadOnlySpan<byte> input)
        {
            if(_isFrozen) throw new InvalidOperationException("Unable to modify frozen object.");

            SaveId = BinaryPrimitives.ReadUInt64LittleEndian(input);
            Size = BinaryPrimitives.ReadUInt64LittleEndian(input.Slice(8));
            SpaceId = input[0x18];
            Field19 = input[0x19];
        }

        public void Freeze() => _isFrozen = true;
    }
}
