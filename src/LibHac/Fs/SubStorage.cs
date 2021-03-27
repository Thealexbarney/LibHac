using System;
using LibHac.Common;
using LibHac.Diag;

namespace LibHac.Fs
{
    /// <summary>
    /// Presents a subsection of a base IStorage as a new IStorage.
    /// </summary>
    /// <remarks>
    /// A SubStorage presents a sub-range of an IStorage as a separate IStorage.
    ///
    /// The SubStorage doesn't check if the offset and size provided are actually in the base storage.
    /// GetSize will return the size given to the SubStorage at initialization and will not query
    /// the base storage's size.
    ///
    /// A SubStorage is non-resizable by default. <see cref="SetResizable"/> may be used to mark
    /// the SubStorage as resizable. The SubStorage may only be resized if the end of the SubStorage
    /// is located at the end of the base storage. When resizing the SubStorage, the base storage
    /// will be resized to the appropriate length. 
    /// </remarks>
    public class SubStorage : IStorage
    {
        private ReferenceCountedDisposable<IStorage> SharedBaseStorage { get; set; }
        protected IStorage BaseStorage { get; private set; }
        private long Offset { get; set; }
        private long Size { get; set; }
        private bool IsResizable { get; set; }

        /// <summary>
        /// Creates an uninitialized <see cref="SubStorage"/>. It must be initialized with <see cref="InitializeFrom"/> before using.
        /// </summary>
        public SubStorage()
        {
            BaseStorage = null;
            Offset = 0;
            Size = 0;
            IsResizable = false;
        }

        /// <summary>
        /// Creates a copy of <paramref name="other"/>.
        /// <paramref name="other"/> will not be disposed when the created <see cref="SubStorage"/> is disposed.
        /// </summary>
        /// <param name="other">The <see cref="SubStorage"/> to create a copy of. Caller retains ownership.</param>
        public SubStorage(SubStorage other)
        {
            BaseStorage = other.BaseStorage;
            Offset = other.Offset;
            Size = other.Size;
            IsResizable = other.IsResizable;
        }

        /// <summary>
        /// Initializes or reinitializes this <see cref="SubStorage"/> as a copy of <paramref name="other"/>.
        /// Any shared references in <paramref name="other"/> will be copied.
        /// </summary>
        /// <param name="other">The <see cref="SubStorage"/> used to initialize this one.</param>
        public void InitializeFrom(SubStorage other)
        {
            if (this != other)
            {
                SharedBaseStorage = other.SharedBaseStorage.AddReference();
                BaseStorage = other.BaseStorage;
                Offset = other.Offset;
                Size = other.Size;
                IsResizable = other.IsResizable;
            }
        }

        /// <summary>
        /// Creates a <see cref="SubStorage"/> from a subsection of another <see cref="IStorage"/>.
        /// <paramref name="baseStorage"/> will not be disposed when the created <see cref="SubStorage"/> is disposed.
        /// </summary>
        /// <param name="baseStorage">The base <see cref="IStorage"/>. Caller retains ownership.</param>
        /// <param name="offset">The offset in the base storage at which to begin the created SubStorage.</param>
        /// <param name="size">The size of the SubStorage.</param>
        public SubStorage(IStorage baseStorage, long offset, long size)
        {
            BaseStorage = baseStorage;
            Offset = offset;
            Size = size;
            IsResizable = false;

            Assert.SdkRequiresNotNull(baseStorage);
            Assert.SdkRequiresLessEqual(0, Offset);
            Assert.SdkRequiresLessEqual(0, Size);
        }

        /// <summary>
        /// Creates a <see cref="SubStorage"/> from a subsection of another <see cref="IStorage"/>.
        /// Holds a reference to <paramref name="sharedBaseStorage"/> until disposed.
        /// </summary>
        /// <param name="sharedBaseStorage">The base IStorage.</param>
        /// <param name="offset">The offset in the base storage at which to begin the created SubStorage.</param>
        /// <param name="size">The size of the SubStorage.</param>
        public SubStorage(ReferenceCountedDisposable<IStorage> sharedBaseStorage, long offset, long size)
        {
            SharedBaseStorage = sharedBaseStorage.AddReference();
            BaseStorage = SharedBaseStorage.Target;
            Offset = offset;
            Size = size;
            IsResizable = false;

            Assert.SdkRequiresNotNull(sharedBaseStorage);
            Assert.SdkRequiresLessEqual(0, Offset);
            Assert.SdkRequiresLessEqual(0, Size);
        }

        /// <summary>
        /// Creates a <see cref="SubStorage"/> from a subsection of another <see cref="SubStorage"/>.
        /// <paramref name="other"/> will not be disposed when the created <see cref="SubStorage"/> is disposed.
        /// </summary>
        /// <remarks>
        /// The created SubStorage will directly use the base SubStorage of <paramref name="other"/> and will
        /// adjust the <paramref name="offset"/> and <paramref name="size"/> accordingly.
        /// This avoids the overhead of going through two SubStorage layers.
        /// </remarks>
        /// <param name="other">The base SubStorage.</param>
        /// <param name="offset">The offset in the base storage at which to begin the created SubStorage.</param>
        /// <param name="size">The size of the SubStorage.</param>
        public SubStorage(SubStorage other, long offset, long size)
        {
            BaseStorage = other.BaseStorage;
            Offset = other.Offset + offset;
            Size = size;
            IsResizable = false;

            Assert.SdkRequires(other.IsValid());
            Assert.SdkRequiresLessEqual(0, Offset);
            Assert.SdkRequiresLessEqual(0, Size);
            Assert.SdkRequiresGreaterEqual(other.Size, offset + size);
        }

        private bool IsValid() => BaseStorage != null;

        /// <summary>
        /// Sets whether the <see cref="SubStorage"/> is resizable or not.
        /// </summary>
        /// <param name="isResizable"><see langword="true"/> if the <see cref="SubStorage"/> should
        /// be resizable. <see langword="false"/> if not.</param>
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
            if (!IsResizable) return ResultFs.UnsupportedSetSizeForNotResizableSubStorage.Log();
            if (!IsOffsetAndSizeValid(Offset, size)) return ResultFs.InvalidSize.Log();

            Result rc = BaseStorage.GetSize(out long currentSize);
            if (rc.IsFailure()) return rc;

            if (currentSize != Offset + Size)
            {
                // SubStorage cannot be resized unless it is located at the end of the base storage.
                return ResultFs.UnsupportedSetSizeForResizableSubStorage.Log();
            }

            rc = BaseStorage.SetSize(Offset + size);
            if (rc.IsFailure()) return rc;

            Size = size;
            return Result.Success;
        }

        protected override Result DoGetSize(out long size)
        {
            UnsafeHelpers.SkipParamInit(out size);

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
