using System;

namespace LibHac.IO
{
    public class SectorStorage : StorageBase
    {
        protected IStorage BaseStorage { get; }

        public int SectorSize { get; }
        public int SectorCount { get; private set; }

        private long _length;

        public SectorStorage(IStorage baseStorage, int sectorSize, bool leaveOpen)
        {
            BaseStorage = baseStorage;
            SectorSize = sectorSize;
            SectorCount = (int)Util.DivideByRoundUp(BaseStorage.GetSize(), SectorSize);
            _length = BaseStorage.GetSize();

            if (!leaveOpen) ToDispose.Add(BaseStorage);
        }

        protected override void ReadImpl(Span<byte> destination, long offset)
        {
            ValidateSize(destination.Length, offset);
            BaseStorage.Read(destination, offset);
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

        public override long GetSize() => _length;

        public override void SetSize(long size)
        {
            BaseStorage.SetSize(size);

            SectorCount = (int)Util.DivideByRoundUp(BaseStorage.GetSize(), SectorSize);
            _length = BaseStorage.GetSize();
        }

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
