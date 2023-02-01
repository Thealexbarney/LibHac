using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Util;

namespace LibHac.Crypto;

[StructLayout(LayoutKind.Explicit, Size = Size)]
public struct AesKey
{
    private const int Size = 0x10;

    [FieldOffset(0)] private byte _byte;
    [FieldOffset(0)] private ulong _ulong;

    [UnscopedRef] public Span<byte> Data => SpanHelpers.CreateSpan(ref _byte, Size);
    [UnscopedRef] public readonly ReadOnlySpan<byte> DataRo => SpanHelpers.CreateReadOnlySpan(in _byte, Size);
    [UnscopedRef] public Span<ulong> Data64 => SpanHelpers.CreateSpan(ref _ulong, Size / sizeof(ulong));
    [UnscopedRef] public readonly ReadOnlySpan<ulong> DataRo64 => SpanHelpers.CreateReadOnlySpan(in _ulong, Size / sizeof(ulong));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool IsZeros() => (DataRo64[0] | DataRo64[1]) == 0;

    public static implicit operator Span<byte>(in AesKey value) => Unsafe.AsRef(in value).Data;

    public static implicit operator ReadOnlySpan<byte>(in AesKey value) => value.DataRo;

    public readonly override string ToString() => DataRo.ToHexString();

#if DEBUG
    [FieldOffset(8)][DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly ulong _dummy1;
#endif
}

[StructLayout(LayoutKind.Explicit, Size = Size)]
public struct AesXtsKey
{
    private const int Size = 0x20;

    [FieldOffset(0)] private byte _byte;
    [FieldOffset(0)] private ulong _ulong;

    [FieldOffset(0)] public AesKey DataKey;
    [FieldOffset(0x10)] public AesKey TweakKey;

    [UnscopedRef] public Span<byte> Data => SpanHelpers.CreateSpan(ref _byte, Size);
    [UnscopedRef] public readonly ReadOnlySpan<byte> DataRo => SpanHelpers.CreateReadOnlySpan(in _byte, Size);
    [UnscopedRef] public Span<ulong> Data64 => SpanHelpers.CreateSpan(ref _ulong, Size / sizeof(ulong));
    [UnscopedRef] public readonly ReadOnlySpan<ulong> DataRo64 => SpanHelpers.CreateReadOnlySpan(in _ulong, Size / sizeof(ulong));

    [UnscopedRef] public Span<AesKey> SubKeys => SpanHelpers.CreateSpan(ref DataKey, Size / Unsafe.SizeOf<AesKey>());

    public static implicit operator Span<byte>(in AesXtsKey value) => Unsafe.AsRef(in value).Data;
    public static implicit operator ReadOnlySpan<byte>(in AesXtsKey value) => value.DataRo;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool IsZeros() => (DataRo64[0] | DataRo64[1] | DataRo64[2] | DataRo64[3]) == 0;

    public readonly override string ToString() => DataRo.ToHexString();
}

[StructLayout(LayoutKind.Explicit, Size = Size)]
public struct AesIv
{
    private const int Size = 0x10;

    [FieldOffset(0)] private byte _byte;
    [FieldOffset(0)] private ulong _ulong;

    [UnscopedRef] public Span<byte> Data => SpanHelpers.CreateSpan(ref _byte, Size);
    [UnscopedRef] public readonly ReadOnlySpan<byte> DataRo => SpanHelpers.CreateReadOnlySpan(in _byte, Size);
    [UnscopedRef] public Span<ulong> Data64 => SpanHelpers.CreateSpan(ref _ulong, Size / sizeof(ulong));
    [UnscopedRef] public readonly ReadOnlySpan<ulong> DataRo64 => SpanHelpers.CreateReadOnlySpan(in _ulong, Size / sizeof(ulong));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool IsZeros() => (DataRo64[0] | DataRo64[1]) == 0;

    public static implicit operator Span<byte>(in AesIv value) => Unsafe.AsRef(in value).Data;
    public static implicit operator ReadOnlySpan<byte>(in AesIv value) => value.DataRo;

    public readonly override string ToString() => DataRo.ToHexString();

#if DEBUG
    [FieldOffset(8)][DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly ulong _dummy1;
#endif
}

[StructLayout(LayoutKind.Explicit, Size = Size)]
public struct AesCmac
{
    private const int Size = 0x10;

    [FieldOffset(0)] private byte _byte;
    [FieldOffset(0)] private ulong _ulong;

    [UnscopedRef] public Span<byte> Data => SpanHelpers.CreateSpan(ref _byte, Size);
    [UnscopedRef] public readonly ReadOnlySpan<byte> DataRo => SpanHelpers.CreateReadOnlySpan(in _byte, Size);
    [UnscopedRef] public Span<ulong> Data64 => SpanHelpers.CreateSpan(ref _ulong, Size / sizeof(ulong));
    [UnscopedRef] public readonly ReadOnlySpan<ulong> DataRo64 => SpanHelpers.CreateReadOnlySpan(in _ulong, Size / sizeof(ulong));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool IsZeros() => (DataRo64[0] | DataRo64[1]) == 0;

    public static implicit operator Span<byte>(in AesCmac value) => Unsafe.AsRef(in value).Data;
    public static implicit operator ReadOnlySpan<byte>(in AesCmac value) => value.DataRo;

    public readonly override string ToString() => DataRo.ToHexString();

#if DEBUG
    [FieldOffset(8)][DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly ulong _dummy1;
#endif
}

[StructLayout(LayoutKind.Sequential)]
public struct RsaFullKey
{
    public Array256<byte> PrivateExponent;
    public Array256<byte> Modulus;
    public Array4<byte> PublicExponent;
    public Array128<byte> Dp;
    public Array128<byte> Dq;
    public Array128<byte> InverseQ;
    public Array128<byte> P;
    public Array128<byte> Q;
}

[StructLayout(LayoutKind.Sequential)]
public struct RsaKey
{
    public Array256<byte> Modulus;
    public Array4<byte> PublicExponent;
}

[StructLayout(LayoutKind.Sequential)]
public struct RsaKeyPair
{
    public Array256<byte> PrivateExponent;
    public Array256<byte> Modulus;
    public Array4<byte> PublicExponent;
    public Array12<byte> Reserved;
}