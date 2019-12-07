using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.Crypto
{
    internal static class CryptoUtil
    {
        public static bool IsSameBytes(ReadOnlySpan<byte> buffer1, ReadOnlySpan<byte> buffer2, int length)
        {
            if (buffer1.Length < (uint)length || buffer2.Length < (uint)length)
                throw new ArgumentOutOfRangeException(nameof(length));

            return IsSameBytes(ref MemoryMarshal.GetReference(buffer1), ref MemoryMarshal.GetReference(buffer2), length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsSameBytes(ref byte p1, ref byte p2, int length)
        {
            int result = 0;

            for (int i = 0; i < length; i++)
            {
                result |= Unsafe.Add(ref p1, i) ^ Unsafe.Add(ref p2, i);
            }

            return result == 0;
        }
    }
}