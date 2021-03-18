using System;
using LibHac.Crypto.Impl;

namespace LibHac.Crypto
{
    public static class Sha256
    {
        public const int DigestSize = 0x20;

        /// <summary>
        /// Creates an uninitialized SHA-256 <see cref="IHash"/> object.
        /// </summary>
        /// <returns> The new uninitialized SHA-256 <see cref="IHash"/> object.</returns>
        public static IHash CreateSha256Generator()
        {
            return new Sha256Generator();
        }

        public static void GenerateSha256Hash(ReadOnlySpan<byte> data, Span<byte> hashBuffer)
        {
            var sha256 = new Sha256Impl();
            sha256.Initialize();

            sha256.Update(data);
            sha256.GetHash(hashBuffer);
        }
    }
}
