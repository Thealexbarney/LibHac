using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Fs;
using LibHac.Util;

namespace LibHac.FsSrv.Sf;

[StructLayout(LayoutKind.Sequential)]
public readonly struct FspPath
{
    internal const int MaxLength = 0x300;

    private readonly Array769<byte> _value;

    [UnscopedRef] public ReadOnlySpan<byte> Str => SpanHelpers.AsReadOnlyByteSpan(in _value);

    public static Result FromSpan(out FspPath fspPath, ReadOnlySpan<byte> path)
    {
        UnsafeHelpers.SkipParamInit(out fspPath);

        Span<byte> str = SpanHelpers.AsByteSpan(ref fspPath);

        // Ensure null terminator even if the creation fails
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