using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace LibHac.Common
{
    public readonly ref struct RentedArray<T>
    {
        // It's faster to create new smaller arrays than rent them
        private const int RentThresholdBytes = 512;
        private static int RentThresholdElements => RentThresholdBytes / Unsafe.SizeOf<T>();

        private readonly Span<T> _span;

        public T[] Array { get; }
        public Span<T> Span => _span;

        public RentedArray(int minimumSize)
        {
            if (minimumSize >= RentThresholdElements)
            {
                Array = ArrayPool<T>.Shared.Rent(minimumSize);
            }
            else
            {
                Array = new T[minimumSize];
            }

            _span = Array.AsSpan(0, minimumSize);
        }

        public void Dispose()
        {
            // Only return if array was rented
            if (_span.Length >= RentThresholdElements)
            {
                ArrayPool<T>.Shared.Return(Array);
            }
        }
    }
}
