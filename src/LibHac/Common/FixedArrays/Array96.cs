#pragma warning disable CS0169, CS0649, IDE0051 // Field is never used, Field is never assigned to, Remove unused private members
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.Common.FixedArrays;

public struct Array96<T>
{
    public const int Length = 96;

    private Array80<T> _0;
    private Array16<T> _80;

    [UnscopedRef] public ref T this[int i] => ref Items[i];

    [UnscopedRef]
    public Span<T> Items
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => SpanHelpers.CreateSpan(ref MemoryMarshal.GetReference(_0.Items), Length);
    }

    [UnscopedRef]
    public readonly ReadOnlySpan<T> ItemsRo
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => SpanHelpers.CreateSpan(ref MemoryMarshal.GetReference(_0.ItemsRo), Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<T>(in Array96<T> value) => value.ItemsRo;
}