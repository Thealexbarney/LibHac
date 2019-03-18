using System;
using System.IO;

namespace LibHac.IO
{
    public class SubStorage : StorageBase
    {
        private IStorage BaseStorage { get; }
        private long Offset { get; }
        private FileAccess Access { get; } = FileAccess.ReadWrite;
        private long _length;

        public SubStorage(IStorage baseStorage, long offset, long length)
        {
            BaseStorage = baseStorage;
            Offset = offset;
            _length = length;
        }

        public SubStorage(SubStorage baseStorage, long offset, long length)
        {
            BaseStorage = baseStorage.BaseStorage;
            Offset = baseStorage.Offset + offset;
            _length = length;
        }

        public SubStorage(IStorage baseStorage, long offset, long length, bool leaveOpen)
            : this(baseStorage, offset, length)
        {
            if (!leaveOpen) ToDispose.Add(BaseStorage);
        }

        public SubStorage(IStorage baseStorage, long offset, long length, bool leaveOpen, FileAccess access)
            : this(baseStorage, offset, length, leaveOpen)
        {
            Access = access;
        }

        protected override void ReadImpl(Span<byte> destination, long offset)
        {
            if ((Access & FileAccess.Read) == 0) throw new InvalidOperationException("Storage is not readable");
            BaseStorage.Read(destination, offset + Offset);
        }

        protected override void WriteImpl(ReadOnlySpan<byte> source, long offset)
        {
            if ((Access & FileAccess.Write) == 0) throw new InvalidOperationException("Storage is not writable");
            BaseStorage.Write(source, offset + Offset);
        }

        public override void Flush()
        {
            BaseStorage.Flush();
        }

        public override long GetSize() => _length;
    }
}
