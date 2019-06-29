using System;

namespace LibHac.Fs.RomFs
{
    public class RomFsFile : FileBase
    {
        private IStorage BaseStorage { get; }
        private long Offset { get; }
        private long Size { get; }

        public RomFsFile(IStorage baseStorage, long offset, long size)
        {
            Mode = OpenMode.Read;
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
            ThrowHelper.ThrowResult(ResultFs.UnsupportedOperationModifyRomFsFile);
        }

        public override void Flush()
        {
        }

        public override long GetSize()
        {
            return Size;
        }

        public override void SetSize(long size)
        {
            ThrowHelper.ThrowResult(ResultFs.UnsupportedOperationModifyRomFsFile);
        }
    }
}
