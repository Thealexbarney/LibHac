using System;
using System.Collections.Generic;
using System.Linq;

namespace LibHac.IO
{
    public class Aes128CtrExStorage : Aes128CtrStorage
    {
        private List<AesSubsectionEntry> SubsectionEntries { get; }
        private List<long> SubsectionOffsets { get; }
        private BucketTree<AesSubsectionEntry> BucketTree { get; }

        public Aes128CtrExStorage(Storage baseStorage, Storage bucketTreeHeader, Storage bucketTreeData, byte[] key, long counterOffset, byte[] ctrHi, bool leaveOpen)
            : base(baseStorage, key, counterOffset, ctrHi, leaveOpen)
        {
            BucketTree = new BucketTree<AesSubsectionEntry>(bucketTreeHeader, bucketTreeData);

            SubsectionEntries = BucketTree.GetEntryList();
            SubsectionOffsets = SubsectionEntries.Select(x => x.Offset).ToList();
        }

        protected override int ReadImpl(Span<byte> destination, long offset)
        {
            AesSubsectionEntry entry = GetSubsectionEntry(offset);
            UpdateCounterSubsection(entry.Counter);

            long inPos = offset;
            int outPos = 0;
            int remaining = destination.Length;

            while (remaining > 0)
            {
                int bytesToRead = (int)Math.Min(entry.OffsetEnd - inPos, remaining);
                int bytesRead = base.ReadImpl(destination.Slice(outPos, bytesToRead), inPos);

                outPos += bytesRead;
                inPos += bytesRead;
                remaining -= bytesRead;

                if (remaining != 0 && inPos >= entry.OffsetEnd)
                {
                    entry = entry.Next;
                    UpdateCounterSubsection(entry.Counter);
                }
            }

            return outPos;
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
