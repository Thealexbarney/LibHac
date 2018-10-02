using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibHac.Streams;

namespace LibHac
{
    public class BktrCryptoStream : Aes128CtrStream
    {
        public SubsectionBlock SubsectionBlock { get; }
        private List<SubsectionEntry> SubsectionEntries { get; } = new List<SubsectionEntry>();
        private List<long> SubsectionOffsets { get; }
        private SubsectionEntry CurrentEntry { get; set; }

        public BktrCryptoStream(Stream baseStream, byte[] key, long offset, long length, long counterOffset, byte[] ctrHi, BktrSuperblock bktr)
            : base(baseStream, key, offset, length, counterOffset, ctrHi)
        {
            BktrHeader header = bktr.SubsectionHeader;
            byte[] subsectionBytes;
            using (var streamDec = new RandomAccessSectorStream(new Aes128CtrStream(baseStream, key, offset, length, counterOffset, ctrHi)))
            {
                streamDec.Position = header.Offset;
                subsectionBytes = new byte[header.Size];
                streamDec.Read(subsectionBytes, 0, subsectionBytes.Length);
            }

            using (var reader = new BinaryReader(new MemoryStream(subsectionBytes)))
            {
                SubsectionBlock = new SubsectionBlock(reader);
            }

            foreach (SubsectionBucket bucket in SubsectionBlock.Buckets)
            {
                SubsectionEntries.AddRange(bucket.Entries);
            }

            // Add a subsection for the BKTR headers to make things easier
            var headerSubsection = new SubsectionEntry
            {
                Offset = bktr.RelocationHeader.Offset,
                Counter = (uint)(ctrHi[4] << 24 | ctrHi[5] << 16 | ctrHi[6] << 8 | ctrHi[7]),
                OffsetEnd = long.MaxValue
            };
            SubsectionEntries.Add(headerSubsection);

            for (int i = 0; i < SubsectionEntries.Count - 1; i++)
            {
                SubsectionEntries[i].Next = SubsectionEntries[i + 1];
                SubsectionEntries[i].OffsetEnd = SubsectionEntries[i + 1].Offset;
            }

            SubsectionOffsets = SubsectionEntries.Select(x => x.Offset).ToList();

            CurrentEntry = GetSubsectionEntry(0);
            UpdateCounterSubsection(CurrentEntry.Counter);
            baseStream.Position = offset;
        }

        public override long Position
        {
            get => base.Position;
            set
            {
                base.Position = value;
                CurrentEntry = GetSubsectionEntry(value);
                UpdateCounterSubsection(CurrentEntry.Counter);
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int totalBytesRead = 0;
            int outPos = offset;

            while (count > 0)
            {
                int bytesToRead = (int)Math.Min(CurrentEntry.OffsetEnd - Position, count);
                int bytesRead = base.Read(buffer, outPos, bytesToRead);

                outPos += bytesRead;
                totalBytesRead += bytesRead;
                count -= bytesRead;

                if (Position >= CurrentEntry.OffsetEnd)
                {
                    CurrentEntry = CurrentEntry.Next;
                    UpdateCounterSubsection(CurrentEntry.Counter);
                }
            }

            return totalBytesRead;
        }

        private SubsectionEntry GetSubsectionEntry(long offset)
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
