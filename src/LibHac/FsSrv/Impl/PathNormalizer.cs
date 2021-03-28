using System;
using System.Buffers;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Util;

namespace LibHac.FsSrv.Impl
{
    public ref struct PathNormalizer
    {
        private readonly U8Span _path;
        private byte[] _rentedArray;

        public U8Span Path => _path;

        public Result Result { get; }

        public PathNormalizer(U8Span path, Option option)
        {

            if (option.HasFlag(Option.AcceptEmpty) && path.IsEmpty())
            {
                _path = path;
                _rentedArray = null;
                Result = Result.Success;
            }
            else
            {
                bool preserveUnc = option.HasFlag(Option.PreserveUnc);
                bool preserveTrailingSeparator = option.HasFlag(Option.PreserveTrailingSeparator);
                bool hasMountName = option.HasFlag(Option.HasMountName);
                Result = Normalize(out _path, out _rentedArray, path, preserveUnc, preserveTrailingSeparator,
                    hasMountName);
            }
        }

        public void Dispose()
        {
            if (_rentedArray is not null)
                ArrayPool<byte>.Shared.Return(_rentedArray);
        }

        private static Result Normalize(out U8Span normalizedPath, out byte[] rentedBuffer, U8Span path,
            bool preserveUnc, bool preserveTailSeparator, bool hasMountName)
        {
            Assert.SdkRequiresNotNullOut(out rentedBuffer);

            normalizedPath = default;
            rentedBuffer = null;

            Result rc = Fs.PathNormalizer.IsNormalized(out bool isNormalized, path, preserveUnc, hasMountName);
            if (rc.IsFailure()) return rc;

            if (isNormalized)
            {
                normalizedPath = path;
            }
            else
            {
                byte[] buffer = null;
                try
                {
                    buffer = ArrayPool<byte>.Shared.Rent(PathTool.EntryNameLengthMax + 1);

                    rc = Fs.PathNormalizer.Normalize(buffer.AsSpan(0, PathTool.EntryNameLengthMax + 1),
                        out long normalizedLength, path, preserveUnc, hasMountName);
                    if (rc.IsFailure()) return rc;

                    // Add the tail separator if needed
                    if (preserveTailSeparator)
                    {
                        int pathLength = StringUtils.GetLength(path, PathTool.EntryNameLengthMax);
                        if (Fs.PathNormalizer.IsSeparator(path[pathLength - 1]) &&
                            !Fs.PathNormalizer.IsSeparator(buffer[normalizedLength - 1]))
                        {
                            Assert.SdkLess(normalizedLength, PathTool.EntryNameLengthMax);

                            buffer[(int)normalizedLength] = StringTraits.DirectorySeparator;
                            buffer[(int)normalizedLength + 1] = StringTraits.NullTerminator;
                        }
                    }

                    normalizedPath = new U8Span(Shared.Move(ref buffer));
                }
                finally
                {
                    if (buffer is not null)
                        ArrayPool<byte>.Shared.Return(buffer);
                }
            }

            return Result.Success;
        }

        [Flags]
        public enum Option
        {
            None = 0,
            PreserveUnc = (1 << 0),
            PreserveTrailingSeparator = (1 << 1),
            HasMountName = (1 << 2),
            AcceptEmpty = (1 << 3)
        }
    }
}
