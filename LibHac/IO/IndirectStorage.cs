using System;
using System.Collections.Generic;
using System.Linq;

namespace LibHac.IO
{
    public class IndirectStorage : Storage
    {
        private List<RelocationEntry> RelocationEntries { get; }
        private List<long> RelocationOffsets { get; }

        private List<Storage> Sources { get; } = new List<Storage>();
        private BucketTree<RelocationEntry> BucketTree { get; }

        public IndirectStorage(Storage bucketTreeHeader, Storage bucketTreeData, bool leaveOpen, params Storage[] sources)
        {
            Sources.AddRange(sources);

            if(!leaveOpen) ToDispose.AddRange(sources);

            BucketTree = new BucketTree<RelocationEntry>(bucketTreeHeader, bucketTreeData);

            RelocationEntries = BucketTree.GetEntryList();
            RelocationOffsets = RelocationEntries.Select(x => x.Offset).ToList();

            Length = BucketTree.BucketOffsets.OffsetEnd;
        }

        protected override int ReadImpl(Span<byte> destination, long offset)
        {
            RelocationEntry entry = GetRelocationEntry(offset);

            long inPos = offset;
            int outPos = 0;
            int remaining = destination.Length;

            while (remaining > 0)
            {
                long entryPos = inPos - entry.Offset;

                int bytesToRead = (int)Math.Min(entry.OffsetEnd - inPos, remaining);
                int bytesRead = Sources[entry.SourceIndex].Read(destination.Slice(outPos, bytesToRead), entry.SourceOffset + entryPos);

                outPos += bytesRead;
                inPos += bytesRead;
                remaining -= bytesRead;

                if (inPos >= entry.OffsetEnd)
                {
                    entry = entry.Next;
                }
            }

            return outPos;
        }

        protected override void WriteImpl(ReadOnlySpan<byte> source, long offset)
        {
            throw new NotImplementedException();
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override bool CanWrite => false;
        
        public override long Length { get; }

        private RelocationEntry GetRelocationEntry(long offset)
        {
            int index = RelocationOffsets.BinarySearch(offset);
            if (index < 0) index = ~index - 1;
            return RelocationEntries[index];
        }
    }
}
