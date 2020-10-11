using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Util;

namespace LibHac.Bcat
{
    [DebuggerDisplay("{ToString()}")]
    [StructLayout(LayoutKind.Sequential, Size = MaxSize)]
    public struct DirectoryName
    {
        private const int MaxSize = 0x20;

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

        public bool IsValid()
        {
            Span<byte> name = Bytes;

            int i;
            for (i = 0; i < name.Length; i++)
            {
                if (name[i] == 0)
                    break;

                if (!StringUtils.IsDigit(name[i]) && !StringUtils.IsAlpha(name[i]) && name[i] != '_' && name[i] != '-')
                    return false;
            }

            if (i == 0 || i == MaxSize)
                return false;

            return name[i] == 0;
        }

        public override string ToString()
        {
            return StringUtils.Utf8ZToString(Bytes);
        }
    }
}
