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

        protected override int ReadSpan(Span<byte> destination, long offset)
        {
            ValidateSize(destination.Length);
            return BaseStorage.Read(destination, offset);
        }

        protected override void WriteSpan(ReadOnlySpan<byte> source, long offset)
        {
            ValidateSize(source.Length);
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
        protected void ValidateSize(long value)
        {
            if (value < 0)
                throw new ArgumentException("Value must be non-negative");
            if (value % SectorSize != 0)
                throw new ArgumentException($"Value must be a multiple of {SectorSize}");
        }
    }
}
