using System;
using System.Collections.Generic;
using System.Linq;

namespace LibHac.Fs
{
    public class Aes128CtrExStorage : Aes128CtrStorage
    {
        private List<AesSubsectionEntry> SubsectionEntries { get; }
        private List<long> SubsectionOffsets { get; }
        private BucketTree<AesSubsectionEntry> BucketTree { get; }

        private readonly object _locker = new object();

        public Aes128CtrExStorage(IStorage baseStorage, IStorage bucketTreeData, byte[] key, long counterOffset, byte[] ctrHi, bool leaveOpen)
            : base(baseStorage, key, counterOffset, ctrHi, leaveOpen)
        {
            BucketTree = new BucketTree<AesSubsectionEntry>(bucketTreeData);

            SubsectionEntries = BucketTree.GetEntryList();
            SubsectionOffsets = SubsectionEntries.Select(x => x.Offset).ToList();
        }

        public Aes128CtrExStorage(IStorage baseStorage, IStorage bucketTreeData, byte[] key, byte[] counter, bool leaveOpen)
            : base(baseStorage, key, counter, leaveOpen)
        {
            BucketTree = new BucketTree<AesSubsectionEntry>(bucketTreeData);

            SubsectionEntries = BucketTree.GetEntryList();
            SubsectionOffsets = SubsectionEntries.Select(x => x.Offset).ToList();
        }

        protected override Result ReadImpl(long offset, Span<byte> destination)
        {
            AesSubsectionEntry entry = GetSubsectionEntry(offset);

            long inPos = offset;
            int outPos = 0;
            int remaining = destination.Length;

            while (remaining > 0)
            {
                int bytesToRead = (int)Math.Min(entry.OffsetEnd - inPos, remaining);

                lock (_locker)
                {
                    UpdateCounterSubsection(entry.Counter);

                    Result rc = base.ReadImpl(inPos, destination.Slice(outPos, bytesToRead));
                    if (rc.IsFailure()) return rc;
                }

                outPos += bytesToRead;
                inPos += bytesToRead;
                remaining -= bytesToRead;

                if (remaining != 0 && inPos >= entry.OffsetEnd)
                {
                    entry = entry.Next;
                }
            }

            return Result.Success;
        }

        protected override Result WriteImpl(long offset, ReadOnlySpan<byte> source)
        {
            return ResultFs.UnsupportedOperationInAesCtrExStorageWrite.Log();
        }

        public override Result Flush()
        {
            return Result.Success;
        }

        private AesSubsectionEntry GetSubsectionEntry(long offset)
        {
            int index = SubsectionOffsets.BinarySearch(offset);
            if (index < 0) index = ~index - 1;
            return SubsectionEntries[index];
        }

        private void UpdateCounterSubsection(uint value)
        {
            Counter[7] = (byte)value;
            Counter[6] = (byte)(value >> 8);
            Counter[5] = (byte)(value >> 16);
            Counter[4] = (byte)(value >> 24);
        }
    }
}
