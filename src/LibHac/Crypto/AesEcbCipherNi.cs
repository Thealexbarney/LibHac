using System;
using LibHac.Crypto.Impl;

namespace LibHac.Crypto;

public class AesEcbEncryptorNi : ICipher
{
    private AesEcbModeNi _baseCipher;

    public AesEcbEncryptorNi(ReadOnlySpan<byte> key)
    {
        _baseCipher = new AesEcbModeNi();
        _baseCipher.Initialize(key, false);
    }

    public int Transform(ReadOnlySpan<byte> input, Span<byte> output)
    {
        return _baseCipher.Encrypt(input, output);
    }
}

public class AesEcbDecryptorNi : ICipher
{
    private AesEcbModeNi _baseCipher;

    public AesEcbDecryptorNi(ReadOnlySpan<byte> key)
    {
        _baseCipher = new AesEcbModeNi();
        _baseCipher.Initialize(key, true);
    }

    public int Transform(ReadOnlySpan<byte> input, Span<byte> output)
    {
        return _baseCipher.Decrypt(input, output);
    }
}