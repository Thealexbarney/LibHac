using System;
using System.Diagnostics;
using System.Security.Cryptography;
using LibHac.Common;

namespace LibHac.Crypto.Impl
{
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

        public void Encrypt(ReadOnlySpan<byte> input, Span<byte> output)
        {
            Debug.Assert(!_isDecrypting);
            Transform(input, output);
        }

        public void Decrypt(ReadOnlySpan<byte> input, Span<byte> output)
        {
            Debug.Assert(_isDecrypting);
            Transform(input, output);
        }

        public void Encrypt(byte[] input, byte[] output, int length)
        {
            Debug.Assert(!_isDecrypting);
            Transform(input, output, length);
        }

        public void Decrypt(byte[] input, byte[] output, int length)
        {
            Debug.Assert(_isDecrypting);
            Transform(input, output, length);
        }

        private void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            using var rented = new RentedArray<byte>(input.Length);

            input.CopyTo(rented.Array);

            Transform(rented.Array, rented.Array, input.Length);

            rented.Array.CopyTo(output);
        }

        private void Transform(byte[] input, byte[] output, int length)
        {
            _transform.TransformBlock(input, 0, length, output, 0);
        }
    }
}
