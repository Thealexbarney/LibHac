using System;
using System.ComponentModel;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs
{
    public readonly struct Buffer : IEquatable<Buffer>
    {
        public static Buffer Empty => default;
        public Memory<byte> Memory { get; }
        public Span<byte> Span => Memory.Span;
        public int Length => Memory.Length;
        public bool IsNull => Memory.IsEmpty;

        public Buffer(Memory<byte> buffer)
        {
            Memory = buffer;
        }

        public static bool operator ==(Buffer left, Buffer right) => left.Memory.Equals(right.Memory);
        public static bool operator !=(Buffer left, Buffer right) => !(left == right);

        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj) => obj is Buffer other && Equals(other);
        public bool Equals(Buffer other) => Memory.Equals(other.Memory);
        public override int GetHashCode() => Memory.GetHashCode();
    }
}
