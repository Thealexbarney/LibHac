using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.Common.FixedArrays
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Array32<T>
    {
        public const int Length = 32;

        private T _item1;
        private T _item2;
        private T _item3;
        private T _item4;
        private T _item5;
        private T _item6;
        private T _item7;
        private T _item8;
        private T _item9;
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

        public Span<T> Items => SpanHelpers.CreateSpan(ref _item1, Length);
        public readonly ReadOnlySpan<T> ReadOnlyItems => SpanHelpers.CreateReadOnlySpan(in _item1, Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlySpan<T>(in Array32<T> value) => value.ReadOnlyItems;
    }
}
