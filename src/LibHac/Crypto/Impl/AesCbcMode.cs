using System;
using System.Security.Cryptography;

namespace LibHac.Crypto.Impl;

public struct AesCbcMode
{
    private AesCore _aesCore;

    public void Initialize(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, bool isDecrypting)
    {
        _aesCore = new AesCore();
        _aesCore.Initialize(key, iv, CipherMode.CBC, isDecrypting);
    }

    public int Encrypt(ReadOnlySpan<byte> input, Span<byte> output)
    {
        return _aesCore.Encrypt(input, output);
    }

    public int Decrypt(ReadOnlySpan<byte> input, Span<byte> output)
    {
        return _aesCore.Decrypt(input, output);
    }
}