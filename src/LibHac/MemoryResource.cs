using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace LibHac
{
    public abstract class MemoryResource
    {
        private const int DefaultAlignment = 8;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Buffer Allocate(long size, int alignment = DefaultAlignment) =>
            DoAllocate(size, alignment);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deallocate(ref Buffer buffer, int alignment = DefaultAlignment)
        {
            DoDeallocate(buffer, alignment);

            // Clear the references to the deallocated buffer.
            buffer = new Buffer();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsEqual(MemoryResource other) =>
            DoIsEqual(other);

        protected abstract Buffer DoAllocate(long size, int alignment);
        protected abstract void DoDeallocate(Buffer buffer, int alignment);
        protected abstract bool DoIsEqual(MemoryResource other);

        /// <summary>
        /// Represents a region of memory allocated by a <see cref="MemoryResource"/>.
        /// </summary>
        public struct Buffer
        {
            private Memory<byte> _memory;

            /// <summary>
            /// A field where <see cref="MemoryResource"/> implementers can store info about the <see cref="Buffer"/>.
            /// </summary>
            internal object Extra { get; }

            /// <summary>
            /// The length of the buffer in bytes.
            /// </summary>
            public readonly int Length => _memory.Length;

            /// <summary>
            /// Gets a span from the <see cref="Buffer"/>.
            /// </summary>
            public readonly Span<byte> Get() => _memory.Span;

            /// <summary>
            /// Returns <see langword="true"/> if the <see cref="Buffer"/> is valid.
            /// </summary>
            public readonly bool IsValid => !_memory.Equals(default);

            internal Buffer(Memory<byte> memory, object extra = null)
            {
                _memory = memory;
                Extra = extra;
            }
        }
    }

    public class ArrayPoolMemoryResource : MemoryResource
    {
        protected override Buffer DoAllocate(long size, int alignment)
        {
            byte[] array = ArrayPool<byte>.Shared.Rent((int)size);

            return new Buffer(array.AsMemory(0, (int)size), array);
        }

        protected override void DoDeallocate(Buffer buffer, int alignment)
        {
            if (buffer.Extra is byte[] array)
            {
                ArrayPool<byte>.Shared.Return(array);
            }
            else
            {
                throw new LibHacException("Buffer was not allocated by this MemoryResource.");
            }
        }

        protected override bool DoIsEqual(MemoryResource other)
        {
            return ReferenceEquals(this, other);
        }
    }
}
