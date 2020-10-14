using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Crypto;
using LibHac.Util;

namespace LibHac.Boot
{
    [DebuggerDisplay("{ToString()}")]
    [StructLayout(LayoutKind.Explicit, Size = 0xB0)]
    public struct EncryptedKeyBlob
    {
#if DEBUG
        [FieldOffset(0x00)] [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Buffer32 _dummy1;
        [FieldOffset(0x20)] [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Buffer32 _dummy2;
        [FieldOffset(0x40)] [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Buffer32 _dummy3;
        [FieldOffset(0x60)] [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Buffer32 _dummy4;
        [FieldOffset(0x80)] [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Buffer32 _dummy5;
        [FieldOffset(0xA0)] [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Buffer16 _dummy6;
#endif

        [FieldOffset(0x00)] public AesCmac Cmac;
        [FieldOffset(0x10)] public AesIv Counter;

        public Span<byte> Payload => Bytes.Slice(0x20, Unsafe.SizeOf<KeyBlob>());

        public Span<byte> Bytes => SpanHelpers.AsByteSpan(ref this);
        public readonly ReadOnlySpan<byte> ReadOnlyBytes => SpanHelpers.AsReadOnlyByteSpan(in this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsZeros()
        {
            ReadOnlySpan<ulong> ulongSpan = MemoryMarshal.Cast<byte, ulong>(ReadOnlyBytes);

            for (int i = 0; i < ulongSpan.Length; i++)
            {
                if (ulongSpan[i] != 0)
                    return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlySpan<byte>(in EncryptedKeyBlob value)
        {
            return SpanHelpers.AsReadOnlyByteSpan(in value);
        }

        public override readonly string ToString() => ReadOnlyBytes.ToHexString();
    }

    [DebuggerDisplay("{ToString()}")]
    [StructLayout(LayoutKind.Explicit, Size = 0x90)]
    public struct KeyBlob
    {
#if DEBUG
        [FieldOffset(0x00)] [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Buffer32 _dummy1;
        [FieldOffset(0x20)] [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Buffer32 _dummy2;
        [FieldOffset(0x40)] [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Buffer32 _dummy3;
        [FieldOffset(0x60)] [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Buffer32 _dummy4;
#endif

        [FieldOffset(0x00)] public AesKey MasterKek;
        [FieldOffset(0x80)] public AesKey Package1Key;

        public Span<byte> Bytes => SpanHelpers.AsByteSpan(ref this);
        public readonly ReadOnlySpan<byte> ReadOnlyBytes => SpanHelpers.AsReadOnlyByteSpan(in this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsZeros()
        {
            ReadOnlySpan<ulong> ulongSpan = MemoryMarshal.Cast<byte, ulong>(ReadOnlyBytes);

            for (int i = 0; i < ulongSpan.Length; i++)
            {
                if (ulongSpan[i] != 0)
                    return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlySpan<byte>(in KeyBlob value)
        {
            return SpanHelpers.AsReadOnlyByteSpan(in value);
        }

        public override readonly string ToString() => ReadOnlyBytes.ToHexString();
    }
}
