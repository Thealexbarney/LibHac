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
    }
}
