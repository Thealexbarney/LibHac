using System.IO;

namespace LibHac
{
    public class RelocationBlock
    {
        public uint Field0;
        public int BucketCount;
        public long Size;
        public long[] BaseOffsets;
        public RelocationBucket[] Buckets;

        public RelocationBlock(BinaryReader reader)
        {
            var start = reader.BaseStream.Position;

            Field0 = reader.ReadUInt32();
            BucketCount = reader.ReadInt32();
            Size = reader.ReadInt64();
            BaseOffsets = new long[BucketCount];
            Buckets = new RelocationBucket[BucketCount];

            for (int i = 0; i < BucketCount; i++)
            {
                BaseOffsets[i] = reader.ReadInt64();
            }

            reader.BaseStream.Position = start + 0x4000;

            for (int i = 0; i < BucketCount; i++)
            {
                Buckets[i] = new RelocationBucket(reader);
            }
        }
    }

    public class RelocationBucket
    {
        public int BucketNum;
        public int EntryCount;
        public long VirtualOffsetEnd;
        public RelocationEntry[] Entries;

        public RelocationBucket(BinaryReader reader)
        {
            var start = reader.BaseStream.Position;

            BucketNum = reader.ReadInt32();
            EntryCount = reader.ReadInt32();
            VirtualOffsetEnd = reader.ReadInt64();
            Entries = new RelocationEntry[EntryCount];

            for (int i = 0; i < EntryCount; i++)
            {
                Entries[i] = new RelocationEntry(reader);
            }

            reader.BaseStream.Position = start + 0x4000;
        }
    }

    public class RelocationEntry
    {
        public long VirtOffset;
        public long VirtOffsetEnd;
        public long PhysOffset;
        public bool IsPatch;
        public RelocationEntry Next;

        public RelocationEntry(BinaryReader reader)
        {
            VirtOffset = reader.ReadInt64();
            PhysOffset = reader.ReadInt64();
            IsPatch = reader.ReadInt32() != 0;
        }
    }

    public class SubsectionBlock
    {
        public uint Field0;
        public int BucketCount;
        public long Size;
        public long[] BaseOffsets;
        public SubsectionBucket[] Buckets;

        public SubsectionBlock(BinaryReader reader)
        {
            var start = reader.BaseStream.Position;

            Field0 = reader.ReadUInt32();
            BucketCount = reader.ReadInt32();
            Size = reader.ReadInt64();
            BaseOffsets = new long[BucketCount];
            Buckets = new SubsectionBucket[BucketCount];

            for (int i = 0; i < BucketCount; i++)
            {
                BaseOffsets[i] = reader.ReadInt64();
            }

            reader.BaseStream.Position = start + 0x4000;

            for (int i = 0; i < BucketCount; i++)
            {
                Buckets[i] = new SubsectionBucket(reader);
            }
        }
    }

    public class SubsectionBucket
    {
        public int BucketNum;
        public int EntryCount;
        public long VirtualOffsetEnd;
        public SubsectionEntry[] Entries;
        public SubsectionBucket(BinaryReader reader)
        {
            var start = reader.BaseStream.Position;

            BucketNum = reader.ReadInt32();
            EntryCount = reader.ReadInt32();
            VirtualOffsetEnd = reader.ReadInt64();
            Entries = new SubsectionEntry[EntryCount];

            for (int i = 0; i < EntryCount; i++)
            {
                Entries[i] = new SubsectionEntry(reader);
            }

            reader.BaseStream.Position = start + 0x4000;
        }
    }

    public class SubsectionEntry
    {
        public long Offset;
        public uint Field8;
        public uint Counter;

        public SubsectionEntry Next;
        public long OffsetEnd;

        public SubsectionEntry() { }

        public SubsectionEntry(BinaryReader reader)
        {
            Offset = reader.ReadInt64();
            Field8 = reader.ReadUInt32();
            Counter = reader.ReadUInt32();
        }
    }
}
