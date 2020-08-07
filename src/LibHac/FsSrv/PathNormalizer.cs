using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSystem;

namespace LibHac.FsSrv
{
    public ref struct PathNormalizer
    {
        private readonly U8Span _path;
        public U8Span Path => _path;

        public Result Result { get; }

        public PathNormalizer(U8Span path, Option option)
        {
            if (option.HasFlag(Option.AcceptEmpty) && path.IsEmpty())
            {
                _path = path;
                Result = Result.Success;
            }
            else
            {
                bool preserveUnc = option.HasFlag(Option.PreserveUnc);
                bool preserveTailSeparator = option.HasFlag(Option.PreserveTailSeparator);
                bool hasMountName = option.HasFlag(Option.HasMountName);
                Result = Normalize(out _path, path, preserveUnc, preserveTailSeparator, hasMountName);
            }
        }

        private static Result Normalize(out U8Span normalizedPath, U8Span path, bool preserveUnc,
            bool preserveTailSeparator, bool hasMountName)
        {
            normalizedPath = default;

            Result rc = PathTool.IsNormalized(out bool isNormalized, path, preserveUnc, hasMountName);
            if (rc.IsFailure()) return rc;

            if (isNormalized)
            {
                normalizedPath = path;
            }
            else
            {
                var buffer = new byte[PathTools.MaxPathLength + 1];

                rc = PathTool.Normalize(buffer, out long normalizedLength, path, preserveUnc, hasMountName);
                if (rc.IsFailure()) return rc;

                // GetLength is capped at MaxPathLength bytes to leave room for the null terminator
                if (preserveTailSeparator &&
                    PathTool.IsSeparator(path[StringUtils.GetLength(path, PathTools.MaxPathLength) - 1]))
                {
                    buffer[(int)normalizedLength] = StringTraits.DirectorySeparator;
                    buffer[(int)normalizedLength + 1] = StringTraits.NullTerminator;
                }

                normalizedPath = new U8Span(buffer);
            }

            return Result.Success;
        }

        [Flags]
        public enum Option
        {
            None = 0,
            PreserveUnc = (1 << 0),
            PreserveTailSeparator = (1 << 1),
            HasMountName = (1 << 2),
            AcceptEmpty = (1 << 3),
        }
    }
}
