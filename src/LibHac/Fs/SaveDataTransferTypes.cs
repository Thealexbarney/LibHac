using System;
using System.Runtime.InteropServices;
using LibHac.Common;

namespace LibHac.Fs
{
    [StructLayout(LayoutKind.Sequential, Size = 0x100)]
    public struct RsaEncryptedKey
    {
        public byte this[int i]
        {
            readonly get => BytesRo[i];
            set => Bytes[i] = value;
        }

        public Span<byte> Bytes => SpanHelpers.AsByteSpan(ref this);
        public readonly ReadOnlySpan<byte> BytesRo => SpanHelpers.AsReadOnlyByteSpan(in this);

    }

    [StructLayout(LayoutKind.Sequential, Size = 0x10)]
    public struct AesKey
    {
        public byte this[int i]
        {
            readonly get => BytesRo[i];
            set => Bytes[i] = value;
        }

        public Span<byte> Bytes => SpanHelpers.AsByteSpan(ref this);
        public readonly ReadOnlySpan<byte> BytesRo => SpanHelpers.AsReadOnlyByteSpan(in this);
    }
}
