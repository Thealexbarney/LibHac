using System;

namespace LibHac.Crypto.Impl;

public struct AesEcbModeNi
{
    private AesCoreNi _aesCore;

    public void Initialize(ReadOnlySpan<byte> key, bool isDecrypting)
    {
        _aesCore.Initialize(key, isDecrypting);
    }

    public int Encrypt(ReadOnlySpan<byte> input, Span<byte> output)
    {
        return _aesCore.EncryptInterleaved8(input, output);
    }

    public int Decrypt(ReadOnlySpan<byte> input, Span<byte> output)
    {
        return _aesCore.DecryptInterleaved8(input, output);
    }
}