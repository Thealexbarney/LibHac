using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.Common.FixedArrays
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Array8<T>
    {
        public const int Length = 8;

        private T _item01;
        private T _item02;
        private T _item03;
        private T _item04;
        private T _item05;
        private T _item06;
        private T _item07;
        private T _item08;

        public ref T this[int i] => ref Items[i];

        public Span<T> Items => SpanHelpers.CreateSpan(ref _item01, Length);
        public readonly ReadOnlySpan<T> ItemsRo => SpanHelpers.CreateReadOnlySpan(in _item01, Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlySpan<T>(in Array8<T> value) => value.ItemsRo;
    }
}