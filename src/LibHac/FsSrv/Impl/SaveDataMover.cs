// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Fs;
using LibHac.FsSrv.Sf;
using LibHac.Os;
using LibHac.Sf;
using LibHac.Util;

namespace LibHac.FsSrv.Impl;

public class SaveDataMover : ISaveDataMover
{
    private enum State
    {
        Initial,
        Registered,
        Copying,
        Finished,
        Fatal,
        Canceled
    }

    private SharedRef<ISaveDataTransferCoreInterface> _transferInterface;
    private Optional<StorageDuplicator> _duplicator;
    private SaveDataSpaceId _sourceSpaceId;
    private Array128<ulong> _sourceSaveIds;
    private SaveDataSpaceId _destinationSpaceId;
    private Array128<ulong> _destinationSaveIds;
    // private TransferMemory _transferMemory;
    private Memory<byte> _workBuffer;
    // private ulong _transferMemorySize;
    private int _saveCount;
    private int _currentSaveIndex;
    private long _remainingSize;
    private State _state;
    private SdkMutex _mutex;

    public SaveDataMover(ref readonly SharedRef<ISaveDataTransferCoreInterface> transferInterface,
        SaveDataSpaceId sourceSpaceId, SaveDataSpaceId destinationSpaceId, NativeHandle transferMemoryHandle,
        ulong transferMemorySize)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    private Result OpenDuplicator()
    {
        throw new NotImplementedException();
    }

    private Result Initialize()
    {
        throw new NotImplementedException();
    }

    public Result Register(ulong saveDataId)
    {
        throw new NotImplementedException();
    }

    public Result Process(out long remainingSize, long sizeToProcess)
    {
        throw new NotImplementedException();
    }

    private Result FinalizeObject()
    {
        throw new NotImplementedException();
    }

    public Result Cancel()
    {
        throw new NotImplementedException();
    }

    private void ChangeStateToFatal()
    {
        throw new NotImplementedException();
    }
}