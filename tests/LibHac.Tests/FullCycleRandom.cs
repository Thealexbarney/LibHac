using System;
using System.Numerics;

namespace LibHac.Tests
{
    /// <summary>
    /// Simple, full-cycle PRNG for use in tests.
    /// </summary>
    public class FullCycleRandom
    {
        private int _state;
        private int _mult;
        private int _inc;
        private int _and;
        private int _max;

        public FullCycleRandom(int period, int seed)
        {
            // Avoid exponential growth pattern when initializing with a 0 seed
            seed ^= 0x55555555;
            _max = period - 1;
            int order = BitOperations.Log2((uint)period - 1) + 1;

            // There isn't any deep reasoning behind the choice of the number of bits
            // in the seed used for initializing each parameter
            int multSeedBits = Math.Max(order >> 1, 2);
            int multSeedMask = (1 << multSeedBits) - 1;
            int multSeed = seed & multSeedMask;
            _mult = (multSeed << 2) | 5;

            int incSeedBits = Math.Max(order >> 2, 2);
            int incSeedMask = (1 << incSeedBits) - 1;
            int incSeed = (seed >> multSeedBits) & incSeedMask;
            _inc = incSeed | 1;

            int stateSeedBits = order;
            int stateSeedMask = (1 << stateSeedBits) - 1;
            int stateSeed = (seed >> multSeedBits + incSeedBits) & stateSeedMask;
            _state = stateSeed;

            _and = (1 << order) - 1;
        }

        public int Next()
        {
            do
            {
                _state = (_state * _mult + _inc) & _and;
            } while (_state > _max);

            return _state;
        }
    }
}
