﻿using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.FsSrv.Sf;
using LibHac.Util;
using static LibHac.Fs.StringTraits;

namespace LibHac.Fs.Common
{
    public static class PathUtility12
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

        public static bool IsCurrentDirectory(ReadOnlySpan<byte> path)
        {
            if (path.Length < 1)
                return false;

            return path[0] == Dot &&
                   (path.Length < 2 || path[1] == NullTerminator || path[1] == DirectorySeparator);
        }

        public static bool IsParentDirectory(ReadOnlySpan<byte> path)
        {
            if (path.Length < 2)
                return false;

            return path[0] == Dot &&
                   path[1] == Dot &&
                   (path.Length < 3 || path[2] == NullTerminator || path[2] == DirectorySeparator);
        }

        public static bool IsSeparator(byte c)
        {
            return c == DirectorySeparator;
        }

        public static bool IsNul(byte c)
        {
            return c == NullTerminator;
        }

        public static Result ConvertToFspPath(out FspPath fspPath, ReadOnlySpan<byte> path)
        {
            UnsafeHelpers.SkipParamInit(out fspPath);

            int length = StringUtils.Copy(SpanHelpers.AsByteSpan(ref fspPath), path, PathTool.EntryNameLengthMax + 1);

            if (length >= PathTool.EntryNameLengthMax + 1)
                return ResultFs.TooLongPath.Log();

            Result rc = PathFormatter.SkipMountName(out ReadOnlySpan<byte> pathWithoutMountName, out _,
                new U8Span(path));
            if (rc.IsFailure()) return rc;

            if (!WindowsPath12.IsWindowsPath(pathWithoutMountName, true))
            {
                Replace(SpanHelpers.AsByteSpan(ref fspPath).Slice(0, 0x300), AltDirectorySeparator, DirectorySeparator);
            }
            else if (fspPath.Str[0] == DirectorySeparator && fspPath.Str[1] == DirectorySeparator)
            {
                SpanHelpers.AsByteSpan(ref fspPath)[0] = AltDirectorySeparator;
                SpanHelpers.AsByteSpan(ref fspPath)[1] = AltDirectorySeparator;
            }

            return Result.Success;
        }

        public static bool IsDirectoryPath(ReadOnlySpan<byte> path)
        {
            if (path.Length < 1 || path[0] == NullTerminator)
                return false;

            int length = StringUtils.GetLength(path);
            return path[length - 1] == DirectorySeparator || path[length - 1] == AltDirectorySeparator;
        }

        public static bool IsDirectoryPath(in FspPath path)
        {
            return IsDirectoryPath(SpanHelpers.AsReadOnlyByteSpan(in path));
        }

        public static Result CheckUtf8(ReadOnlySpan<byte> path)
        {
            Assert.SdkRequiresNotNull(path);

            uint utf8Buffer = 0;
            Span<byte> utf8BufferSpan = SpanHelpers.AsByteSpan(ref utf8Buffer);

            ReadOnlySpan<byte> currentChar = path;

            while (currentChar.Length > 0 && currentChar[0] != NullTerminator)
            {
                utf8BufferSpan.Clear();

                CharacterEncodingResult result =
                    CharacterEncoding.PickOutCharacterFromUtf8String(utf8BufferSpan, ref currentChar);

                if (result != CharacterEncodingResult.Success)
                    return ResultFs.InvalidPathFormat.Log();

                result = CharacterEncoding.ConvertCharacterUtf8ToUtf32(out _, utf8BufferSpan);

                if (result != CharacterEncodingResult.Success)
                    return ResultFs.InvalidPathFormat.Log();
            }

            return Result.Success;
        }

        public static Result CheckInvalidCharacter(byte c)
        {
            /*
            The optimized code is equivalent to this:

            ReadOnlySpan<byte> invalidChars = new[]
                {(byte) ':', (byte) '*', (byte) '?', (byte) '<', (byte) '>', (byte) '|'};

            for (int i = 0; i < invalidChars.Length; i++)
            {
                if (c == invalidChars[i])
                    return ResultFs.InvalidCharacter.Log();
            }

            return Result.Success;
            */

            const ulong mask = (1ul << (byte)':') |
                               (1ul << (byte)'*') |
                               (1ul << (byte)'?') |
                               (1ul << (byte)'<') |
                               (1ul << (byte)'>');

            if (c <= 0x3Fu && ((1ul << c) & mask) != 0 || c == (byte)'|')
                return ResultFs.InvalidCharacter.Log();

            return Result.Success;
        }

        public static Result CheckInvalidBackslash(out bool containsBackslash, ReadOnlySpan<byte> path, bool allowBackslash)
        {
            containsBackslash = false;

            for (int i = 0; i < path.Length && path[i] != NullTerminator; i++)
            {
                if (path[i] == '\\')
                {
                    containsBackslash = true;

                    if (!allowBackslash)
                        return ResultFs.InvalidCharacter.Log();
                }
            }

            return Result.Success;
        }

        public static Result CheckEntryNameBytes(ReadOnlySpan<byte> path, int maxEntryLength)
        {
            Assert.SdkRequiresNotNull(path);

            int currentEntryLength = 0;

            for (int i = 0; i < path.Length && path[i] != NullTerminator; i++)
            {
                currentEntryLength++;

                if (path[i] == DirectorySeparator || path[i] == AltDirectorySeparator)
                    currentEntryLength = 0;

                // Note: The original does use >= instead of >
                if (currentEntryLength >= maxEntryLength)
                    return ResultFs.TooLongPath.Log();
            }

            return Result.Success;
        }

        public static bool IsSubPath(ReadOnlySpan<byte> lhs, ReadOnlySpan<byte> rhs)
        {
            Assert.SdkRequiresNotNull(lhs);
            Assert.SdkRequiresNotNull(rhs);

            if (WindowsPath12.IsUncPath(lhs) && !WindowsPath12.IsUncPath(rhs))
                return false;

            if (!WindowsPath12.IsUncPath(lhs) && WindowsPath12.IsUncPath(rhs))
                return false;

            if (lhs.At(0) == DirectorySeparator && lhs.At(1) == NullTerminator &&
                rhs.At(0) == DirectorySeparator && rhs.At(1) != NullTerminator)
                return true;

            if (rhs.At(0) == DirectorySeparator && rhs.At(1) == NullTerminator &&
                lhs.At(0) == DirectorySeparator && lhs.At(1) != NullTerminator)
                return true;

            for (int i = 0; ; i++)
            {
                if (lhs.At(i) == NullTerminator)
                {
                    return rhs.At(i) == DirectorySeparator;
                }
                else if (rhs.At(i) == NullTerminator)
                {
                    return lhs.At(i) == DirectorySeparator;
                }
                else if (lhs.At(i) != rhs.At(i))
                {
                    return false;
                }
            }
        }

        public static bool IsPathAbsolute(ReadOnlySpan<byte> path)
        {
            if (WindowsPath12.IsWindowsPath(path, false))
                return true;

            return path.At(0) == DirectorySeparator;
        }

        public static bool IsPathRelative(ReadOnlySpan<byte> path)
        {
            return path.At(0) != NullTerminator && !IsPathAbsolute(path);
        }

        public static bool IsPathStartWithCurrentDirectory(ReadOnlySpan<byte> path)
        {
            return IsCurrentDirectory(path) || IsParentDirectory(path);
        }
    }
}
