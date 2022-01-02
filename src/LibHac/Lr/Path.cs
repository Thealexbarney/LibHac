using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Util;

namespace LibHac.Lr;

public struct Path
{
    public Array768<byte> Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InitEmpty(out Path path)
    {
        UnsafeHelpers.SkipParamInit(out path);
        SpanHelpers.AsByteSpan(ref path)[0] = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator U8Span(in Path value) => new U8Span(SpanHelpers.AsReadOnlyByteSpan(in value));

    public readonly override string ToString() => StringUtils.Utf8ZToString(Value);
}