using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;

namespace LibHac.Spl
{
    [DebuggerDisplay("{ToString()}")]
    [StructLayout(LayoutKind.Sequential, Size = 0x10)]
    public struct AccessKey
    {
        private long _dummy1;
        private long _dummy2;

        public Span<byte> Key => SpanHelpers.CreateSpan(ref Unsafe.As<long, byte>(ref _dummy1), 0x10);

        public override string ToString() => Key.ToHexString();
    }
}
