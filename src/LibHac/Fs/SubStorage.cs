using System;
using LibHac.Diag;

namespace LibHac.Fs
{
    public class SubStorage : IStorage
    {
        private ReferenceCountedDisposable<IStorage> SharedBaseStorage { get; set; }
        protected IStorage BaseStorage { get; private set; }
        private long Offset { get; set; }
        private long Size { get; set; }
        private bool IsResizable { get; set; }

        public SubStorage()
        {
            BaseStorage = null;
            Offset = 0;
            Size = 0;
            IsResizable = false;
        }

        public SubStorage(SubStorage other)
        {
            BaseStorage = other.BaseStorage;
            Offset = other.Offset;
            Size = other.Size;
            IsResizable = other.IsResizable;
        }

        public void InitializeFrom(SubStorage other)
        {
            if (this != other)
            {
                BaseStorage = other.BaseStorage;
                Offset = other.Offset;
                Size = other.Size;
                IsResizable = other.IsResizable;
            }
        }

        public SubStorage(IStorage baseStorage, long offset, long size)
        {
            BaseStorage = baseStorage;
            Offset = offset;
            Size = size;
            IsResizable = false;

            Assert.AssertTrue(IsValid());
            Assert.AssertTrue(Offset >= 0);
            Assert.AssertTrue(Size >= 0);
        }

        public SubStorage(ReferenceCountedDisposable<IStorage> sharedBaseStorage, long offset, long size)
        {
            SharedBaseStorage = sharedBaseStorage.AddReference();
            BaseStorage = SharedBaseStorage.Target;
            Offset = offset;
            Size = size;
            IsResizable = false;

            Assert.AssertTrue(IsValid());
            Assert.AssertTrue(Offset >= 0);
            Assert.AssertTrue(Size >= 0);
        }

        public SubStorage(SubStorage subStorage, long offset, long size)
        {
            BaseStorage = subStorage.BaseStorage;
            Offset = subStorage.Offset + offset;
            Size = size;
            IsResizable = false;

            Assert.AssertTrue(IsValid());
            Assert.AssertTrue(Offset >= 0);
            Assert.AssertTrue(Size >= 0);
            Assert.AssertTrue(subStorage.Size >= offset + size);
        }

        private bool IsValid() => BaseStorage != null;

        public void SetResizable(bool isResizable)
        {
            IsResizable = isResizable;
        }

        protected override Result DoRead(long offset, Span<byte> destination)
        {
            if (!IsValid()) return ResultFs.NotInitialized.Log();
            if (destination.Length == 0) return Result.Success;

            if (!IsRangeValid(offset, destination.Length, Size)) return ResultFs.OutOfRange.Log();

            return BaseStorage.Read(Offset + offset, destination);
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
        {
            if (!IsValid()) return ResultFs.NotInitialized.Log();
            if (source.Length == 0) return Result.Success;

            if (!IsRangeValid(offset, source.Length, Size)) return ResultFs.OutOfRange.Log();

            return BaseStorage.Write(Offset + offset, source);
        }

        protected override Result DoFlush()
        {
            if (!IsValid()) return ResultFs.NotInitialized.Log();

            return BaseStorage.Flush();
        }

        protected override Result DoSetSize(long size)
        {
            if (!IsValid()) return ResultFs.NotInitialized.Log();
            if (!IsResizable) return ResultFs.UnsupportedOperationInSubStorageSetSize.Log();
            if (!IsOffsetAndSizeValid(Offset, size)) return ResultFs.InvalidSize.Log();

            Result rc = BaseStorage.GetSize(out long currentSize);
            if (rc.IsFailure()) return rc;

            if (currentSize != Offset + Size)
            {
                // SubStorage cannot be resized unless it is located at the end of the base storage.
                return ResultFs.UnsupportedOperationInResizableSubStorageSetSize.Log();
            }

            rc = BaseStorage.SetSize(Offset + size);
            if (rc.IsFailure()) return rc;

            Size = size;
            return Result.Success;
        }

        protected override Result DoGetSize(out long size)
        {
            size = default;

            if (!IsValid()) return ResultFs.NotInitialized.Log();

            size = Size;
            return Result.Success;
        }

        protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
        {
            if (!IsValid()) return ResultFs.NotInitialized.Log();

            if (size == 0) return Result.Success;

            if (!IsOffsetAndSizeValid(Offset, size)) return ResultFs.OutOfRange.Log();

            return base.DoOperateRange(outBuffer, operationId, Offset + offset, size, inBuffer);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SharedBaseStorage?.Dispose();
                SharedBaseStorage = null;
            }

            base.Dispose(disposing);
        }
    }
}
