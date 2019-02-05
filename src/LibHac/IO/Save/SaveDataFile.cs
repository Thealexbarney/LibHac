using System;

namespace LibHac.IO.Save
{
    public class SaveDataFile : FileBase
    {
        private AllocationTableStorage BaseStorage { get; }
        private long Offset { get; }
        private long Size { get; }

        public SaveDataFile(AllocationTableStorage baseStorage, long offset, long size, OpenMode mode)
        {
            Mode = mode;
            BaseStorage = baseStorage;
            Offset = offset;
            Size = size;
        }

        public override int Read(Span<byte> destination, long offset)
        {
            int toRead = ValidateReadParamsAndGetSize(destination, offset);

            long storageOffset = Offset + offset;
            BaseStorage.Read(destination.Slice(0, toRead), storageOffset);

            return toRead;
        }

        public override void Write(ReadOnlySpan<byte> source, long offset)
        {
            ValidateWriteParams(source, offset);

            BaseStorage.Write(source, offset);
        }

        public override void Flush()
        {
            BaseStorage.Flush();
        }

        public override long GetSize()
        {
            return Size;
        }

        public override void SetSize(long size)
        {
            throw new NotImplementedException();
        }
    }
}
