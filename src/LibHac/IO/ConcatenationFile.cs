using System;
using System.Collections.Generic;
using System.Linq;

namespace LibHac.IO
{
    public class ConcatenationFile : FileBase
    {
        private IFile[] Sources { get; }
        private long SplitFileSize { get; }

        public ConcatenationFile(IList<IFile> sources, long splitFileSize, OpenMode mode)
        {
            Sources = sources.ToArray();
            SplitFileSize = splitFileSize;
            Mode = mode;

            for (int i = 0; i < Sources.Length - 1; i++)
            {
                if (Sources[i].GetSize() != SplitFileSize)
                {
                    throw new ArgumentException($"Source file must have size {splitFileSize}");
                }
            }
        }

        public override int Read(Span<byte> destination, long offset)
        {
            long inPos = offset;
            int outPos = 0;
            int remaining = ValidateReadParamsAndGetSize(destination, offset);

            while (remaining > 0)
            {
                int fileIndex = GetFileIndexFromOffset(offset);
                IFile file = Sources[fileIndex];
                long fileOffset = offset - fileIndex * SplitFileSize;

                long fileEndOffset = Math.Min((fileIndex + 1) * SplitFileSize, GetSize());
                int bytesToRead = (int)Math.Min(fileEndOffset - inPos, remaining);
                int bytesRead = file.Read(destination.Slice(outPos, bytesToRead), fileOffset);

                outPos += bytesRead;
                inPos += bytesRead;
                remaining -= bytesRead;

                if (bytesRead < bytesToRead) break;
            }

            return outPos;
        }

        public override void Write(ReadOnlySpan<byte> source, long offset)
        {
            throw new NotImplementedException();
        }

        public override void Flush()
        {
            foreach (IFile file in Sources)
            {
                file.Flush();
            }
        }

        public override long GetSize()
        {
            long size = 0;

            foreach (IFile file in Sources)
            {
                size += file.GetSize();
            }

            return size;
        }

        public override void SetSize(long size)
        {
            throw new NotImplementedException();
        }

        private int GetFileIndexFromOffset(long offset)
        {
            return (int)(offset / SplitFileSize);
        }
    }
}
