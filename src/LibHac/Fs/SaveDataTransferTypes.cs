using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using LibHac.Common.FixedArrays;

namespace LibHac.Fs;

public struct RsaEncryptedKey
{
    public Array256<byte> Value;
}

public struct AesKey
{
    private Array2<ulong> _value;

    [UnscopedRef]
    public Span<byte> Value => MemoryMarshal.Cast<ulong, byte>(_value);
}

public struct AesMac
{
    public Array16<byte> Data;
}

public struct AesIv
{
    public Array16<byte> Data;
}

public struct InitialDataVersion2
{
    public Array8192<byte> Value;
}