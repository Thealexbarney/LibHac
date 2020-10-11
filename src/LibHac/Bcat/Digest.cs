using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Util;

namespace LibHac.Bcat
{
    [DebuggerDisplay("{ToString()}")]
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct Digest
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private ulong _dummy0;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private ulong _dummy1;

        public byte this[int i]
        {
            get => Bytes[i];
            set => Bytes[i] = value;
        }

        public Span<byte> Bytes => SpanHelpers.AsByteSpan(ref this);

        public override string ToString()
        {
            return Bytes.ToHexString();
        }
    }
}
