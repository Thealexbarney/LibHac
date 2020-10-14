using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.Common.FixedArrays
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Array12<T>
    {
        public const int Length = 12;

        private T _item01;
        private T _item02;
        private T _item03;
        private T _item04;
        private T _item05;
        private T _item06;
        private T _item07;
        private T _item08;
        private T _item09;
        private T _item10;
        private T _item11;
        private T _item12;

        public ref T this[int i] => ref Items[i];

        public Span<T> Items => SpanHelpers.CreateSpan(ref _item01, Length);
        public readonly ReadOnlySpan<T> ItemsRo => SpanHelpers.CreateReadOnlySpan(in _item01, Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlySpan<T>(in Array12<T> value) => value.ItemsRo;
    }
}