using System;

namespace LibHac.Kvdb
{
    internal struct AutoBuffer : IDisposable
    {
        private const int Alignment = 0x10;

        private MemoryResource _memoryResource;
        private MemoryResource.Buffer _buffer;

        public Span<byte> Get() => _buffer.Get();
        public int GetSize() => _buffer.Length;

        public Result Initialize(long size, MemoryResource memoryResource)
        {
            MemoryResource.Buffer buffer = memoryResource.Allocate(size, Alignment);
            if (!buffer.IsValid)
                return ResultKvdb.AllocationFailed.Log();

            _memoryResource = memoryResource;
            _buffer = buffer;

            return Result.Success;
        }

        public void Dispose()
        {
            if (_buffer.IsValid)
            {
                _memoryResource.Deallocate(ref _buffer, Alignment);
            }
        }
    }
}
