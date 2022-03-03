using System;
using System.Diagnostics;
using System.Security.Cryptography;
using LibHac.Common;

namespace LibHac.Crypto.Impl;

public struct AesCore
{
    private ICryptoTransform _transform;
    private bool _isDecrypting;

    public void Initialize(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, CipherMode mode, bool isDecrypting)
    {
        Debug.Assert(key.Length == Aes.KeySize128);
        Debug.Assert(iv.IsEmpty || iv.Length == Aes.BlockSize);

        var aes = System.Security.Cryptography.Aes.Create();

        if (aes == null) throw new CryptographicException("Unable to create AES object");
        aes.Key = key.ToArray();
        aes.Mode = mode;
        aes.Padding = PaddingMode.None;

        if (!iv.IsEmpty)
        {
            aes.IV = iv.ToArray();
        }

        _transform = isDecrypting ? aes.CreateDecryptor() : aes.CreateEncryptor();
        _isDecrypting = isDecrypting;
    }

    public int Encrypt(ReadOnlySpan<byte> input, Span<byte> output)
    {
        Debug.Assert(!_isDecrypting);
        return Transform(input, output);
    }

    public int Decrypt(ReadOnlySpan<byte> input, Span<byte> output)
    {
        Debug.Assert(_isDecrypting);
        return Transform(input, output);
    }

    public int Encrypt(byte[] input, byte[] output, int length)
    {
        Debug.Assert(!_isDecrypting);
        return Transform(input, output, length);
    }

    public int Decrypt(byte[] input, byte[] output, int length)
    {
        Debug.Assert(_isDecrypting);
        return Transform(input, output, length);
    }

    private int Transform(ReadOnlySpan<byte> input, Span<byte> output)
    {
        using var rented = new RentedArray<byte>(input.Length);

        input.CopyTo(rented.Array);

        int bytesWritten = Transform(rented.Array, rented.Array, input.Length);

        rented.Array.CopyTo(output);

        return bytesWritten;
    }

    private int Transform(byte[] input, byte[] output, int length)
    {
        return _transform.TransformBlock(input, 0, length, output, 0);
    }
}