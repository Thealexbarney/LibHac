using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Util;

namespace LibHac.Common
{
    [DebuggerDisplay("{ToString()}")]
    internal ref struct PathBuilder
    {
        private Span<byte> _buffer;
        private int _pos;

        public int Length
        {
            get => _pos;
            set
            {
                Debug.Assert(value >= 0);
                Debug.Assert(value <= Capacity);
                _pos = value;
            }
        }

        public int Capacity => _buffer.Length - 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PathBuilder(Span<byte> buffer)
        {
            _buffer = buffer;
            _pos = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result Append(byte value)
        {
            int pos = _pos;
            if (pos >= Capacity)
            {
                return ResultFs.TooLongPath.Log();
            }

            _buffer[pos] = value;
            _pos = pos + 1;
            return Result.Success;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result Append(ReadOnlySpan<byte> value)
        {
            int pos = _pos;
            if (pos + value.Length >= Capacity)
            {
                return ResultFs.TooLongPath.Log();
            }

            value.CopyTo(_buffer.Slice(pos));
            _pos = pos + value.Length;
            return Result.Success;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result AppendWithPrecedingSeparator(byte value)
        {
            int pos = _pos;
            if (pos + 1 >= Capacity)
            {
                // Append the separator if there's enough space
                if (pos < Capacity)
                {
                    _buffer[pos] = (byte)'/';
                    _pos = pos + 1;
                }

                return ResultFs.TooLongPath.Log();
            }

            _buffer[pos] = (byte)'/';
            _buffer[pos + 1] = value;
            _pos = pos + 2;
            return Result.Success;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result GoUpLevels(int count)
        {
            Debug.Assert(count > 0);

            int separators = 0;
            int pos = _pos - 1;

            for (; pos >= 0; pos--)
            {
                if (PathTools.IsDirectorySeparator(_buffer[pos]))
                {
                    separators++;

                    if (separators == count) break;
                }
            }

            if (separators != count) return ResultFs.DirectoryUnobtainable.Log();

            _pos = pos;
            return Result.Success;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Terminate()
        {
            if (_buffer.Length > _pos)
            {
                _buffer[_pos] = 0;
            }
        }

        public override string ToString()
        {
            return StringUtils.Utf8ZToString(_buffer.Slice(0, Length));
        }
    }
}
