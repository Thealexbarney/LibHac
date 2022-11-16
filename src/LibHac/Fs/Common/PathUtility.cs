using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Util;
using static LibHac.Fs.StringTraits;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs;

/// <summary>
/// Contains various utility functions for working with paths.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
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

    public static Result ConvertToFspPath(out FspPath fspPath, ReadOnlySpan<byte> path)
    {
        UnsafeHelpers.SkipParamInit(out fspPath);

        int length = StringUtils.Copy(SpanHelpers.AsByteSpan(ref fspPath), path, PathTool.EntryNameLengthMax + 1);

        if (length >= PathTool.EntryNameLengthMax + 1)
            return ResultFs.TooLongPath.Log();

        Result res = PathFormatter.SkipMountName(out ReadOnlySpan<byte> pathWithoutMountName, out int skipLength,
            new U8Span(path));
        if (res.IsFailure()) return res.Miss();

        if (!WindowsPath.IsWindowsPath(pathWithoutMountName, true))
        {
            Replace(SpanHelpers.AsByteSpan(ref fspPath).Slice(0, 0x300), AltDirectorySeparator, DirectorySeparator);
        }
        else
        {
            bool isHostOrNoMountName = skipLength == 0 || StringUtils.Compare(path, CommonMountNames.HostRootFileSystemMountName,
                CommonMountNames.HostRootFileSystemMountName.Length) == 0;

            if (isHostOrNoMountName && WindowsPath.IsUncPath(path.Slice(skipLength), true, false))
            {
                SpanHelpers.AsByteSpan(ref fspPath)[skipLength] = AltDirectorySeparator;
                SpanHelpers.AsByteSpan(ref fspPath)[skipLength + 1] = AltDirectorySeparator;
            }
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

        ReadOnlySpan<byte> invalidChars = ":*?<>|"u8;

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

        if (WindowsPath.IsUncPath(lhs) && !WindowsPath.IsUncPath(rhs))
            return false;

        if (!WindowsPath.IsUncPath(lhs) && WindowsPath.IsUncPath(rhs))
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
        if (WindowsPath.IsWindowsPath(path, false))
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