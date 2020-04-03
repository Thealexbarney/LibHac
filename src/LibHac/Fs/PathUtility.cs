using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using LibHac.Common;
using static LibHac.Fs.PathTool;

namespace LibHac.Fs
{
    public static class PathUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWindowsDrive(U8Span path)
        {
            return (uint)path.Length > 1 &&
                   (IsDriveSeparator(path[1]) &&
                    IsWindowsDriveCharacter(path[0]));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWindowsDriveCharacter(byte c)
        {
            return (0b1101_1111 & c) - 'A' <= 'Z' - 'A';
            //return 'a' <= c && c <= 'z' || 'A' <= c && c <= 'Z';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsUnc(U8Span path)
        {
            return (uint)path.Length > 1 &&
                   (IsSeparator(path.GetUnsafe(0)) && IsSeparator(path.GetUnsafe(1)) ||
                    IsAltSeparator(path.GetUnsafe(0)) && IsAltSeparator(path.GetUnsafe(1)));
        }

        public static int GetWindowsPathSkipLength(U8Span path)
        {
            if (IsWindowsDrive(path))
                return 2;

            if (!IsUnc(path))
                return 0;

            for (int i = 2; i < path.Length && !IsNullTerminator(path[i]); i++)
            {
                byte c = path[i];
                if (c == (byte)'$' || IsDriveSeparator(c))
                {
                    return i + 1;
                }
            }

            return 0;
        }

        public static Result VerifyPath(U8Span path, int maxPathLength, int maxNameLength)
        {
            Debug.Assert(!path.IsNull());

            int nameLength = 0;

            for (int i = 0; i < path.Length && i <= maxPathLength && nameLength <= maxNameLength; i++)
            {
                byte c = path[i];

                if (IsNullTerminator(c))
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

        public static void Replace(Span<byte> buffer, byte oldChar, byte newChar)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] == oldChar)
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
                StringTraits.AltDirectorySeparator,
                StringTraits.DirectorySeparator);

            return Result.Success;
        }
    }
}
