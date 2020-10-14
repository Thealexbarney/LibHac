using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.Common.FixedArrays
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Array32<T>
    {
        public const int Length = 32;

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
        private T _item13;
        private T _item14;
        private T _item15;
        private T _item16;
        private T _item17;
        private T _item18;
        private T _item19;
        private T _item20;
        private T _item21;
        private T _item22;
        private T _item23;
        private T _item24;
        private T _item25;
        private T _item26;
        private T _item27;
        private T _item28;
        private T _item29;
        private T _item30;
        private T _item31;
        private T _item32;

        public ref T this[int i] => ref Items[i];

        public Span<T> Items => SpanHelpers.CreateSpan(ref _item01, Length);
        public readonly ReadOnlySpan<T> ItemsRo => SpanHelpers.CreateReadOnlySpan(in _item01, Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlySpan<T>(in Array32<T> value) => value.ItemsRo;
    }
}
