using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.Common.FixedArrays
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Array256<T>
    {
        public const int Length = 256;

        private Array128<T> _0;
        private Array128<T> _128;

        public ref T this[int i] => ref Items[i];

        public Span<T> Items
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => SpanHelpers.CreateSpan(ref MemoryMarshal.GetReference(_0.Items), Length);
        }

        public readonly ReadOnlySpan<T> ItemsRo
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => SpanHelpers.CreateSpan(ref MemoryMarshal.GetReference(_0.ItemsRo), Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlySpan<T>(in Array256<T> value) => value.ItemsRo;
    }
}