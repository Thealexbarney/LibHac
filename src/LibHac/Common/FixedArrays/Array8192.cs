#pragma warning disable CS0169, CS0649, IDE0051 // Field is never used, Field is never assigned to, Remove unused private members
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.Common.FixedArrays;

public struct Array8192<T>
{
    public const int Length = 8192;

    private Array4096<T> _0;
    private Array4096<T> _4096;

    public ref T this[int i] => ref Items[i];

    public Span<T> Items
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => SpanHelpers.CreateSpan(ref MemoryMarshal.GetReference(_0.Items), Length);
    }

    public readonly ReadOnlySpan<T> ItemsRo
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => SpanHelpers.CreateSpan(ref MemoryMarshal.GetReference(_0.ItemsRo), Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<T>(in Array8192<T> value) => value.ItemsRo;
}