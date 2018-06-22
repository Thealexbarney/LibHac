using System.IO;

namespace libhac
{
    public class Pfs0
    {
        public Pfs0Superblock Superblock { get; set; }
    }

    public class Pfs0Superblock
    {
        public byte[] MasterHash; /* SHA-256 hash of the hash table. */
        public uint BlockSize; /* In bytes. */
        public uint Always2;
        public ulong HashTableOffset; /* Normally zero. */
        public ulong HashTableSize;
        public ulong Pfs0Offset;
        public ulong Pfs0Size;

        public Pfs0Superblock(BinaryReader reader)
        {
            MasterHash = reader.ReadBytes(0x20);
            BlockSize = reader.ReadUInt32();
            Always2 = reader.ReadUInt32();
            HashTableOffset = reader.ReadUInt64();
            HashTableSize = reader.ReadUInt64();
            Pfs0Offset = reader.ReadUInt64();
            Pfs0Size = reader.ReadUInt64();
            reader.BaseStream.Position += 0xF0;
        }
    }
}
