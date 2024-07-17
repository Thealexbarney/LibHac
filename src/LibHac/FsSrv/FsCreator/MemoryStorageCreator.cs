using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Os;

namespace LibHac.FsSrv.FsCreator;

/// <summary>
/// Creates <see cref="MemoryStorage"/>s from registered memory buffers.
/// </summary>
/// <remarks>
/// <para>Used for in-memory System and User partitions when booting in safe mode.
/// On startup, FS registers buffers which can be used later if needed.</para>
/// <para>Based on nnSdk 18.3.0 (FS 18.0.0)</para>
/// </remarks>
public class MemoryStorageCreator : IMemoryStorageCreator
{
    private SdkMutexType _mutex;
    private Array4<Buffer> _bufferArray;

    private struct Buffer
    {
        public Memory<byte> MemoryBuffer;
        public bool IsInUse; // Each registered buffer can only be used to create a MemoryStorage a single time

        public Buffer()
        {
            MemoryBuffer = default;
            IsInUse = false;
        }
    }

    public MemoryStorageCreator()
    {
        _mutex = new SdkMutexType();

        for (int i = 0; i < _bufferArray.Length; i++)
        {
            _bufferArray[i] = new Buffer();
        }
    }

    public Result Create(ref SharedRef<IStorage> outStorage, out Memory<byte> outBuffer, IMemoryStorageCreator.MemoryStorageId id)
    {
        UnsafeHelpers.SkipParamInit(out outBuffer);

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        ref Buffer buffer = ref _bufferArray[(int)id];

        if (buffer.IsInUse)
            return ResultFs.AllocationMemoryFailed.Log();

        if (buffer.MemoryBuffer.IsEmpty)
            return ResultFs.AllocationMemoryFailed.Log();

        using var storage = new SharedRef<MemoryStorageFromMemory>(new MemoryStorageFromMemory(buffer.MemoryBuffer));
        buffer.MemoryBuffer.Span.Clear();

        outStorage.SetByMove(ref storage.Ref);
        outBuffer = buffer.MemoryBuffer;
        buffer.IsInUse = true;

        return Result.Success;
    }

    public Result RegisterBuffer(IMemoryStorageCreator.MemoryStorageId id, Memory<byte> buffer)
    {
        Assert.SdkAssert(id < IMemoryStorageCreator.MemoryStorageId.Count);
        Assert.SdkAssert(_bufferArray[(int)id].MemoryBuffer.IsEmpty);

        _bufferArray[(int)id].MemoryBuffer = buffer;

        return Result.Success;
    }
}