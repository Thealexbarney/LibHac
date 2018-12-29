using System;

namespace LibHac.IO
{
    public class RomFsFile : FileBase
    {
        private IStorage BaseStorage { get; }
        private long Offset { get; }
        private long Size { get; }

        public RomFsFile(IStorage baseStorage, long offset, long size)
        {
            BaseStorage = baseStorage;
            Offset = offset;
            Size = size;
        }

        public override int Read(Span<byte> destination, long offset)
        {
            long storageOffset = Offset + offset;
            int toRead = GetAvailableSizeAndValidate(destination, offset);

            BaseStorage.Read(destination.Slice(0, toRead), storageOffset);

            return toRead;
        }

        public override void Write(ReadOnlySpan<byte> source, long offset)
        {
            throw new NotImplementedException();
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long GetSize()
        {
            return Size;
        }

        public override long SetSize()
        {
            throw new NotSupportedException();
        }
    }
}
