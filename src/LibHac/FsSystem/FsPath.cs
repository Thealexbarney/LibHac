using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Util;

namespace LibHac.FsSystem
{
    [DebuggerDisplay("{ToString()}")]
    [StructLayout(LayoutKind.Sequential, Size = MaxLength + 1)]
    public struct FsPath
    {
        internal const int MaxLength = 0x300;

#if DEBUG
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Padding100 Padding000;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Padding100 Padding100;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Padding100 Padding200;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly byte Padding300;
#endif

        public Span<byte> Str => SpanHelpers.AsByteSpan(ref this);

        public static Result FromSpan(out FsPath fsPath, ReadOnlySpan<byte> path)
        {
            UnsafeHelpers.SkipParamInit(out fsPath);

            // Ensure null terminator even if the creation fails for safety
            fsPath.Str[MaxLength] = 0;

            var sb = new U8StringBuilder(fsPath.Str);
            bool overflowed = sb.Append(path).Overflowed;

            return overflowed ? ResultFs.TooLongPath.Log() : Result.Success;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator U8Span(in FsPath value) => new U8Span(SpanHelpers.AsReadOnlyByteSpan(in value));

        public override string ToString() => StringUtils.Utf8ZToString(Str);
    }
}
