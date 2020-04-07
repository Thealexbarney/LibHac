using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LibHac.Common;

namespace LibHac.Bcat
{
    [DebuggerDisplay("{ToString()}")]
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct DirectoryName
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private ulong _dummy0;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private ulong _dummy1;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private ulong _dummy2;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private ulong _dummy3;

        public byte this[int i]
        {
            get => Bytes[i];
            set => Bytes[i] = value;
        }

        public Span<byte> Bytes => SpanHelpers.AsByteSpan(ref this);

        public override string ToString()
        {
            return StringUtils.Utf8ZToString(Bytes);
        }
    }
}
