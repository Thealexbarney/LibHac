using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.Common;

public static class SpanExtensions
{
    /// <summary>
    /// Gets the element at the specified zero-based index or gets 0 if the index is out-of-bounds.
    /// </summary>
    /// <param name="span">The <see cref="ReadOnlySpan{T}"/> containing the element to get.</param>
    /// <param name="i">The zero-based index of the element.</param>
    /// <returns>The element at the specified index or 0 if out-of-bounds.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte At(in this ReadOnlySpan<byte> span, int i)
    {
        return (uint)i >= (uint)span.Length ? (byte)0 : span[i];
    }

    /// <summary>
    /// Gets the element at the specified zero-based index or gets 0 if the index is out-of-bounds.
    /// </summary>
    /// <param name="span">The <see cref="Span{T}"/> containing the element to get.</param>
    /// <param name="i">The zero-based index of the element.</param>
    /// <returns>The element at the specified index or 0 if out-of-bounds.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte At(in this Span<byte> span, int i)
    {
        return (uint)i >= (uint)span.Length ? (byte)0 : span[i];
    }
}

public static class SpanHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> CreateSpan<T>(ref T reference, int length)
    {
        return MemoryMarshal.CreateSpan(ref reference, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(ref T reference) where T : unmanaged
    {
        return CreateSpan(ref reference, 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<TSpan> AsSpan<TStruct, TSpan>(ref TStruct reference)
        where TStruct : unmanaged where TSpan : unmanaged
    {
        return CreateSpan(ref Unsafe.As<TStruct, TSpan>(ref reference),
            Unsafe.SizeOf<TStruct>() / Unsafe.SizeOf<TSpan>());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<byte> AsByteSpan<T>(ref T reference) where T : unmanaged
    {
        return CreateSpan(ref Unsafe.As<T, byte>(ref reference), Unsafe.SizeOf<T>());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> CreateReadOnlySpan<T>(in T reference, int length)
    {
        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in reference), length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(in T reference) where T : unmanaged
    {
        return CreateReadOnlySpan(in reference, 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<TSpan> AsReadOnlySpan<TStruct, TSpan>(in TStruct reference)
        where TStruct : unmanaged where TSpan : unmanaged
    {
        return CreateReadOnlySpan(in Unsafe.As<TStruct, TSpan>(ref Unsafe.AsRef(in reference)),
            Unsafe.SizeOf<TStruct>() / Unsafe.SizeOf<TSpan>());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<byte> AsReadOnlyByteSpan<T>(in T reference) where T : unmanaged
    {
        return CreateReadOnlySpan(in Unsafe.As<T, byte>(ref Unsafe.AsRef(in reference)), Unsafe.SizeOf<T>());
    }

    // All AsStruct methods do bounds checks on the input
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T AsStruct<T>(Span<byte> span) where T : unmanaged
    {
        return ref MemoryMarshal.Cast<byte, T>(span)[0];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref readonly T AsReadOnlyStruct<T>(ReadOnlySpan<byte> span) where T : unmanaged
    {
        return ref MemoryMarshal.Cast<byte, T>(span)[0];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref TTo AsStruct<TFrom, TTo>(Span<TFrom> span)
        where TFrom : unmanaged
        where TTo : unmanaged
    {
        return ref MemoryMarshal.Cast<TFrom, TTo>(span)[0];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref readonly TTo AsStruct<TFrom, TTo>(ReadOnlySpan<TFrom> span)
        where TFrom : unmanaged
        where TTo : unmanaged
    {
        return ref MemoryMarshal.Cast<TFrom, TTo>(span)[0];
    }
}
