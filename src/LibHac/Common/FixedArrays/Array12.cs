#pragma warning disable CS0169, CS0649, IDE0051 // Field is never used, Field is never assigned to, Remove unused private members
using System;
using System.Runtime.CompilerServices;

namespace LibHac.Common.FixedArrays;

public struct Array12<T>
{
    public const int Length = 12;

    private T _0;
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

    public Span<T> Items => SpanHelpers.CreateSpan(ref _0, Length);
    public readonly ReadOnlySpan<T> ItemsRo => SpanHelpers.CreateReadOnlySpan(in _0, Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<T>(in Array12<T> value) => value.ItemsRo;
}