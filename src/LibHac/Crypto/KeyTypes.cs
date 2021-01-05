using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Util;

namespace LibHac.Crypto
{
    [DebuggerDisplay("{ToString()}")]
    [StructLayout(LayoutKind.Explicit, Size = Size)]
    public struct AesKey
    {
        private const int Size = 0x10;

        [FieldOffset(0)] private byte _byte;
        [FieldOffset(0)] private ulong _ulong;

        public Span<byte> Data => SpanHelpers.CreateSpan(ref _byte, Size);
        public readonly ReadOnlySpan<byte> DataRo => SpanHelpers.CreateReadOnlySpan(in _byte, Size);
        public Span<ulong> Data64 => SpanHelpers.CreateSpan(ref _ulong, Size / sizeof(ulong));
        public readonly ReadOnlySpan<ulong> DataRo64 => SpanHelpers.CreateReadOnlySpan(in _ulong, Size / sizeof(ulong));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsZeros() => (DataRo64[0] | DataRo64[1]) == 0;

        public static implicit operator Span<byte>(in AesKey value) => Unsafe.AsRef(in value).Data;

        public static implicit operator ReadOnlySpan<byte>(in AesKey value) => value.DataRo;

        public override readonly string ToString() => DataRo.ToHexString();

#if DEBUG
        [FieldOffset(8)] [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly ulong _dummy1;
#endif
    }

    [DebuggerDisplay("{ToString()}")]
    [StructLayout(LayoutKind.Explicit, Size = Size)]
    public struct AesXtsKey
    {
        private const int Size = 0x20;

        [FieldOffset(0)] private byte _byte;
        [FieldOffset(0)] private ulong _ulong;

        [FieldOffset(0)] public AesKey DataKey;
        [FieldOffset(0x10)] public AesKey TweakKey;

        public Span<byte> Data => SpanHelpers.CreateSpan(ref _byte, Size);
        public readonly ReadOnlySpan<byte> DataRo => SpanHelpers.CreateReadOnlySpan(in _byte, Size);
        public Span<ulong> Data64 => SpanHelpers.CreateSpan(ref _ulong, Size / sizeof(ulong));
        public readonly ReadOnlySpan<ulong> DataRo64 => SpanHelpers.CreateReadOnlySpan(in _ulong, Size / sizeof(ulong));

        public Span<AesKey> SubKeys => SpanHelpers.CreateSpan(ref DataKey, Size / Unsafe.SizeOf<AesKey>());

        public static implicit operator Span<byte>(in AesXtsKey value) => Unsafe.AsRef(in value).Data;
        public static implicit operator ReadOnlySpan<byte>(in AesXtsKey value) => value.DataRo;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsZeros() => (DataRo64[0] | DataRo64[1] | DataRo64[2] | DataRo64[3]) == 0;

        public override readonly string ToString() => DataRo.ToHexString();
    }

    [DebuggerDisplay("{ToString()}")]
    [StructLayout(LayoutKind.Explicit, Size = Size)]
    public struct AesIv
    {
        private const int Size = 0x10;

        [FieldOffset(0)] private byte _byte;
        [FieldOffset(0)] private ulong _ulong;

        public Span<byte> Data => SpanHelpers.CreateSpan(ref _byte, Size);
        public readonly ReadOnlySpan<byte> DataRo => SpanHelpers.CreateReadOnlySpan(in _byte, Size);
        public Span<ulong> Data64 => SpanHelpers.CreateSpan(ref _ulong, Size / sizeof(ulong));
        public readonly ReadOnlySpan<ulong> DataRo64 => SpanHelpers.CreateReadOnlySpan(in _ulong, Size / sizeof(ulong));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsZeros() => (DataRo64[0] | DataRo64[1]) == 0;

        public static implicit operator Span<byte>(in AesIv value) => Unsafe.AsRef(in value).Data;
        public static implicit operator ReadOnlySpan<byte>(in AesIv value) => value.DataRo;

        public override readonly string ToString() => DataRo.ToHexString();

#if DEBUG
        [FieldOffset(8)] [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly ulong _dummy1;
#endif
    }

    [DebuggerDisplay("{ToString()}")]
    [StructLayout(LayoutKind.Explicit, Size = Size)]
    public struct AesCmac
    {
        private const int Size = 0x10;

        [FieldOffset(0)] private byte _byte;
        [FieldOffset(0)] private ulong _ulong;

        public Span<byte> Data => SpanHelpers.CreateSpan(ref _byte, Size);
        public readonly ReadOnlySpan<byte> DataRo => SpanHelpers.CreateReadOnlySpan(in _byte, Size);
        public Span<ulong> Data64 => SpanHelpers.CreateSpan(ref _ulong, Size / sizeof(ulong));
        public readonly ReadOnlySpan<ulong> DataRo64 => SpanHelpers.CreateReadOnlySpan(in _ulong, Size / sizeof(ulong));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsZeros() => (DataRo64[0] | DataRo64[1]) == 0;

        public static implicit operator Span<byte>(in AesCmac value) => Unsafe.AsRef(in value).Data;
        public static implicit operator ReadOnlySpan<byte>(in AesCmac value) => value.DataRo;

        public override readonly string ToString() => DataRo.ToHexString();

#if DEBUG
        [FieldOffset(8)] [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly ulong _dummy1;
#endif
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RsaFullKey
    {
        public Data100 PrivateExponent;
        public Data80 Dp;
        public Data80 Dq;
        public Data3 PublicExponent;
        public Data80 InverseQ;
        public Data100 Modulus;
        public Data80 P;
        public Data80 Q;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RsaKey
    {
        public Data100 Modulus;
        public Data3 PublicExponent;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x100)]
    public struct Data100
    {
        [FieldOffset(0)] private byte _byte;

        public Span<byte> Data => SpanHelpers.CreateSpan(ref _byte, 0x100);
        public readonly ReadOnlySpan<byte> DataRo => SpanHelpers.CreateReadOnlySpan(in _byte, 0x100);
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x80)]
    public struct Data80
    {
        [FieldOffset(0)] private byte _byte;

        public Span<byte> Data => SpanHelpers.CreateSpan(ref _byte, 0x80);
        public readonly ReadOnlySpan<byte> DataRo => SpanHelpers.CreateReadOnlySpan(in _byte, 0x80);
    }

    [StructLayout(LayoutKind.Explicit, Size = 3)]
    public struct Data3
    {
        [FieldOffset(0)] private byte _byte;

        public Span<byte> Data => SpanHelpers.CreateSpan(ref _byte, 3);
        public readonly ReadOnlySpan<byte> DataRo => SpanHelpers.CreateReadOnlySpan(in _byte, 3);
    }
}
