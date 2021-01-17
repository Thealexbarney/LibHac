using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace LibHac.Tests
{
    public struct Random
    {
        private ulong _state1;
        private ulong _state2;

        public Random(ulong seed)
        {
            ulong x = seed;
            ulong z = x + 0x9e3779b97f4a7c15;
            z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9;
            z = (z ^ (z >> 27)) * 0x94d049bb133111eb;
            x = z ^ (z >> 31);
            z = (x += 0x9e3779b97f4a7c15);
            z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9;
            z = (z ^ (z >> 27)) * 0x94d049bb133111eb;
            _state1 = z ^ (z >> 31);
            _state2 = x;
        }

        ulong Next()
        {
            ulong s0 = _state1;
            ulong s1 = _state2;
            ulong result = BitOperations.RotateLeft(s0 + s1, 17) + s0;

            s1 ^= s0;
            _state1 = BitOperations.RotateLeft(s0, 49) ^ s1 ^ (s1 << 21);
            _state2 = BitOperations.RotateLeft(s1, 28);

            return result;
        }

        public int Next(int minValue, int maxValue)
        {
            if (minValue > maxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(minValue));
            }

            long range = (long)maxValue - minValue;
            return (int)((uint)Next() * (1.0 / uint.MaxValue) * range) + minValue;
        }

        public void NextBytes(Span<byte> buffer)
        {
            Span<ulong> bufferUlong = MemoryMarshal.Cast<byte, ulong>(buffer);

            for (int i = 0; i < bufferUlong.Length; i++)
            {
                bufferUlong[i] = Next();
            }

            for (int i = bufferUlong.Length * sizeof(ulong); i < buffer.Length; i++)
            {
                buffer[i] = (byte)Next();
            }
        }
    }
}
