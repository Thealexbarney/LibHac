using System;
using System.Numerics;
using System.Security.Cryptography;

namespace LibHac.Crypto
{
    public static class Rsa
    {
        public static bool VerifyRsa2048PssSha256(ReadOnlySpan<byte> signature, ReadOnlySpan<byte> modulus,
            ReadOnlySpan<byte> exponent, ReadOnlySpan<byte> message) =>
                VerifyRsa2048Sha256(signature, modulus, exponent, message, RSASignaturePadding.Pss);

        public static bool VerifyRsa2048Pkcs1Sha256(ReadOnlySpan<byte> signature, ReadOnlySpan<byte> modulus,
            ReadOnlySpan<byte> exponent, ReadOnlySpan<byte> message) =>
                VerifyRsa2048Sha256(signature, modulus, exponent, message, RSASignaturePadding.Pkcs1);

        private static bool VerifyRsa2048Sha256(ReadOnlySpan<byte> signature, ReadOnlySpan<byte> modulus,
            ReadOnlySpan<byte> exponent, ReadOnlySpan<byte> message, RSASignaturePadding padding)
        {
            try
            {
                var param = new RSAParameters { Modulus = modulus.ToArray(), Exponent = exponent.ToArray() };

                using (var rsa = RSA.Create(param))
                {
                    return rsa.VerifyData(message, signature, HashAlgorithmName.SHA256, padding);
                }
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

        public static bool VerifyRsa2048PssSha256WithHash(ReadOnlySpan<byte> signature, ReadOnlySpan<byte> modulus,
            ReadOnlySpan<byte> exponent, ReadOnlySpan<byte> message)
        {
            try
            {
                var param = new RSAParameters { Modulus = modulus.ToArray(), Exponent = exponent.ToArray() };

                using (var rsa = RSA.Create(param))
                {
                    return rsa.VerifyHash(message, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
                }
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

        /// <param name="n">The RSA Modulus (n)</param>
        /// <param name="e">The RSA Public Exponent (e)</param>
        /// <param name="d">The RSA Private Exponent (d)</param>
        public static RSAParameters RecoverParameters(BigInteger n, BigInteger e, BigInteger d)
        {
            (BigInteger p, BigInteger q) = DeriveRsaPrimeNumberPair(n, e, d);

            BigInteger dp = d % (p - BigInteger.One);
            BigInteger dq = d % (q - BigInteger.One);
            BigInteger inverseQ = ModInverse(q, p);

            byte[] nBytes = n.ToByteArray();
            int modLen = nBytes.Length;

            if (nBytes[^1] == 0)
            {
                modLen--;
            }

            int halfModLen = (modLen + 1) / 2;

            return new RSAParameters
            {
                Modulus = n.GetBytes(modLen),
                Exponent = e.GetBytes(-1),
                D = d.GetBytes(modLen),
                P = p.GetBytes(halfModLen),
                Q = q.GetBytes(halfModLen),
                DP = dp.GetBytes(halfModLen),
                DQ = dq.GetBytes(halfModLen),
                InverseQ = inverseQ.GetBytes(halfModLen)
            };
        }

        /// <param name="n">The RSA Modulus (n)</param>
        /// <param name="e">The RSA Public Exponent (e)</param>
        /// <param name="d">The RSA Private Exponent (d)</param>
        public static RSAParameters RecoverParameters(ReadOnlySpan<byte> n, ReadOnlySpan<byte> e, ReadOnlySpan<byte> d) =>
            RecoverParameters(n.GetBigInteger(), e.GetBigInteger(), d.GetBigInteger());

        /// <summary>
        /// Derive RSA Prime Number Pair (p, q) from RSA Modulus (n), RSA Public Exponent (e) and RSA Private Exponent (d)
        /// </summary>
        /// <param name="n">The RSA Modulus (n)</param>
        /// <param name="e">The RSA Public Exponent (e)</param>
        /// <param name="d">The RSA Private Exponent (d)</param>
        /// <returns>RSA Prime Number Pair</returns>
        private static (BigInteger p, BigInteger q) DeriveRsaPrimeNumberPair(BigInteger n, BigInteger e, BigInteger d)
        {
            BigInteger k = d * e - BigInteger.One;

            if (!k.IsEven)
            {
                throw new InvalidOperationException("d*e - 1 is odd");
            }

            BigInteger two = BigInteger.One + BigInteger.One;
            BigInteger t = BigInteger.One;

            BigInteger r = k / two;

            while (r.IsEven)
            {
                t++;
                r /= two;
            }

            byte[] rndBuf = n.ToByteArray();

            if (rndBuf[^1] == 0)
            {
                rndBuf = new byte[rndBuf.Length - 1];
            }

            BigInteger nMinusOne = n - BigInteger.One;

            bool cracked = false;
            BigInteger y = BigInteger.Zero;

            var rng = new Random(0);

            for (int i = 0; i < 100 && !cracked; i++)
            {
                BigInteger g;

                do
                {
                    rng.NextBytes(rndBuf);
                    g = GetBigInteger(rndBuf);
                }
                while (g >= n);

                y = BigInteger.ModPow(g, r, n);

                if (y.IsOne || y == nMinusOne)
                {
                    i--;
                    continue;
                }

                for (BigInteger j = BigInteger.One; j < t; j++)
                {
                    BigInteger x = BigInteger.ModPow(y, two, n);

                    if (x.IsOne)
                    {
                        cracked = true;
                        break;
                    }

                    if (x == nMinusOne)
                    {
                        break;
                    }

                    y = x;
                }
            }

            if (!cracked)
            {
                throw new InvalidOperationException("Prime factors not found");
            }

            BigInteger p = BigInteger.GreatestCommonDivisor(y - BigInteger.One, n);
            BigInteger q = n / p;

            return (p, q);
        }

        private static BigInteger GetBigInteger(this ReadOnlySpan<byte> bytes)
        {
            byte[] signPadded = new byte[bytes.Length + 1];
            bytes.CopyTo(signPadded.AsSpan(1));
            Array.Reverse(signPadded);
            return new BigInteger(signPadded);
        }

        private static byte[] GetBytes(this BigInteger value, int size)
        {
            byte[] bytes = value.ToByteArray();

            if (size == -1)
            {
                size = bytes.Length;
            }

            if (bytes.Length > size + 1)
            {
                throw new InvalidOperationException($"Cannot squeeze value {value} to {size} bytes from {bytes.Length}.");
            }

            if (bytes.Length == size + 1 && bytes[bytes.Length - 1] != 0)
            {
                throw new InvalidOperationException($"Cannot squeeze value {value} to {size} bytes from {bytes.Length}.");
            }

            Array.Resize(ref bytes, size);
            Array.Reverse(bytes);
            return bytes;
        }

        private static BigInteger ModInverse(BigInteger e, BigInteger n)
        {
            BigInteger r = n;
            BigInteger newR = e;
            BigInteger t = 0;
            BigInteger newT = 1;

            while (newR != 0)
            {
                BigInteger quotient = r / newR;
                BigInteger temp;

                temp = t;
                t = newT;
                newT = temp - quotient * newT;

                temp = r;
                r = newR;
                newR = temp - quotient * newR;
            }

            if (t < 0)
            {
                t = t + n;
            }

            return t;
        }
    }
}
