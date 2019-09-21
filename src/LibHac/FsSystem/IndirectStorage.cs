using System;
using System.Collections.Generic;
using System.Linq;
using LibHac.Fs;

namespace LibHac.FsSystem
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

        protected override Result ReadImpl(long offset, Span<byte> destination)
        {
            RelocationEntry entry = GetRelocationEntry(offset);

            if (entry.SourceIndex > Sources.Count)
            {
                return ResultFs.InvalidIndirectStorageSource.Log();
            }

            long inPos = offset;
            int outPos = 0;
            int remaining = destination.Length;

            while (remaining > 0)
            {
                long entryPos = inPos - entry.Offset;

                int bytesToRead = (int)Math.Min(entry.OffsetEnd - inPos, remaining);

                Result rc = Sources[entry.SourceIndex].Read(entry.SourceOffset + entryPos, destination.Slice(outPos, bytesToRead));
                if (rc.IsFailure()) return rc;

                outPos += bytesToRead;
                inPos += bytesToRead;
                remaining -= bytesToRead;

                if (inPos >= entry.OffsetEnd)
                {
                    entry = entry.Next;
                }
            }

            return Result.Success;
        }

        protected override Result WriteImpl(long offset, ReadOnlySpan<byte> source)
        {
            return ResultFs.UnsupportedOperationInIndirectStorageSetSize.Log();
        }

        public override Result Flush()
        {
            return Result.Success;
        }

        public override Result GetSize(out long size)
        {
            size = _length;
            return Result.Success;
        }

        public override Result SetSize(long size)
        {
            return ResultFs.UnsupportedOperationInIndirectStorageSetSize.Log();
        }

        private RelocationEntry GetRelocationEntry(long offset)
        {
            int index = RelocationOffsets.BinarySearch(offset);
            if (index < 0) index = ~index - 1;
            return RelocationEntries[index];
        }
    }
}
