using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Util;

namespace LibHac.FsSrv.Sf
{
    [DebuggerDisplay("{ToString()}")]
    [StructLayout(LayoutKind.Sequential, Size = MaxLength + 1)]
    public readonly struct FspPath
    {
        internal const int MaxLength = 0x300;

#if DEBUG
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Padding100 Padding000;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Padding100 Padding100;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Padding100 Padding200;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly byte Padding300;
#endif

        public ReadOnlySpan<byte> Str => SpanHelpers.AsReadOnlyByteSpan(in this);

        public static Result FromSpan(out FspPath fspPath, ReadOnlySpan<byte> path)
        {
            UnsafeHelpers.SkipParamInit(out fspPath);

            Span<byte> str = SpanHelpers.AsByteSpan(ref fspPath);

            // Ensure null terminator even if the creation fails for safety
            str[MaxLength] = 0;

            var sb = new U8StringBuilder(str);
            bool overflowed = sb.Append(path).Overflowed;

            return overflowed ? ResultFs.TooLongPath.Log() : Result.Success;
        }

        public static void CreateEmpty(out FspPath fspPath)
        {
            UnsafeHelpers.SkipParamInit(out fspPath);
            SpanHelpers.AsByteSpan(ref fspPath)[0] = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator U8Span(in FspPath value) => new U8Span(SpanHelpers.AsReadOnlyByteSpan(in value));

        public override string ToString() => StringUtils.Utf8ZToString(Str);
    }
}
