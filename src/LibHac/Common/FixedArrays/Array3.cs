﻿#pragma warning disable CS0169, IDE0051 // Remove unused private members
using System;
using System.Runtime.CompilerServices;

namespace LibHac.Common.FixedArrays;

public struct Array3<T>
{
    public const int Length = 3;

    private T _1;
    private T _2;
    private T _3;

    public ref T this[int i] => ref Items[i];

    public Span<T> Items => SpanHelpers.CreateSpan(ref _1, Length);
    public readonly ReadOnlySpan<T> ItemsRo => SpanHelpers.CreateReadOnlySpan(in _1, Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<T>(in Array3<T> value) => value.ItemsRo;
}