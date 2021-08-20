using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.Common.FixedArrays
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Array5<T>
    {
        public const int Length = 5;

        private T _1;
        private T _2;
        private T _3;
        private T _4;
        private T _5;

        public ref T this[int i] => ref Items[i];

        public Span<T> Items => SpanHelpers.CreateSpan(ref _1, Length);
        public readonly ReadOnlySpan<T> ItemsRo => SpanHelpers.CreateReadOnlySpan(in _1, Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlySpan<T>(in Array5<T> value) => value.ItemsRo;
    }
}