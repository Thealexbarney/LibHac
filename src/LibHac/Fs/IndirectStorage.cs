using System;
using System.Collections.Generic;
using System.Linq;

namespace LibHac.Fs
{
    public class IndirectStorage : StorageBase
    {
        private List<RelocationEntry> RelocationEntries { get; }
        private List<long> RelocationOffsets { get; }

        private List<IStorage> Sources { get; } = new List<IStorage>();
        private BucketTree<RelocationEntry> BucketTree { get; }
        private long _length;

        public IndirectStorage(IStorage bucketTreeData, bool leaveOpen, params IStorage[] sources)
        {
            Sources.AddRange(sources);

            if (!leaveOpen) ToDispose.AddRange(sources);

            BucketTree = new BucketTree<RelocationEntry>(bucketTreeData);

            RelocationEntries = BucketTree.GetEntryList();
            RelocationOffsets = RelocationEntries.Select(x => x.Offset).ToList();

            _length = BucketTree.BucketOffsets.OffsetEnd;
        }

        protected override void ReadImpl(Span<byte> destination, long offset)
        {
            RelocationEntry entry = GetRelocationEntry(offset);

            if (entry.SourceIndex > Sources.Count)
            {
                ThrowHelper.ThrowResult(ResultFs.InvalidIndirectStorageSource);
            }

            long inPos = offset;
            int outPos = 0;
            int remaining = destination.Length;

            while (remaining > 0)
            {
                long entryPos = inPos - entry.Offset;

                int bytesToRead = (int)Math.Min(entry.OffsetEnd - inPos, remaining);
                Sources[entry.SourceIndex].Read(destination.Slice(outPos, bytesToRead), entry.SourceOffset + entryPos);

                outPos += bytesToRead;
                inPos += bytesToRead;
                remaining -= bytesToRead;

                if (inPos >= entry.OffsetEnd)
                {
                    entry = entry.Next;
                }
            }
        }

        protected override void WriteImpl(ReadOnlySpan<byte> source, long offset)
        {
            ThrowHelper.ThrowResult(ResultFs.UnsupportedOperationInIndirectStorageWrite);
        }

        public override void Flush() { }

        public override long GetSize() => _length;

        public override void SetSize(long size)
        {
            ThrowHelper.ThrowResult(ResultFs.UnsupportedOperationInIndirectStorageSetSize);
        }

        private RelocationEntry GetRelocationEntry(long offset)
        {
            int index = RelocationOffsets.BinarySearch(offset);
            if (index < 0) index = ~index - 1;
            return RelocationEntries[index];
        }
    }
}
