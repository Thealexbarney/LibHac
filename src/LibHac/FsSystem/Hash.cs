using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LibHac.Common;

namespace LibHac.FsSystem
{
    [StructLayout(LayoutKind.Sequential, Size = 0x20)]
    public struct Hash
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private ulong _dummy0;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private ulong _dummy1;

        public readonly ReadOnlySpan<byte> Bytes => SpanHelpers.AsReadOnlyByteSpan(in this);
        public Span<byte> BytesMutable => SpanHelpers.AsByteSpan(ref this);
    }
}
