using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.Common.FixedArrays
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Array16<T>
    {
        public const int Length = 16;

        private Array8<T> _0;
        private Array8<T> _8;

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
        public static implicit operator ReadOnlySpan<T>(in Array16<T> value) => value.ItemsRo;
    }
}