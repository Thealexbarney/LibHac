using System;

namespace LibHac.Fs
{
    public class MemoryStorage : StorageBase
    {
        private byte[] _buffer;
        private int _start;
        private int _length;
        private int _capacity;
        private bool _isExpandable;

        public MemoryStorage() : this(0) { }

        public MemoryStorage(int capacity)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Argument must be positive");

            _capacity = capacity;
            _isExpandable = true;
            CanAutoExpand = true;
            _buffer = new byte[capacity];
        }

        public MemoryStorage(byte[] buffer) : this(buffer, 0, buffer.Length) { }

        public MemoryStorage(byte[] buffer, int index, int count)
        {
            if (buffer == null) throw new NullReferenceException(nameof(buffer));
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index), "Value must be non-negative.");
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), "Value must be non-negative.");
            if (buffer.Length - index < count) throw new ArgumentException("Length, index and count parameters are invalid.");

            _buffer = buffer;
            _start = index;
            _length = count;
            _capacity = count;
            _isExpandable = false;
        }

        protected override void ReadImpl(Span<byte> destination, long offset)
        {
            _buffer.AsSpan((int)(_start + offset), destination.Length).CopyTo(destination);
        }

        protected override void WriteImpl(ReadOnlySpan<byte> source, long offset)
        {
            long requiredCapacity = _start + offset + source.Length;

            if (requiredCapacity > _length)
            {
                if (requiredCapacity > _capacity) EnsureCapacity(requiredCapacity);
                _length = (int)(requiredCapacity - _start);
            }

            source.CopyTo(_buffer.AsSpan((int)(_start + offset), source.Length));
        }

        public byte[] ToArray()
        {
            var array = new byte[_length];
            Buffer.BlockCopy(_buffer, _start, array, 0, _length);
            return array;
        }

        // returns a bool saying whether we allocated a new array.
        private void EnsureCapacity(long value)
        {
            if (value < 0 || value > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(value));
            if (value <= _capacity) return;

            long newCapacity = Math.Max(value, 256);
            newCapacity = Math.Max(newCapacity, _capacity * 2);

            SetCapacity((int)Math.Min(newCapacity, int.MaxValue));
        }

        private void SetCapacity(int value)
        {
            if (value < _length)
                throw new ArgumentOutOfRangeException(nameof(value), "Capacity is smaller than the current length.");

            if (!_isExpandable && value != _capacity) throw new NotSupportedException("MemoryStorage is not expandable.");

            if (_isExpandable && value != _capacity)
            {
                var newBuffer = new byte[value];
                Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _length);

                _buffer = newBuffer;
                _capacity = value;
            }
        }

        public override void Flush() { }

        public override long GetSize() => _length;
    }
}
