using System;

namespace LibHac.Fs
{
    public class PartitionFile : FileBase
    {
        private IStorage BaseStorage { get; }
        private long Offset { get; }
        private long Size { get; }

        public PartitionFile(IStorage baseStorage, long offset, long size, OpenMode mode)
        {
            Mode = mode;
            BaseStorage = baseStorage;
            Offset = offset;
            Size = size;
        }

        public override int Read(Span<byte> destination, long offset, ReadOption options)
        {
            int toRead = ValidateReadParamsAndGetSize(destination, offset);

            long storageOffset = Offset + offset;
            BaseStorage.Read(destination.Slice(0, toRead), storageOffset);

            return toRead;
        }

        public override void Write(ReadOnlySpan<byte> source, long offset, WriteOption options)
        {
            ValidateWriteParams(source, offset);

            BaseStorage.Write(source, offset);

            if ((options & WriteOption.Flush) != 0)
            {
                BaseStorage.Flush();
            }
        }

        public override void Flush()
        {
            if ((Mode & OpenMode.Write) != 0)
            {
                BaseStorage.Flush();
            }
        }

        public override long GetSize()
        {
            return Size;
        }

        public override void SetSize(long size)
        {
            ThrowHelper.ThrowResult(ResultFs.UnsupportedOperationInPartitionFileSetSize);
        }
    }
}
