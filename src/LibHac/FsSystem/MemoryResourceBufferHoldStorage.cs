using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;

namespace LibHac.FsSystem;

/// <summary>
/// Allocates a buffer from the provided <see cref="MemoryResource"/> that is deallocated
/// when the <see cref="MemoryResourceBufferHoldStorage"/> is disposed.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public class MemoryResourceBufferHoldStorage : IStorage
{
    private SharedRef<IStorage> _storage;
    private MemoryResource _memoryResource;
    private Mem.Buffer _buffer;

    public MemoryResourceBufferHoldStorage(ref readonly SharedRef<IStorage> baseStorage, MemoryResource memoryResource,
        int bufferSize)
    {
        _storage = SharedRef<IStorage>.CreateCopy(in baseStorage);
        _memoryResource = memoryResource;
        _buffer = memoryResource.Allocate(bufferSize);
    }

    public override void Dispose()
    {
        if (!_buffer.IsNull)
        {
            _memoryResource.Deallocate(ref _buffer);
        }

        _storage.Destroy();

        base.Dispose();
    }

    public bool IsValid()
    {
        return !_buffer.IsNull;
    }

    public Mem.Buffer GetBuffer()
    {
        return _buffer;
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        Assert.SdkRequiresNotNull(_storage);

        return _storage.Get.Read(offset, destination);
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        Assert.SdkRequiresNotNull(_storage);

        return _storage.Get.Write(offset, source);
    }

    public override Result Flush()
    {
        Assert.SdkRequiresNotNull(_storage);

        return _storage.Get.Flush();
    }

    public override Result SetSize(long size)
    {
        Assert.SdkRequiresNotNull(_storage);

        return _storage.Get.SetSize(size);
    }

    public override Result GetSize(out long size)
    {
        Assert.SdkRequiresNotNull(_storage);

        return _storage.Get.GetSize(out size);
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        Assert.SdkRequiresNotNull(_storage);

        return _storage.Get.OperateRange(outBuffer, operationId, offset, size, inBuffer);
    }
}