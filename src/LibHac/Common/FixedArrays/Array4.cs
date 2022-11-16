#pragma warning disable CS0169, CS0649, IDE0051 // Field is never used, Field is never assigned to, Remove unused private members
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace LibHac.Common.FixedArrays;

public struct Array4<T>
{
    public const int Length = 4;

    private T _0;
    private T _1;
    private T _2;
    private T _3;

    [UnscopedRef] public ref T this[int i] => ref Items[i];

    [UnscopedRef] public Span<T> Items => SpanHelpers.CreateSpan(ref _0, Length);
    [UnscopedRef] public readonly ReadOnlySpan<T> ItemsRo => SpanHelpers.CreateReadOnlySpan(in _0, Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<T>(in Array4<T> value) => value.ItemsRo;
}