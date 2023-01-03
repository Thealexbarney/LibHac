using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace LibHac.Mem;

/// <summary>
/// Represents a region of memory allocated by a <see cref="MemoryResource"/>.
/// </summary>
public struct Buffer : IEquatable<Buffer>
{
    private Memory<byte> _memory;

    public static Buffer Empty => default;

    /// <summary>
    /// A field where <see cref="MemoryResource"/> implementers can store info about the <see cref="Buffer"/>.
    /// </summary>
    internal object Extra { get; }

    /// <summary>
    /// The length of the buffer in bytes.
    /// </summary>
    public readonly int Length => _memory.Length;

    /// <summary>
    /// Gets a <see cref="Span{T}"/> from the <see cref="Buffer"/>.
    /// </summary>
    public readonly Span<byte> Span => _memory.Span;

    /// <summary>
    /// Returns <see langword="true"/> if the <see cref="Buffer"/> is not valid.
    /// </summary>
    public readonly bool IsNull => _memory.IsEmpty;

    internal Buffer(Memory<byte> memory, object extra = null)
    {
        _memory = memory;
        Extra = extra;
    }

    /// <summary>
    /// Forms a <see cref="BufferSegment"/> out of the current <see cref="Buffer"/> that begins at a specified index.
    /// </summary>
    /// <param name="start">The index at which to begin the segment.</param>
    /// <returns><para>A <see cref="BufferSegment"/> that contains all elements of the current <see cref="Buffer"/> instance
    /// from <paramref name="start"/> to the end of the instance.</para>
    /// <para> The <see cref="BufferSegment"/> must not be accessed after this parent <see cref="Buffer"/> is deallocated.</para></returns>
    internal BufferSegment GetSegment(int start) => new BufferSegment(_memory.Slice(start));
    
    /// <summary>
    /// Forms a <see cref="BufferSegment"/> out of the current <see cref="Buffer"/> starting at a specified index for a specified length.
    /// </summary>
    /// <param name="start">The index at which to begin the segment.</param>
    /// <param name="length">The number of elements to include in the segment.</param>
    /// <returns><para>A <see cref="BufferSegment"/> that contains <paramref name="length"/> elements from the current
    /// <see cref="Buffer"/> instance starting at <paramref name="start"/>.</para>
    /// <para> The <see cref="BufferSegment"/> must not be accessed after this parent <see cref="Buffer"/> is deallocated.</para></returns>
    internal BufferSegment GetSegment(int start, int length) => new BufferSegment(_memory.Slice(start, length));

    public static bool operator ==(Buffer left, Buffer right) => left._memory.Equals(right._memory);
    public static bool operator !=(Buffer left, Buffer right) => !(left == right);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool Equals(object obj) => obj is Buffer other && Equals(other);
    public bool Equals(Buffer other) => _memory.Equals(other._memory);
    public override int GetHashCode() => _memory.GetHashCode();
}

/// <summary>
/// Represents a region of memory borrowed from a <see cref="Buffer"/>.
/// This <see cref="BufferSegment"/> must not be accessed after the parent <see cref="Buffer"/> that created it is deallocated.
/// </summary>
public readonly struct BufferSegment
{
    private readonly Memory<byte> _memory;

    public BufferSegment(Memory<byte> memory)
    {
        _memory = memory;
    }

    /// <summary>
    /// The length of the buffer in bytes.
    /// </summary>
    public int Length => _memory.Length;

    /// <summary>
    /// Gets a <see cref="Span{T}"/> from the <see cref="BufferSegment"/>.
    /// </summary>
    public Span<byte> Span => _memory.Span;

    /// <summary>
    /// Gets a <see cref="Span{T}"/> from the <see cref="BufferSegment"/> of the specified type.
    /// </summary>
    public Span<T> GetSpan<T>() where T : unmanaged => MemoryMarshal.Cast<byte, T>(Span);

    /// <summary>
    /// Returns <see langword="true"/> if the <see cref="BufferSegment"/> is not valid.
    /// </summary>
    public bool IsNull => _memory.IsEmpty;
}