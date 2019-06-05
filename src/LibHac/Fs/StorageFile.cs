using System;

namespace LibHac.Fs
{
    public class StorageFile : FileBase
    {
        private IStorage BaseStorage { get; }

        public StorageFile(IStorage baseStorage, OpenMode mode)
        {
            BaseStorage = baseStorage;
            Mode = mode;
        }

        public override int Read(Span<byte> destination, long offset, ReadOption options)
        {
            int toRead = ValidateReadParamsAndGetSize(destination, offset);

            BaseStorage.Read(destination.Slice(0, toRead), offset);

            return toRead;
        }

        public override void Write(ReadOnlySpan<byte> source, long offset, WriteOption options)
        {
            ValidateWriteParams(source, offset);

            BaseStorage.Write(source, offset);

            if ((options & WriteOption.Flush) != 0)
            {
                Flush();
            }
        }

        public override void Flush()
        {
            BaseStorage.Flush();
        }

        public override long GetSize()
        {
            return BaseStorage.GetSize();
        }

        public override void SetSize(long size)
        {
            BaseStorage.SetSize(size);
        }
    }
}
