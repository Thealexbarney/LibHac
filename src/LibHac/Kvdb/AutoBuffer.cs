using System;
using Buffer = LibHac.Mem.Buffer;

namespace LibHac.Kvdb;

internal struct AutoBuffer : IDisposable
{
    private const int Alignment = 0x10;

    private MemoryResource _memoryResource;
    private Buffer _buffer;

    public Span<byte> Get() => _buffer.Span;
    public int GetSize() => _buffer.Length;

    public Result Initialize(long size, MemoryResource memoryResource)
    {
        Buffer buffer = memoryResource.Allocate(size, Alignment);
        if (buffer.IsNull)
            return ResultKvdb.AllocationFailed.Log();

        _memoryResource = memoryResource;
        _buffer = buffer;

        return Result.Success;
    }

    public void Dispose()
    {
        if (!_buffer.IsNull)
        {
            _memoryResource.Deallocate(ref _buffer, Alignment);
        }
    }
}
