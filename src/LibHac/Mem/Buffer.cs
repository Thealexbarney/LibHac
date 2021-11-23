using System;
using System.ComponentModel;

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

    public static bool operator ==(Buffer left, Buffer right) => left._memory.Equals(right._memory);
    public static bool operator !=(Buffer left, Buffer right) => !(left == right);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool Equals(object obj) => obj is Buffer other && Equals(other);
    public bool Equals(Buffer other) => _memory.Equals(other._memory);
    public override int GetHashCode() => _memory.GetHashCode();
}