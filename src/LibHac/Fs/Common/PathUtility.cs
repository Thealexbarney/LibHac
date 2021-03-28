using System;
using System.Diagnostics;
using LibHac.Common;
using LibHac.Diag;
using static LibHac.Fs.StringTraits;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs
{
    internal struct PathUtilityGlobals
    {
        public PathVerifier PathVerifier;

        public void Initialize(FileSystemClient _)
        {
            PathVerifier.Initialize();
        }
    }

    internal struct PathVerifier
    {
        public void Initialize()
        {
            // Todo
        }

        public static Result Verify(U8Span path, int maxPathLength, int maxNameLength)
        {
            Debug.Assert(!path.IsNull());

            int nameLength = 0;

            for (int i = 0; i < path.Length && i <= maxPathLength && nameLength <= maxNameLength; i++)
            {
                byte c = path[i];

                if (c == 0)
                    return Result.Success;

                // todo: Compare path based on their Unicode code points

                if (c == ':' || c == '*' || c == '?' || c == '<' || c == '>' || c == '|')
                    return ResultFs.InvalidCharacter.Log();

                nameLength++;
                if (c == '\\' || c == '/')
                {
                    nameLength = 0;
                }
            }

            return ResultFs.TooLongPath.Log();
        }
    }

    public static class PathUtility
    {
        public static void Replace(Span<byte> buffer, byte currentChar, byte newChar)
        {
            Assert.SdkRequiresNotNull(buffer);

            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] == currentChar)
                {
                    buffer[i] = newChar;
                }
            }
        }

        /// <summary>
        /// Performs the extra functions that nn::fs::FspPathPrintf does on the string buffer.
        /// </summary>
        /// <param name="builder">The string builder to process.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        public static Result ToSfPath(in this U8StringBuilder builder)
        {
            if (builder.Overflowed)
                return ResultFs.TooLongPath.Log();

            Replace(builder.Buffer.Slice(builder.Capacity),
                AltDirectorySeparator,
                DirectorySeparator);

            return Result.Success;
        }

        public static Result VerifyPath(this FileSystemClient fs, U8Span path, int maxPathLength, int maxNameLength)
        {
            return PathVerifier.Verify(path, maxPathLength, maxNameLength);
        }

        public static bool IsSubPath(U8Span lhs, U8Span rhs)
        {
            Assert.SdkRequires(!lhs.IsNull());
            Assert.SdkRequires(!rhs.IsNull());

            bool isUncLhs = WindowsPath.IsUnc(lhs);
            bool isUncRhs = WindowsPath.IsUnc(rhs);

            if (isUncLhs && !isUncRhs || !isUncLhs && isUncRhs)
                return false;

            if (lhs.GetOrNull(0) == DirectorySeparator && lhs.GetOrNull(1) == NullTerminator &&
               rhs.GetOrNull(0) == DirectorySeparator && rhs.GetOrNull(1) != NullTerminator)
                return true;

            if (rhs.GetOrNull(0) == DirectorySeparator && rhs.GetOrNull(1) == NullTerminator &&
                lhs.GetOrNull(0) == DirectorySeparator && lhs.GetOrNull(1) != NullTerminator)
                return true;

            for (int i = 0; ; i++)
            {
                if (lhs.GetOrNull(i) == NullTerminator)
                {
                    return rhs.GetOrNull(i) == DirectorySeparator;
                }
                else if (rhs.GetOrNull(i) == NullTerminator)
                {
                    return lhs.GetOrNull(i) == DirectorySeparator;
                }
                else if (lhs.GetOrNull(i) != rhs.GetOrNull(i))
                {
                    return false;
                }
            }
        }
    }
}
