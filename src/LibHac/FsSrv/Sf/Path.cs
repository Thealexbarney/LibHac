using System;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;

namespace LibHac.FsSrv.Sf;

[StructLayout(LayoutKind.Sequential)]
public readonly struct Path
{
    private readonly Array769<byte> _value;

    public ReadOnlySpan<byte> Str => SpanHelpers.AsReadOnlyByteSpan(in _value);
}