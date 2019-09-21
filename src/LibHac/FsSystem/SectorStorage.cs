using System;

namespace LibHac.FsSystem
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

            baseStorage.GetSize(out long baseSize).ThrowIfFailure();

            SectorCount = (int)Util.DivideByRoundUp(baseSize, SectorSize);
            _length = baseSize;

            if (!leaveOpen) ToDispose.Add(BaseStorage);
        }

        protected override Result ReadImpl(long offset, Span<byte> destination)
        {
            ValidateSize(destination.Length, offset);
            return BaseStorage.Read(offset, destination);
        }

        protected override Result WriteImpl(long offset, ReadOnlySpan<byte> source)
        {
            ValidateSize(source.Length, offset);
            return BaseStorage.Write(offset, source);
        }

        public override Result Flush()
        {
            return BaseStorage.Flush();
        }

        public override Result GetSize(out long size)
        {
            size = _length;
            return Result.Success;
        }

        public override Result SetSize(long size)
        {
            Result rc = BaseStorage.SetSize(size);
            if (rc.IsFailure()) return rc;

            rc = BaseStorage.GetSize(out long newSize);
            if (rc.IsFailure()) return rc;

            SectorCount = (int)Util.DivideByRoundUp(newSize, SectorSize);
            _length = newSize;

            return Result.Success;
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
