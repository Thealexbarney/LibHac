using System;
using System.Runtime.CompilerServices;

namespace LibHac.Kvdb
{
    public ref struct ImkvdbWriter
    {
        private Span<byte> _data;
        private int _position;

        public ImkvdbWriter(Span<byte> data)
        {
            _data = data;
            _position = 0;
        }

        public void WriteHeader(int entryCount)
        {
            if (_position + Unsafe.SizeOf<ImkvdbHeader>() > _data.Length) throw new InvalidOperationException();

            ref ImkvdbHeader header = ref Unsafe.As<byte, ImkvdbHeader>(ref _data[_position]);

            header.Magic = ImkvdbHeader.ExpectedMagic;
            header.Reserved = 0;
            header.EntryCount = entryCount;

            _position += Unsafe.SizeOf<ImkvdbHeader>();
        }

        public void WriteEntry(IExportable key, IExportable value)
        {
            WriteEntryHeader(key.ExportSize, value.ExportSize);
            Write(key);
            Write(value);
        }

        private void WriteEntryHeader(int keySize, int valueSize)
        {
            if (_position + Unsafe.SizeOf<ImkvdbEntryHeader>() > _data.Length) throw new InvalidOperationException();

            ref ImkvdbEntryHeader header = ref Unsafe.As<byte, ImkvdbEntryHeader>(ref _data[_position]);

            header.Magic = ImkvdbEntryHeader.ExpectedMagic;
            header.KeySize = keySize;
            header.ValueSize = valueSize;

            _position += Unsafe.SizeOf<ImkvdbEntryHeader>();
        }

        private void Write(IExportable value)
        {
            int valueSize = value.ExportSize;
            if (_position + valueSize > _data.Length) throw new InvalidOperationException();

            Span<byte> dest = _data.Slice(_position, valueSize);
            value.ToBytes(dest);

            _position += valueSize;
        }
    }
}
