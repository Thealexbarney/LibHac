using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.Common.FixedArrays
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Array12<T>
    {
        public const int Length = 12;

        private T _1;
        private T _2;
        private T _3;
        private T _4;
        private T _5;
        private T _6;
        private T _7;
        private T _8;
        private T _9;
        private T _10;
        private T _11;
        private T _12;

        public ref T this[int i] => ref Items[i];

        public Span<T> Items => SpanHelpers.CreateSpan(ref _1, Length);
        public readonly ReadOnlySpan<T> ItemsRo => SpanHelpers.CreateReadOnlySpan(in _1, Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlySpan<T>(in Array12<T> value) => value.ItemsRo;
    }
}