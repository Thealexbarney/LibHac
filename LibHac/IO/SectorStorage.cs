using System;

namespace LibHac.IO
{
    public class SectorStorage : Storage
    {
        protected Storage BaseStorage { get; }

        public int SectorSize { get; }
        public int SectorCount { get; }

        public SectorStorage(Storage baseStorage, int sectorSize, bool keepOpen)
        {
            BaseStorage = baseStorage;
            SectorSize = sectorSize;
            SectorCount = (int)Util.DivideByRoundUp(BaseStorage.Length, sectorSize);
            Length = baseStorage.Length;
            if (!keepOpen) ToDispose.Add(BaseStorage);
        }

        protected override int ReadImpl(Span<byte> destination, long offset)
        {
            ValidateSize(destination.Length, offset);
            return BaseStorage.Read(destination, offset);
        }

        protected override void WriteImpl(ReadOnlySpan<byte> source, long offset)
        {
            ValidateSize(source.Length, offset);
            BaseStorage.Write(source, offset);
        }

        public override void Flush()
        {
            BaseStorage.Flush();
        }

        public override long Length { get; }

        /// <summary>
        /// Validates that the size is a multiple of the sector size
        /// </summary>
        protected void ValidateSize(long size, long offset)
        {
            if (size < 0)
                throw new ArgumentException("Size must be non-negative");
            if (offset < 0)
                throw new ArgumentException("Offset must be non-negative");
            if (offset % SectorSize != 0)
                throw new ArgumentException($"Offset must be a multiple of {SectorSize}");
        }
    }
}
