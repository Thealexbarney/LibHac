using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem
{
    public class FileReader
    {
        private const int BufferSize = 0x10;
        private IFile _file;
        private byte[] _buffer;
        private long _start;

        public long Position { get; set; }

        public FileReader(IFile file)
        {
            _file = file;
            _buffer = new byte[BufferSize];
        }

        public FileReader(IFile file, long start)
        {
            _file = file;
            _start = start;
            _buffer = new byte[BufferSize];
        }

        private void FillBuffer(long offset, int count, bool updatePosition)
        {
            Debug.Assert(count <= BufferSize);

            _file.Read(out long _, _start + offset, _buffer.AsSpan(0, count)).ThrowIfFailure();
            if (updatePosition) Position = offset + count;
        }

        public byte ReadUInt8(long offset, bool updatePosition)
        {
            FillBuffer(offset, sizeof(byte), updatePosition);

            return _buffer[0];
        }

        public sbyte ReadInt8(long offset, bool updatePosition)
        {
            FillBuffer(offset, sizeof(sbyte), updatePosition);

            return (sbyte)_buffer[0];
        }

        public ushort ReadUInt16(long offset, bool updatePosition)
        {
            FillBuffer(offset, sizeof(ushort), updatePosition);

            return MemoryMarshal.Read<ushort>(_buffer);
        }

        public short ReadInt16(long offset, bool updatePosition)
        {
            FillBuffer(offset, sizeof(short), updatePosition);

            return MemoryMarshal.Read<short>(_buffer);
        }

        public int ReadUInt24(long offset, bool updatePosition)
        {
            FillBuffer(offset, 3, updatePosition);

            return MemoryMarshal.Read<int>(_buffer) & 0xFFFFFF;
        }

        public int ReadInt24(long offset, bool updatePosition)
        {
            FillBuffer(offset, 3, updatePosition);

            return BitTools.SignExtend32(MemoryMarshal.Read<int>(_buffer), 24);
        }

        public uint ReadUInt32(long offset, bool updatePosition)
        {
            FillBuffer(offset, sizeof(uint), updatePosition);

            return MemoryMarshal.Read<uint>(_buffer);
        }

        public int ReadInt32(long offset, bool updatePosition)
        {
            FillBuffer(offset, sizeof(int), updatePosition);

            return MemoryMarshal.Read<int>(_buffer);
        }

        public ulong ReadUInt64(long offset, bool updatePosition)
        {
            FillBuffer(offset, sizeof(ulong), updatePosition);

            return MemoryMarshal.Read<ulong>(_buffer);
        }

        public long ReadInt64(long offset, bool updatePosition)
        {
            FillBuffer(offset, sizeof(long), updatePosition);

            return MemoryMarshal.Read<long>(_buffer);
        }

        public float ReadSingle(long offset, bool updatePosition)
        {
            FillBuffer(offset, sizeof(float), updatePosition);

            return MemoryMarshal.Read<float>(_buffer);
        }

        public double ReadDouble(long offset, bool updatePosition)
        {
            FillBuffer(offset, sizeof(double), updatePosition);

            return MemoryMarshal.Read<double>(_buffer);
        }

        public byte[] ReadBytes(long offset, int length, bool updatePosition)
        {
            byte[] bytes = new byte[length];
            _file.Read(out long _, offset, bytes).ThrowIfFailure();

            if (updatePosition) Position = offset + length;
            return bytes;
        }

        public void ReadBytes(Span<byte> destination, long offset, bool updatePosition)
        {
            _file.Read(out long _, offset, destination).ThrowIfFailure();

            if (updatePosition) Position = offset + destination.Length;
        }

        public string ReadAscii(long offset, int length, bool updatePosition)
        {
            byte[] bytes = new byte[length];
            _file.Read(out long _, offset, bytes).ThrowIfFailure();

            if (updatePosition) Position = offset + length;
            return Encoding.ASCII.GetString(bytes);
        }

        public byte ReadUInt8(long offset) => ReadUInt8(offset, true);
        public sbyte ReadInt8(long offset) => ReadInt8(offset, true);
        public ushort ReadUInt16(long offset) => ReadUInt16(offset, true);
        public short ReadInt16(long offset) => ReadInt16(offset, true);
        public int ReadUInt24(long offset) => ReadUInt24(offset, true);
        public int ReadInt24(long offset) => ReadInt24(offset, true);
        public uint ReadUInt32(long offset) => ReadUInt32(offset, true);
        public int ReadInt32(long offset) => ReadInt32(offset, true);
        public ulong ReadUInt64(long offset) => ReadUInt64(offset, true);
        public long ReadInt64(long offset) => ReadInt64(offset, true);
        public float ReadSingle(long offset) => ReadSingle(offset, true);
        public double ReadDouble(long offset) => ReadDouble(offset, true);
        public byte[] ReadBytes(long offset, int length) => ReadBytes(offset, length, true);
        public void ReadBytes(Span<byte> destination, long offset) => ReadBytes(destination, offset, true);
        public string ReadAscii(long offset, int length) => ReadAscii(offset, length, true);

        public byte ReadUInt8() => ReadUInt8(Position, true);
        public sbyte ReadInt8() => ReadInt8(Position, true);
        public ushort ReadUInt16() => ReadUInt16(Position, true);
        public short ReadInt16() => ReadInt16(Position, true);
        public int ReadUInt24() => ReadUInt24(Position, true);
        public int ReadInt24() => ReadInt24(Position, true);
        public uint ReadUInt32() => ReadUInt32(Position, true);
        public int ReadInt32() => ReadInt32(Position, true);
        public ulong ReadUInt64() => ReadUInt64(Position, true);
        public long ReadInt64() => ReadInt64(Position, true);
        public float ReadSingle() => ReadSingle(Position, true);
        public double ReadDouble() => ReadDouble(Position, true);
        public byte[] ReadBytes(int length) => ReadBytes(Position, length, true);
        public void ReadBytes(Span<byte> destination) => ReadBytes(destination, Position, true);
        public string ReadAscii(int length) => ReadAscii(Position, length, true);
    }
}
