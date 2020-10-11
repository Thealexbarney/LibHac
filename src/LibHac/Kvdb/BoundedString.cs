using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Util;

namespace LibHac.Kvdb
{
    public struct BoundedString<TSize> where TSize : unmanaged
    {
        private TSize _string;

        public Span<byte> Get() => SpanHelpers.AsByteSpan(ref _string);

        public int GetLength() =>
            StringUtils.GetLength(SpanHelpers.AsReadOnlyByteSpan(in _string), Unsafe.SizeOf<TSize>());
    }

    [StructLayout(LayoutKind.Sequential, Size = 768)]
    internal struct Size768 { }
}
