using System;
using System.Security.Cryptography;

namespace LibHac.Crypto2
{
    public class AesEcbEncryptor : ICipher
    {
        private ICryptoTransform _encryptor;

        public AesEcbEncryptor(ReadOnlySpan<byte> key)
        {
            Aes aes = Aes.Create();

            if (aes == null) throw new CryptographicException("Unable to create AES object");
            aes.Key = key.ToArray();
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;

            _encryptor = aes.CreateEncryptor();
        }

        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            var outputBuffer = new byte[input.Length];

            _encryptor.TransformBlock(input.ToArray(), 0, input.Length, outputBuffer, 0);

            outputBuffer.CopyTo(output);
        }
    }

    public class AesEcbDecryptor : ICipher
    {
        private ICryptoTransform _decryptor;

        public AesEcbDecryptor(ReadOnlySpan<byte> key)
        {
            Aes aes = Aes.Create();

            if (aes == null) throw new CryptographicException("Unable to create AES object");
            aes.Key = key.ToArray();
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;

            _decryptor = aes.CreateDecryptor();
        }

        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            var outputBuffer = new byte[input.Length];

            _decryptor.TransformBlock(input.ToArray(), 0, input.Length, outputBuffer, 0);

            outputBuffer.CopyTo(output);
        }
    }
}
