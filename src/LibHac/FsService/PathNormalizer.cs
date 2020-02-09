using System;
using LibHac.Common;

namespace LibHac.FsService
{
    public ref struct PathNormalizer
    {
        private U8Span _path;
        private Result _result;

        public PathNormalizer(U8Span path, Option option)
        {
            if (option.HasFlag(Option.AcceptEmpty) && path.IsEmpty())
            {
                _path = path;
                _result = Result.Success;
            }
            else
            {
                bool preserveUnc = option.HasFlag(Option.PreserveUnc);
                bool preserveTailSeparator = option.HasFlag(Option.PreserveTailSeparator);
                bool hasMountName = option.HasFlag(Option.HasMountName);
                _result = Normalize(out _path, path, preserveUnc, preserveTailSeparator, hasMountName);
            }
        }

        private static Result Normalize(out U8Span normalizedPath, U8Span path, bool preserveUnc,
            bool preserveTailSeparator, bool hasMountName)
        {
            normalizedPath = default;
            throw new NotImplementedException();

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
