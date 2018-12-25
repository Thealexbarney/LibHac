using System;

namespace LibHac.IO
{
    public class RomFsFile : IFile
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

        public void Read(Span<byte> destination, long offset)
        {
            long storageOffset = Offset + offset;

            BaseStorage.Read(destination, storageOffset);
        }

        public void Write(ReadOnlySpan<byte> source, long offset)
        {
            throw new NotImplementedException();
        }

        public void Flush()
        {
            throw new NotImplementedException();
        }

        public long GetSize()
        {
            return Size;
        }

        public long SetSize()
        {
            throw new NotImplementedException();
        }
    }
}
