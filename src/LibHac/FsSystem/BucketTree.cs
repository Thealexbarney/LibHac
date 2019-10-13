using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class BucketTree<T> where T : BucketTreeEntry<T>, new()
    {
        private const int BucketAlignment = 0x4000;
        public BucketTreeBucket<OffsetEntry> BucketOffsets { get; }
        public BucketTreeBucket<T>[] Buckets { get; }

        public BucketTree(IStorage data)
        {
            var reader = new BinaryReader(data.AsStream());

            BucketOffsets = new BucketTreeBucket<OffsetEntry>(reader);

            Buckets = new BucketTreeBucket<T>[BucketOffsets.EntryCount];

            for (int i = 0; i < BucketOffsets.EntryCount; i++)
            {
                reader.BaseStream.Position = (i + 1) * BucketAlignment;
                Buckets[i] = new BucketTreeBucket<T>(reader);
            }
        }

        public List<T> GetEntryList()
        {
            List<T> list = Buckets.SelectMany(x => x.Entries).ToList();

            for (int i = 0; i < list.Count - 1; i++)
            {
                list[i].Next = list[i + 1];
                list[i].OffsetEnd = list[i + 1].Offset;
            }

            list[list.Count - 1].OffsetEnd = BucketOffsets.OffsetEnd;

            return list;
        }
    }

    public class BucketTreeBucket<T> where T : BucketTreeEntry<T>, new()
    {
        public int Index;
        public int EntryCount;
        public long OffsetEnd;
        public T[] Entries;

        public BucketTreeBucket(BinaryReader reader)
        {
            Index = reader.ReadInt32();
            EntryCount = reader.ReadInt32();
            OffsetEnd = reader.ReadInt64();
            Entries = new T[EntryCount];

            for (int i = 0; i < EntryCount; i++)
            {
                Entries[i] = new T().Read(reader);
            }
        }
    }

    public abstract class BucketTreeEntry<T> where T : BucketTreeEntry<T>
    {
        public long Offset { get; set; }
        public long OffsetEnd { get; set; }
        public T Next { get; set; }

        protected abstract void ReadSpecific(BinaryReader reader);
        internal T Read(BinaryReader reader)
        {
            Offset = reader.ReadInt64();
            ReadSpecific(reader);
            return (T)this;
        }
    }

    public class OffsetEntry : BucketTreeEntry<OffsetEntry>
    {
        protected override void ReadSpecific(BinaryReader reader) { }
    }

    public class AesSubsectionEntry : BucketTreeEntry<AesSubsectionEntry>
    {
        public uint Field8 { get; set; }
        public uint Counter { get; set; }

        protected override void ReadSpecific(BinaryReader reader)
        {
            Field8 = reader.ReadUInt32();
            Counter = reader.ReadUInt32();
        }
    }

    public class RelocationEntry : BucketTreeEntry<RelocationEntry>
    {
        public long SourceOffset { get; set; }
        public int SourceIndex { get; set; }

        protected override void ReadSpecific(BinaryReader reader)
        {
            SourceOffset = reader.ReadInt64();
            SourceIndex = reader.ReadInt32();
        }
    }
}
