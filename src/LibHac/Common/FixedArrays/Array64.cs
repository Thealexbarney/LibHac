using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.Common.FixedArrays
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Array64<T>
    {
        public const int Length = 64;

        private Array32<T> _0;
        private Array32<T> _32;

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
        public static implicit operator ReadOnlySpan<T>(in Array64<T> value) => value.ItemsRo;
    }
}