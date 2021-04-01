using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using static LibHac.Fs.StringTraits;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs
{
    public static class WindowsPath
    {
        private const int WindowsDriveLength = 2;
        private const int UncPathPrefixLength = 2;
        private const int DosDevicePathPrefixLength = 4;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWindowsDrive(U8Span path)
        {
            Assert.SdkRequires(!path.IsNull());

            return (uint)path.Length > 1 &&
                    IsWindowsDriveCharacter(path[0]) &&
                    path[1] == DriveSeparator;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWindowsDriveCharacter(byte c)
        {
            // Mask lowercase letters to uppercase and check if it's in range
            return (0b1101_1111 & c) - 'A' <= 'Z' - 'A';
            // return 'a' <= c && c <= 'z' || 'A' <= c && c <= 'Z';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsUnc(U8Span path)
        {
            return (uint)path.Length >= UncPathPrefixLength &&
                   (path.GetUnsafe(0) == DirectorySeparator && path.GetUnsafe(1) == DirectorySeparator ||
                    path.GetUnsafe(0) == AltDirectorySeparator && path.GetUnsafe(1) == AltDirectorySeparator);
        }

        public static bool IsUnc(string path)
        {
            return (uint)path.Length >= UncPathPrefixLength &&
                   (path[0] == DirectorySeparator && path[1] == DirectorySeparator ||
                    path[0] == AltDirectorySeparator && path[1] == AltDirectorySeparator);
        }

        public static int GetWindowsPathSkipLength(U8Span path)
        {
            if (IsWindowsDrive(path))
                return WindowsDriveLength;

            if (!IsUnc(path))
                return 0;

            for (int i = UncPathPrefixLength; i < path.Length && path[i] != NullTerminator; i++)
            {
                if (path[i] == (byte)'$' || path[i] == DriveSeparator)
                {
                    return i + 1;
                }
            }

            return 0;
        }

        public static bool IsDosDelimiter(char c)
        {
            return c == '/' || c == '\\';
        }

        public static bool IsDosDevicePath(ReadOnlySpan<char> path)
        {
            return path.Length >= DosDevicePathPrefixLength
                   && IsDosDelimiter(path[0])
                   && path[1] == '\\'
                   && (path[2] == '.' || path[2] == '?')
                   && IsDosDelimiter(path[3]);
        }

        public static int GetDosDevicePathPrefixLength()
        {
            return DosDevicePathPrefixLength;
        }

        public static bool IsDriveName(ReadOnlySpan<char> path)
        {
            return path.Length == WindowsDriveLength && path[1] == ':';
        }

        public static bool IsUncPath(ReadOnlySpan<char> path)
        {
            return !IsDosDevicePath(path) && path.Length >= UncPathPrefixLength && IsDosDelimiter(path[0]) &&
                   IsDosDelimiter(path[1]);
        }

        public static int GetUncPathPrefixLength(ReadOnlySpan<char> path)
        {
            int i;
            for (i = 0; i < path.Length; i++)
            {
                if (path[i] == '\0')
                    break;

                if (IsDosDelimiter(path[i]) && i + 1 == DosDevicePathPrefixLength)
                    break;
            }

            return i;
        }

        public static bool IsPathRooted(ReadOnlySpan<char> path)
        {
            return IsDriveName(path.Slice(0, Math.Min(WindowsDriveLength, path.Length)))
                   || IsDosDevicePath(path.Slice(0, Math.Min(DosDevicePathPrefixLength, path.Length)))
                   || IsUncPath(path.Slice(0, Math.Min(DosDevicePathPrefixLength, path.Length)));
        }
    }
}
