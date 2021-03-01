using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LibHac.Common;

namespace LibHac.Fs
{
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    [DebuggerDisplay("{ToString()}")]
    internal struct MountName
    {
        public Span<byte> Name => SpanHelpers.AsByteSpan(ref this);

        public override string ToString() => new U8Span(Name).ToString();
    }
}
