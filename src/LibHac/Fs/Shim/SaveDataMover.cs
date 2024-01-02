// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Sf;

namespace LibHac.Fs.Shim;

public interface ISaveDataMover : IDisposable
{
    Result Register(ulong saveDataId);
    Result Process(out long outRemainingSize, long sizeToProcess);
    Result Cancel();
}

file class SaveDataMoverImpl : ISaveDataMover
{
    private SharedRef<FsSrv.Sf.ISaveDataMover> _baseInterface;

    public SaveDataMoverImpl(SharedRef<FsSrv.Sf.ISaveDataMover> baseInterface)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result Register(ulong saveDataId)
    {
        throw new NotImplementedException();
    }

    public Result Process(out long outRemainingSize, long sizeToProcess)
    {
        throw new NotImplementedException();
    }

    public Result Cancel()
    {
        throw new NotImplementedException();
    }
}

public static class SaveDataMover
{
    public static Result OpenSaveDataMover(this FileSystemClient fs, ref UniqueRef<ISaveDataMover> outMover,
        SaveDataSpaceId sourceSpaceId, SaveDataSpaceId destinationSpaceId, NativeHandle workBufferHandle,
        ulong workBufferSize)
    {
        throw new NotImplementedException();
    }
}

public struct SizeCalculatorForSaveDataMover
{
    private long _currentSize;

    public void Add(in SaveDataInfo saveDataInfo)
    {
        throw new NotImplementedException();
    }

    public long GetTotalSize()
    {
        throw new NotImplementedException();
    }

    public static Result GetFreeSpaceSize(FileSystemClient fs, out long outFreeSpaceSize, SaveDataSpaceId spaceId)
    {
        throw new NotImplementedException();
    }
}