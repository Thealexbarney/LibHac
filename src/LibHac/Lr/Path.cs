using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Util;

namespace LibHac.Lr
{
    [DebuggerDisplay("{ToString()}")]
    [StructLayout(LayoutKind.Sequential, Size = PathTool.EntryNameLengthMax)]
    public struct Path
    {
#if DEBUG
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Padding100 Padding000;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Padding100 Padding100;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Padding100 Padding200;
#endif

        public readonly ReadOnlySpan<byte> Str => SpanHelpers.AsReadOnlyByteSpan(in this);
        public Span<byte> StrMutable => SpanHelpers.AsByteSpan(ref this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InitEmpty(out Path path)
        {
            UnsafeHelpers.SkipParamInit(out path);
            SpanHelpers.AsByteSpan(ref path)[0] = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator U8Span(in Path value) => new U8Span(SpanHelpers.AsReadOnlyByteSpan(in value));

        public override readonly string ToString() => StringUtils.Utf8ZToString(Str);
    }
}
