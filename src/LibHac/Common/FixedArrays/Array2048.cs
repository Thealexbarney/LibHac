#pragma warning disable CS0169, IDE0051 // Remove unused private members
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.Common.FixedArrays;

public struct Array2048<T>
{
    public const int Length = 2048;

    private Array1024<T> _0;
    private Array1024<T> _1024;

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
    public static implicit operator ReadOnlySpan<T>(in Array2048<T> value) => value.ItemsRo;
}