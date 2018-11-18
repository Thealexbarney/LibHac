using System;
using System.Collections.Generic;
using System.IO;

namespace LibHac.IO
{
    public class ConcatenationStorage : Storage
    {
        private ConcatSource[] Sources { get; }
        public override long Length { get; }

        public ConcatenationStorage(IList<Storage> sources, bool leaveOpen)
        {
            Sources = new ConcatSource[sources.Count];
            if (!leaveOpen) ToDispose.AddRange(sources);

            long length = 0;
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i].Length < 0) throw new ArgumentException("Sources must have an explicit length.");
                Sources[i] = new ConcatSource(sources[i], length, sources[i].Length);
                length += sources[i].Length;
            }

            Length = length;
        }

        protected override void ReadImpl(Span<byte> destination, long offset)
        {
            long inPos = offset;
            int outPos = 0;
            int remaining = destination.Length;

            while (remaining > 0)
            {
                ConcatSource entry = FindSource(inPos);
                long sourcePos = inPos - entry.StartOffset;

                int bytesToRead = (int)Math.Min(entry.EndOffset - inPos, remaining);
                entry.Storage.Read(destination.Slice(outPos, bytesToRead), sourcePos);

                outPos += bytesToRead;
                inPos += bytesToRead;
                remaining -= bytesToRead;
            }
        }

        protected override void WriteImpl(ReadOnlySpan<byte> source, long offset)
        {
            long inPos = offset;
            int outPos = 0;
            int remaining = source.Length;

            while (remaining > 0)
            {
                ConcatSource storage = FindSource(inPos);
                long sourcePos = inPos - storage.StartOffset;

                int bytesToWrite = (int)Math.Min(storage.EndOffset - inPos, remaining);
                storage.Storage.Write(source.Slice(outPos, bytesToWrite), sourcePos);

                outPos += bytesToWrite;
                inPos += bytesToWrite;
                remaining -= bytesToWrite;
            }
        }

        public override void Flush()
        {
            foreach (ConcatSource source in Sources)
            {
                source.Storage.Flush();
            }
        }

        public override Storage Slice(long start, long length, bool leaveOpen, FileAccess access)
        {
            ConcatSource startSource = FindSource(start);
            ConcatSource endSource = FindSource(start + length - 1);

            if (startSource != endSource)
            {
                return base.Slice(start, length, leaveOpen, access);
            }

            Storage storage = startSource.Storage.Slice(start - startSource.StartOffset, length, true, access);
            if (!leaveOpen) storage.ToDispose.Add(this);

            return storage;
        }

        private ConcatSource FindSource(long offset)
        {
            foreach (ConcatSource info in Sources)
            {
                if (info.EndOffset > offset) return info;
            }

            throw new ArgumentOutOfRangeException(nameof(offset), offset, "The Storage does not contain this offset.");
        }

        private class ConcatSource
        {
            public Storage Storage { get; }
            public long StartOffset { get; }
            public long EndOffset { get; }

            public ConcatSource(Storage storage, long startOffset, long length)
            {
                Storage = storage;
                StartOffset = startOffset;
                EndOffset = startOffset + length;
            }
        }
    }
}
