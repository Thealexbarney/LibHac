using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using libhac.XTSSharp;

namespace libhac
{
    public class BktrCryptoStream : AesCtrStream
    {
        public SubsectionBlock SubsectionBlock { get; }
        private List<SubsectionEntry> SubsectionEntries { get; } = new List<SubsectionEntry>();
        private List<long> SubsectionOffsets { get; }
        private SubsectionEntry CurrentEntry { get; set; }

        public BktrCryptoStream(Stream baseStream, byte[] key, long offset, long length, long counterOffset, byte[] ctrHi, NcaSection section)
            : base(baseStream, key, offset, length, counterOffset, ctrHi)
        {
            if (section.Type != SectionType.Bktr) throw new ArgumentException("Section is not of type BKTR");

            var bktr = section.Header.Bktr;
            var header = bktr.SubsectionHeader;
            byte[] subsectionBytes;
            using (var streamDec = new RandomAccessSectorStream(new AesCtrStream(baseStream, key, offset, length, counterOffset, ctrHi)))
            {
                streamDec.Position = header.Offset;
                subsectionBytes = new byte[header.Size];
                streamDec.Read(subsectionBytes, 0, subsectionBytes.Length);
            }

            using (var reader = new BinaryReader(new MemoryStream(subsectionBytes)))
            {
                SubsectionBlock = new SubsectionBlock(reader);
            }

            foreach (var bucket in SubsectionBlock.Buckets)
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
            Decryptor.UpdateCounterSubsection(CurrentEntry.Counter);
            baseStream.Position = offset;
        }

        public override long Position
        {
            get => base.Position;
            set
            {
                base.Position = value;
                CurrentEntry = GetSubsectionEntry(value);
                Decryptor.UpdateCounterSubsection(CurrentEntry.Counter);
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var ret = base.Read(buffer, offset, count);
            if (Position >= CurrentEntry.OffsetEnd)
            {
                CurrentEntry = CurrentEntry.Next;
                Decryptor.UpdateCounterSubsection(CurrentEntry.Counter);
            }

            return ret;
        }

        private SubsectionEntry GetSubsectionEntry(long offset)
        {
            var index = SubsectionOffsets.BinarySearch(offset);
            if (index < 0) index = ~index - 1;
            return SubsectionEntries[index];
        }
    }
}
