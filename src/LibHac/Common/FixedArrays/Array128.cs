using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.Common.FixedArrays
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Array128<T>
    {
        public const int Length = 128;

        private Array64<T> _0;
        private Array64<T> _64;

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
        public static implicit operator ReadOnlySpan<T>(in Array128<T> value) => value.ItemsRo;
    }
}