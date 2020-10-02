using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.Common.FixedArrays
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Array12<T>
    {
        public const int Length = 12;

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

        public ref T this[int i] => ref Items[i];

        public Span<T> Items => SpanHelpers.CreateSpan(ref _item1, Length);
        public readonly ReadOnlySpan<T> ReadOnlyItems => SpanHelpers.CreateReadOnlySpan(in _item1, Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlySpan<T>(in Array12<T> value) => value.ReadOnlyItems;
    }
}