using System;
using System.Collections.Generic;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class ConcatenationStorage : IStorage
    {
        private ConcatSource[] Sources { get; }
        private long Length { get; }
        private bool LeaveOpen { get; }

        public ConcatenationStorage(IList<IStorage> sources, bool leaveOpen)
        {
            Sources = new ConcatSource[sources.Count];
            LeaveOpen = leaveOpen;

            long length = 0;
            for (int i = 0; i < sources.Count; i++)
            {
                sources[i].GetSize(out long sourceSize).ThrowIfFailure();

                if (sourceSize < 0) throw new ArgumentException("Sources must have an explicit length.");
                Sources[i] = new ConcatSource(sources[i], length, sourceSize);
                length += sourceSize;
            }

            Length = length;
        }

        protected override Result DoRead(long offset, Span<byte> destination)
        {
            long inPos = offset;
            int outPos = 0;
            int remaining = destination.Length;

            if (!IsRangeValid(offset, destination.Length, Length))
                return ResultFs.OutOfRange.Log();

            int sourceIndex = FindSource(inPos);

            while (remaining > 0)
            {
                ConcatSource entry = Sources[sourceIndex];
                long entryPos = inPos - entry.StartOffset;
                long entryRemain = entry.StartOffset + entry.Size - inPos;

                int bytesToRead = (int)Math.Min(entryRemain, remaining);

                Result rc = entry.Storage.Read(entryPos, destination.Slice(outPos, bytesToRead));
                if (rc.IsFailure()) return rc;

                outPos += bytesToRead;
                inPos += bytesToRead;
                remaining -= bytesToRead;
                sourceIndex++;
            }

            return Result.Success;
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
        {
            long inPos = offset;
            int outPos = 0;
            int remaining = source.Length;

            if (!IsRangeValid(offset, source.Length, Length))
                return ResultFs.OutOfRange.Log();

            int sourceIndex = FindSource(inPos);

            while (remaining > 0)
            {
                ConcatSource entry = Sources[sourceIndex];
                long entryPos = inPos - entry.StartOffset;
                long entryRemain = entry.StartOffset + entry.Size - inPos;

                int bytesToWrite = (int)Math.Min(entryRemain, remaining);

                Result rc = entry.Storage.Write(entryPos, source.Slice(outPos, bytesToWrite));
                if (rc.IsFailure()) return rc;

                outPos += bytesToWrite;
                inPos += bytesToWrite;
                remaining -= bytesToWrite;
                sourceIndex++;
            }

            return Result.Success;
        }

        protected override Result DoFlush()
        {
            foreach (ConcatSource source in Sources)
            {
                Result rc = source.Storage.Flush();
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
        }

        protected override Result DoSetSize(long size)
        {
            return ResultFs.NotImplemented.Log();
        }

        protected override Result DoGetSize(out long size)
        {
            size = Length;
            return Result.Success;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!LeaveOpen && Sources != null)
                {
                    foreach (ConcatSource source in Sources)
                    {
                        source?.Storage?.Dispose();
                    }
                }
            }
        }

        private int FindSource(long offset)
        {
            if (offset < 0 || offset >= Length)
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
