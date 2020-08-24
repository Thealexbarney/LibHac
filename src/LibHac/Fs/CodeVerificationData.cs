using System;
using System.Runtime.InteropServices;
using LibHac.Common;

namespace LibHac.Fs
{
    [StructLayout(LayoutKind.Explicit, Size = 0x124)]
    public struct CodeVerificationData
    {
        private const int Signature2Size = 0x100;

        [FieldOffset(0x000)] private byte _signature2;
        [FieldOffset(0x100)] public Buffer32 NcaHeaderHash;
        [FieldOffset(0x120)] public bool IsValid;

        public Span<byte> NcaSignature2 => SpanHelpers.CreateSpan(ref _signature2, Signature2Size);
    }
}
