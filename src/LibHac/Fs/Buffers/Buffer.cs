using System;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs
{
    public readonly struct Buffer
    {
        public Memory<byte> Memory { get; }
        public Span<byte> Span => Memory.Span;

        public Buffer(Memory<byte> buffer)
        {
            Memory = buffer;
        }
    }
}
