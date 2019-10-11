using System;
using System.IO;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class SubStorage : StorageBase
    {
        private IStorage BaseStorage { get; }
        private long Offset { get; }
        private FileAccess Access { get; } = FileAccess.ReadWrite;
        private long _length;

        public SubStorage(IStorage baseStorage, long offset, long length)
        {
            BaseStorage = baseStorage;
            Offset = offset;
            _length = length;
        }

        public SubStorage(SubStorage baseStorage, long offset, long length)
        {
            BaseStorage = baseStorage.BaseStorage;
            Offset = baseStorage.Offset + offset;
            _length = length;
        }

        public SubStorage(IStorage baseStorage, long offset, long length, bool leaveOpen)
            : this(baseStorage, offset, length)
        {
            if (!leaveOpen) ToDispose.Add(BaseStorage);
        }

        public SubStorage(IStorage baseStorage, long offset, long length, bool leaveOpen, FileAccess access)
            : this(baseStorage, offset, length, leaveOpen)
        {
            Access = access;
        }

        protected override Result ReadImpl(long offset, Span<byte> destination)
        {
            if ((Access & FileAccess.Read) == 0) throw new InvalidOperationException("Storage is not readable");
            return BaseStorage.Read(offset + Offset, destination);
        }

        protected override Result WriteImpl(long offset, ReadOnlySpan<byte> source)
        {
            if ((Access & FileAccess.Write) == 0) throw new InvalidOperationException("Storage is not writable");
            return BaseStorage.Write(offset + Offset, source);
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
            if (BaseStorage == null) return ResultFs.SubStorageNotInitialized.Log();

            // todo: Add IsResizable member
            // if (!IsResizable) return ResultFs.SubStorageNotResizable.Log();

            if (Offset < 0 || size < 0) return ResultFs.InvalidSize.Log();

            Result rc = BaseStorage.GetSize(out long baseSize);
            if (rc.IsFailure()) return rc;

            if (baseSize != Offset + _length)
            {
                // SubStorage cannot be resized unless it is located at the end of the base storage.
                return ResultFs.SubStorageNotResizableMiddleOfFile.Log();
            }

            rc = BaseStorage.SetSize(Offset + size);
            if (rc.IsFailure()) return rc;

            _length = size;

            return Result.Success;
        }
    }
}
