#pragma warning disable CS0169, IDE0051 // Remove unused private members
using System;
using System.Runtime.CompilerServices;

namespace LibHac.Common.FixedArrays;

public struct Array11<T>
{
    public const int Length = 11;

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

    public ref T this[int i] => ref Items[i];

    public Span<T> Items => SpanHelpers.CreateSpan(ref _1, Length);
    public readonly ReadOnlySpan<T> ItemsRo => SpanHelpers.CreateReadOnlySpan(in _1, Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<T>(in Array11<T> value) => value.ItemsRo;
}