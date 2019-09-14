using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.Common
{
    public static class SpanHelpers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETCOREAPP
        public static Span<T> CreateSpan<T>(ref T reference, int length)
        {
            return MemoryMarshal.CreateSpan(ref reference, length);
        }
#else
        public static unsafe Span<T> CreateSpan<T>(ref T reference, int length)
        {
            return new Span<T>(Unsafe.AsPointer(ref reference), length);
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> AsSpan<T>(ref T reference) where T : unmanaged
        {
            return CreateSpan(ref reference, 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<byte> AsByteSpan<T>(ref T reference) where T : unmanaged
        {
            Span<T> span = AsSpan(ref reference);
            return MemoryMarshal.Cast<T, byte>(span);
        }
    }
}
