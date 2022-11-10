using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Crypto;
using LibHac.Util;

namespace LibHac.Boot;

public struct EncryptedKeyBlob
{
    public AesCmac Cmac;
    public AesIv Counter;
    public Array144<byte> Payload;

    public Span<byte> Bytes => SpanHelpers.AsByteSpan(ref this);
    public readonly ReadOnlySpan<byte> BytesRo => SpanHelpers.AsReadOnlyByteSpan(in this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool IsZeros()
    {
        foreach (ulong val in SpanHelpers.AsReadOnlySpan<EncryptedKeyBlob, ulong>(in this))
        {
            if (val != 0)
                return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<byte>(in EncryptedKeyBlob value) =>
        SpanHelpers.AsReadOnlyByteSpan(in value);

    public readonly override string ToString() => BytesRo.ToHexString();
}

public struct KeyBlob
{
    public AesKey MasterKek;
    public Array112<byte> Unused;
    public AesKey Package1Key;

    public Span<byte> Bytes => SpanHelpers.AsByteSpan(ref this);
    public readonly ReadOnlySpan<byte> BytesRo => SpanHelpers.AsReadOnlyByteSpan(in this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool IsZeros()
    {
        foreach (ulong val in SpanHelpers.AsReadOnlySpan<KeyBlob, ulong>(in this))
        {
            if (val != 0)
                return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<byte>(in KeyBlob value) => SpanHelpers.AsReadOnlyByteSpan(in value);

    public readonly override string ToString() => BytesRo.ToHexString();
}