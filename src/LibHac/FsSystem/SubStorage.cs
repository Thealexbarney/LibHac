using System;
using System.IO;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class SubStorage : IStorage
    {
        private IStorage BaseStorage { get; }
        private long Offset { get; }
        private FileAccess Access { get; } = FileAccess.ReadWrite;
        private long Length { get; set; }
        private bool LeaveOpen { get; }

        public SubStorage(IStorage baseStorage, long offset, long length)
        {
            BaseStorage = baseStorage;
            Offset = offset;
            Length = length;
        }

        public SubStorage(SubStorage baseStorage, long offset, long length)
        {
            BaseStorage = baseStorage.BaseStorage;
            Offset = baseStorage.Offset + offset;
            Length = length;
        }

        public SubStorage(IStorage baseStorage, long offset, long length, bool leaveOpen)
            : this(baseStorage, offset, length)
        {
            LeaveOpen = leaveOpen;
        }

        public SubStorage(IStorage baseStorage, long offset, long length, bool leaveOpen, FileAccess access)
            : this(baseStorage, offset, length, leaveOpen)
        {
            Access = access;
        }

        protected override Result DoRead(long offset, Span<byte> destination)
        {
            if ((Access & FileAccess.Read) == 0) throw new InvalidOperationException("Storage is not readable");
            return BaseStorage.Read(offset + Offset, destination);
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
        {
            if ((Access & FileAccess.Write) == 0) throw new InvalidOperationException("Storage is not writable");
            return BaseStorage.Write(offset + Offset, source);
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
            if (BaseStorage == null) return ResultFs.NotInitialized.Log();

            // todo: Add IsResizable member
            // if (!IsResizable) return ResultFs.SubStorageNotResizable.Log();

            if (Offset < 0 || size < 0) return ResultFs.InvalidSize.Log();

            Result rc = BaseStorage.GetSize(out long baseSize);
            if (rc.IsFailure()) return rc;

            if (baseSize != Offset + Length)
            {
                // SubStorage cannot be resized unless it is located at the end of the base storage.
                return ResultFs.UnsupportedOperationInResizableSubStorageSetSize.Log();
            }

            rc = BaseStorage.SetSize(Offset + size);
            if (rc.IsFailure()) return rc;

            Length = size;

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
    }
}
