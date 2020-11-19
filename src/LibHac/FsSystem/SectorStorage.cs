using System;
using LibHac.Fs;
using LibHac.Util;

namespace LibHac.FsSystem
{
    public class SectorStorage : IStorage
    {
        protected IStorage BaseStorage { get; }

        public int SectorSize { get; }
        public int SectorCount { get; private set; }

        private long Length { get; set; }
        private bool LeaveOpen { get; }

        public SectorStorage(IStorage baseStorage, int sectorSize, bool leaveOpen)
        {
            BaseStorage = baseStorage;
            SectorSize = sectorSize;

            baseStorage.GetSize(out long baseSize).ThrowIfFailure();

            SectorCount = (int)BitUtil.DivideUp(baseSize, SectorSize);
            Length = baseSize;

            LeaveOpen = leaveOpen;
        }

        protected override Result DoRead(long offset, Span<byte> destination)
        {
            ValidateSize(destination.Length, offset);
            return BaseStorage.Read(offset, destination);
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
        {
            ValidateSize(source.Length, offset);
            return BaseStorage.Write(offset, source);
        }

        protected override Result DoFlush()
        {
            return BaseStorage.Flush();
        }

        protected override Result DoGetSize(out long size)
        {
            size = Length;
            return Result.Success;
        }

        protected override Result DoSetSize(long size)
        {
            Result rc = BaseStorage.SetSize(size);
            if (rc.IsFailure()) return rc;

            rc = BaseStorage.GetSize(out long newSize);
            if (rc.IsFailure()) return rc;

            SectorCount = (int)BitUtil.DivideUp(newSize, SectorSize);
            Length = newSize;

            return Result.Success;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!LeaveOpen)
                {
                    BaseStorage?.Dispose();
                }
            }
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
