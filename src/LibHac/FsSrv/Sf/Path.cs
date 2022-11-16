using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;

namespace LibHac.FsSrv.Sf;

[StructLayout(LayoutKind.Sequential)]
public readonly struct Path
{
    private readonly Array769<byte> _value;

    [UnscopedRef] public ReadOnlySpan<byte> Str => SpanHelpers.AsReadOnlyByteSpan(in _value);
}