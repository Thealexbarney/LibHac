using System;
using System.Collections.Generic;

namespace LibHac.IO
{
    public class ConcatenationStorage : StorageBase
    {
        private ConcatSource[] Sources { get; }
        private long _length;

        public ConcatenationStorage(IList<IStorage> sources, bool leaveOpen)
        {
            Sources = new ConcatSource[sources.Count];
            if (!leaveOpen) ToDispose.AddRange(sources);

            long length = 0;
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i].GetSize() < 0) throw new ArgumentException("Sources must have an explicit length.");
                Sources[i] = new ConcatSource(sources[i], length, sources[i].GetSize());
                length += sources[i].GetSize();
            }

            _length = length;
        }

        protected override void ReadImpl(Span<byte> destination, long offset)
        {
            long inPos = offset;
            int outPos = 0;
            int remaining = destination.Length;
            int sourceIndex = FindSource(inPos);

            while (remaining > 0)
            {
                ConcatSource entry = Sources[sourceIndex];
                long entryPos = inPos - entry.StartOffset;
                long entryRemain = entry.StartOffset + entry.Size - inPos;

                int bytesToRead = (int)Math.Min(entryRemain, remaining);
                entry.Storage.Read(destination.Slice(outPos, bytesToRead), entryPos);

                outPos += bytesToRead;
                inPos += bytesToRead;
                remaining -= bytesToRead;
                sourceIndex++;
            }
        }

        protected override void WriteImpl(ReadOnlySpan<byte> source, long offset)
        {
            long inPos = offset;
            int outPos = 0;
            int remaining = source.Length;
            int sourceIndex = FindSource(inPos);

            while (remaining > 0)
            {
                ConcatSource entry = Sources[sourceIndex];
                long entryPos = inPos - entry.StartOffset;
                long entryRemain = entry.StartOffset + entry.Size - inPos;

                int bytesToWrite = (int)Math.Min(entryRemain, remaining);
                entry.Storage.Write(source.Slice(outPos, bytesToWrite), entryPos);

                outPos += bytesToWrite;
                inPos += bytesToWrite;
                remaining -= bytesToWrite;
                sourceIndex++;
            }
        }

        public override void Flush()
        {
            foreach (ConcatSource source in Sources)
            {
                source.Storage.Flush();
            }
        }

        public override long GetSize() => _length;

        private int FindSource(long offset)
        {
            if (offset < 0 || offset >= _length)
                throw new ArgumentOutOfRangeException(nameof(offset), offset, "The Storage does not contain this offset.");

            int lo = 0;
            int hi = Sources.Length - 1;

            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) >> 1);

                long val = Sources[mid].StartOffset;

                if (val == offset) return mid;

                if (val < offset)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return lo - 1;
        }

        private class ConcatSource
        {
            public IStorage Storage { get; }
            public long StartOffset { get; }
            public long Size { get; }

            public ConcatSource(IStorage storage, long startOffset, long length)
            {
                Storage = storage;
                StartOffset = startOffset;
                Size = length;
            }
        }
    }
}
