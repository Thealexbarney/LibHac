using System;
using System.Buffers;
using System.Security.Cryptography;

namespace LibHac.Crypto2
{
    public class AesEcbEncryptor : ICipher
    {
        private const int BufferRentThreshold = 1024;
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
            if (input.Length < BufferRentThreshold)
            {
                var outputBuffer = new byte[input.Length];
                input.CopyTo(outputBuffer);

                _encryptor.TransformBlock(outputBuffer, 0, input.Length, outputBuffer, 0);

                outputBuffer.CopyTo(output);
            }
            else
            {
                byte[] outputBuffer = ArrayPool<byte>.Shared.Rent(input.Length);
                try
                {
                    input.CopyTo(outputBuffer);

                    _encryptor.TransformBlock(outputBuffer, 0, input.Length, outputBuffer, 0);

                    outputBuffer.CopyTo(output);
                }
                finally { ArrayPool<byte>.Shared.Return(outputBuffer); }
            }
        }
    }

    public class AesEcbDecryptor : ICipher
    {
        private const int BufferRentThreshold = 1024;
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
            if (input.Length < BufferRentThreshold)
            {
                var outputBuffer = new byte[input.Length];
                input.CopyTo(outputBuffer);

                _decryptor.TransformBlock(outputBuffer, 0, input.Length, outputBuffer, 0);

                outputBuffer.CopyTo(output);
            }
            else
            {
                byte[] outputBuffer = ArrayPool<byte>.Shared.Rent(input.Length);
                try
                {
                    input.CopyTo(outputBuffer);

                    _decryptor.TransformBlock(outputBuffer, 0, input.Length, outputBuffer, 0);

                    outputBuffer.CopyTo(output);
                }
                finally { ArrayPool<byte>.Shared.Return(outputBuffer); }
            }
        }
    }
}
