using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Util;
using static LibHac.Util.CharacterEncoding;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs;

/// <summary>
/// Contains functions for working with Windows paths.
/// </summary>
/// <remarks>Based on FS 12.1.0 (nnSdk 12.3.1)</remarks>
public static class WindowsPath
{
    private const int WindowsDriveLength = 2;
    private const int UncPathPrefixLength = 2;
    private const int DosDevicePathPrefixLength = 4;

    public static int GetCodePointByteLength(byte firstCodeUnit)
    {
        if ((firstCodeUnit & 0x80) == 0x00) return 1;
        if ((firstCodeUnit & 0xE0) == 0xC0) return 2;
        if ((firstCodeUnit & 0xF0) == 0xE0) return 3;
        if ((firstCodeUnit & 0xF8) == 0xF0) return 4;
        return 0;
    }

    private static bool IsUncPathImpl(ReadOnlySpan<byte> path, bool checkForwardSlash, bool checkBackSlash)
    {
        Assert.SdkRequiresNotNull(path);

        if ((uint)path.Length < UncPathPrefixLength)
            return false;

        if (checkForwardSlash && path[0] == '/' && path[1] == '/')
            return true;

        return checkBackSlash && path[0] == '\\' && path[1] == '\\';
    }

    private static bool IsUncPathImpl(ReadOnlySpan<char> path, bool checkForwardSlash, bool checkBackSlash)
    {
        Assert.SdkRequiresNotNull(path);

        if ((uint)path.Length < UncPathPrefixLength)
            return false;

        if (checkForwardSlash && path[0] == '/' && path[1] == '/')
            return true;

        return checkBackSlash && path[0] == '\\' && path[1] == '\\';
    }

    private static int GetUncPathPrefixLengthImpl(ReadOnlySpan<byte> path, bool checkForwardSlash)
    {
        Assert.SdkRequiresNotNull(path);

        int length;
        int separatorCount = 0;

        for (length = 0; length < path.Length && path[length] != 0; length++)
        {
            if (checkForwardSlash && path[length] == '/')
                ++separatorCount;

            if (path[length] == '\\')
                ++separatorCount;

            if (separatorCount == 4)
                return length;
        }

        return length;
    }

    private static int GetUncPathPrefixLengthImpl(ReadOnlySpan<char> path, bool checkForwardSlash)
    {
        Assert.SdkRequiresNotNull(path);

        int length;
        int separatorCount = 0;

        for (length = 0; length < path.Length && path[length] != 0; length++)
        {
            if (checkForwardSlash && path[length] == '/')
                ++separatorCount;

            if (path[length] == '\\')
                ++separatorCount;

            if (separatorCount == 4)
                return length;
        }

        return length;
    }

    private static bool IsDosDevicePathImpl(ReadOnlySpan<byte> path)
    {
        Assert.SdkRequiresNotNull(path);

        if ((uint)path.Length < DosDevicePathPrefixLength)
            return false;

        return path[0] == '\\' &&
               path[1] == '\\' &&
               (path[2] == '.' || path[2] == '?') &&
               (path[3] == '/' || path[3] == '\\');
    }

    private static bool IsDosDevicePathImpl(ReadOnlySpan<char> path)
    {
        Assert.SdkRequiresNotNull(path);

        if ((uint)path.Length < DosDevicePathPrefixLength)
            return false;

        return path[0] == '\\' &&
               path[1] == '\\' &&
               (path[2] == '.' || path[2] == '?') &&
               (path[3] == '/' || path[3] == '\\');
    }

    public static bool IsWindowsDrive(ReadOnlySpan<byte> path)
    {
        Assert.SdkRequiresNotNull(path);

        if ((uint)path.Length < WindowsDriveLength)
            return false;

        // Mask lowercase letters to uppercase and check if it's in range
        return ((0b1101_1111 & path[0]) - 'A' <= 'Z' - 'A') && path[1] == ':';
        // return ('a' <= c && c <= 'z' || 'A' <= c && c <= 'Z') && path[1] == ':';
    }

    public static bool IsUncPath(ReadOnlySpan<byte> path)
    {
        return IsUncPathImpl(path, true, true);
    }

    public static bool IsUncPath(ReadOnlySpan<byte> path, bool checkForwardSlash, bool checkBackSlash)
    {
        return IsUncPathImpl(path, checkForwardSlash, checkBackSlash);
    }

    public static int GetUncPathPrefixLength(ReadOnlySpan<byte> path)
    {
        return GetUncPathPrefixLengthImpl(path, true);
    }

    public static bool IsDosDevicePath(ReadOnlySpan<byte> path)
    {
        return IsDosDevicePathImpl(path);
    }

    public static int GetDosDevicePathPrefixLength()
    {
        return DosDevicePathPrefixLength;
    }

    public static bool IsWindowsPath(ReadOnlySpan<byte> path, bool checkForwardSlash)
    {
        return IsWindowsDrive(path) || IsDosDevicePath(path) || IsUncPath(path, checkForwardSlash, true);
    }

    public static int GetWindowsSkipLength(ReadOnlySpan<byte> path)
    {
        if (IsDosDevicePath(path))
            return GetDosDevicePathPrefixLength();

        if (IsWindowsDrive(path))
            return WindowsDriveLength;

        if (IsUncPath(path))
            return GetUncPathPrefixLength(path);

        return 0;
    }

    public static bool IsDosDelimiterW(char c)
    {
        return c == '/' || c == '\\';
    }

    public static bool IsWindowsDriveW(ReadOnlySpan<char> path)
    {
        Assert.SdkRequiresNotNull(path);

        if ((uint)path.Length < WindowsDriveLength)
            return false;

        // Mask lowercase letters to uppercase and check if it's in range
        return ((0b1101_1111 & path[0]) - 'A' <= 'Z' - 'A') && path[1] == ':';
        // return ('a' <= c && c <= 'z' || 'A' <= c && c <= 'Z') && path[1] == ':';
    }

    public static bool IsUncPathW(ReadOnlySpan<char> path)
    {
        return IsUncPathImpl(path, true, true);
    }

    public static int GetUncPathPrefixLengthW(ReadOnlySpan<char> path)
    {
        return GetUncPathPrefixLengthImpl(path, true);
    }

    public static bool IsDosDevicePathW(ReadOnlySpan<char> path)
    {
        return IsDosDevicePathImpl(path);
    }

    public static bool IsWindowsPathW(ReadOnlySpan<char> path)
    {
        return IsWindowsDriveW(path) || IsUncPathW(path) || IsDosDevicePathW(path);
    }

    public static Result CheckCharacterCountForWindows(ReadOnlySpan<byte> path, int maxNameLength, int maxPathLength)
    {
        Assert.SdkRequiresNotNull(path);

        ReadOnlySpan<byte> currentChar = path;
        int currentNameLength = 0;
        int currentPathLength = 0;

        while (currentChar.Length > 1 && currentChar[0] != 0)
        {
            int utf16CodeUnitCount = GetCodePointByteLength(currentChar[0]) < 4 ? 1 : 2;

            int utf8Buffer = 0;
            CharacterEncodingResult result =
                PickOutCharacterFromUtf8String(SpanHelpers.AsByteSpan(ref utf8Buffer), ref currentChar);

            if (result != CharacterEncodingResult.Success)
                return ResultFs.InvalidPathFormat.Log();

            result = ConvertCharacterUtf8ToUtf32(out uint pathChar, SpanHelpers.AsReadOnlyByteSpan(in utf8Buffer));

            if (result != CharacterEncodingResult.Success)
                return ResultFs.InvalidPathFormat.Log();

            currentNameLength += utf16CodeUnitCount;
            currentPathLength += utf16CodeUnitCount;

            if (pathChar == '/' || pathChar == '\\')
                currentNameLength = 0;

            if (maxNameLength > 0 && currentNameLength > maxNameLength)
                return ResultFs.TooLongPath.Log();

            if (maxPathLength > 0 && currentPathLength > maxPathLength)
                return ResultFs.TooLongPath.Log();
        }

        return Result.Success;
    }
}
