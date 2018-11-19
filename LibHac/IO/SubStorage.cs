using System;
using System.IO;

namespace LibHac.IO
{
    public class SubStorage : Storage
    {
        private IStorage BaseStorage { get; }
        private long Offset { get; }
        public override long Length { get; }

        public SubStorage(IStorage baseStorage, long offset, long length)
        {
            BaseStorage = baseStorage;
            Offset = offset;
            Length = length;
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
            BaseStorage.Read(destination, offset + Offset);
        }

        protected override void WriteImpl(ReadOnlySpan<byte> source, long offset)
        {
            BaseStorage.Write(source, offset + Offset);
        }

        public override void Flush()
        {
            BaseStorage.Flush();
        }

        public override Storage Slice(long start, long length, bool leaveOpen)
        {
            Storage storage = BaseStorage.Slice(Offset + start, length, true);
            if (!leaveOpen) storage.ToDispose.Add(this);

            return storage;
        }
    }
}
