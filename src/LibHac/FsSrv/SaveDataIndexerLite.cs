using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Os;
using LibHac.Sf;
using LibHac.Util;

namespace LibHac.FsSrv;

/// <summary>
/// Iterates through all the save data indexed in a <see cref="SaveDataIndexerLite"/>.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
internal class SaveDataIndexerLiteInfoReader : SaveDataInfoReaderImpl
{
    private bool _finishedIterating;
    private SaveDataInfo _info;

    public SaveDataIndexerLiteInfoReader()
    {
        _finishedIterating = true;
        _info = default;
    }

    public void Dispose() { }

    public SaveDataIndexerLiteInfoReader(in SaveDataAttribute key, in SaveDataIndexerValue value)
    {
        _finishedIterating = false;
        _info = default;

        // Don't set the State, Index, or Rank of the returned SaveDataInfo
        _info.SaveDataId = value.SaveDataId;
        _info.SpaceId = value.SpaceId;
        _info.Size = value.Size;
        _info.StaticSaveDataId = key.StaticSaveDataId;
        _info.ProgramId = key.ProgramId;
        _info.Type = key.Type;
        _info.UserId = key.UserId;
    }

    public Result Read(out long readCount, OutBuffer saveDataInfoBuffer)
    {
        UnsafeHelpers.SkipParamInit(out readCount);

        if (_finishedIterating || saveDataInfoBuffer.Size == 0)
        {
            readCount = 0;
            return Result.Success;
        }

        if (saveDataInfoBuffer.Size < Unsafe.SizeOf<SaveDataInfo>())
            return ResultFs.InvalidSize.Log();

        Unsafe.As<byte, SaveDataInfo>(ref MemoryMarshal.GetReference(saveDataInfoBuffer.Buffer)) = _info;
        readCount = 1;
        _finishedIterating = true;

        return Result.Success;
    }
}

/// <summary>
/// Indexes metadata for temporary save data, holding a key-value pair of types
/// <see cref="SaveDataAttribute"/> and <see cref="SaveDataIndexerValue"/> respectively. 
/// </summary>
/// <remarks>
/// Only one temporary save data may exist at a time. When a new
/// save data is added to the index, the existing key-value pair is replaced.
/// <para>Based on nnSdk 13.4.0 (FS 13.1.0)</para>
/// </remarks>
public class SaveDataIndexerLite : ISaveDataIndexer
{
    private SdkMutex _mutex;
    private ulong _nextSaveDataId;
    private Optional<SaveDataAttribute> _key;
    private SaveDataIndexerValue _value;

    public SaveDataIndexerLite()
    {
        _mutex = new SdkMutex();
        _nextSaveDataId = 0x4000000000000000;
    }

    public void Dispose() { }

    public Result Commit()
    {
        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        return Result.Success;
    }

    public Result Rollback()
    {
        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        return Result.Success;
    }

    public Result Reset()
    {
        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        _key.Clear();
        return Result.Success;
    }

    public Result Publish(out ulong saveDataId, in SaveDataAttribute key)
    {
        UnsafeHelpers.SkipParamInit(out saveDataId);

        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        if (_key.HasValue && key == _key.ValueRo)
            return ResultFs.AlreadyExists.Log();

        _key.Set(in key);

        saveDataId = _nextSaveDataId;
        _value = new SaveDataIndexerValue { SaveDataId = _nextSaveDataId };
        _nextSaveDataId++;

        return Result.Success;
    }

    public Result Get(out SaveDataIndexerValue value, in SaveDataAttribute key)
    {
        UnsafeHelpers.SkipParamInit(out value);

        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        if (_key.HasValue && key == _key.ValueRo)
        {
            value = _value;
            return Result.Success;
        }

        return ResultFs.TargetNotFound.Log();
    }

    public Result PutStaticSaveDataIdIndex(in SaveDataAttribute key)
    {
        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        if (_key.HasValue && key == _key.ValueRo)
            return ResultFs.AlreadyExists.Log();

        _key.Set(in key);
        _value = default;

        return Result.Success;
    }

    public bool IsRemainedReservedOnly()
    {
        return false;
    }

    public Result Delete(ulong saveDataId)
    {
        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        if (_key.HasValue && saveDataId == _value.SaveDataId)
        {
            _key.Clear();
            return Result.Success;
        }

        return ResultFs.TargetNotFound.Log();
    }

    public Result SetSpaceId(ulong saveDataId, SaveDataSpaceId spaceId)
    {
        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        if (_key.HasValue && saveDataId == _value.SaveDataId)
        {
            _value.SpaceId = spaceId;
            return Result.Success;
        }

        return ResultFs.TargetNotFound.Log();
    }

    public Result SetSize(ulong saveDataId, long size)
    {
        // Note: Nintendo doesn't lock in this function for some reason
        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        if (_key.HasValue && saveDataId == _value.SaveDataId)
        {
            _value.Size = size;
            return Result.Success;
        }

        return ResultFs.TargetNotFound.Log();
    }

    public Result SetState(ulong saveDataId, SaveDataState state)
    {
        // Note: Nintendo doesn't lock in this function for some reason
        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        if (_key.HasValue && saveDataId == _value.SaveDataId)
        {
            _value.State = state;
            return Result.Success;
        }

        return ResultFs.TargetNotFound.Log();
    }

    public Result GetKey(out SaveDataAttribute key, ulong saveDataId)
    {
        UnsafeHelpers.SkipParamInit(out key);

        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        if (_key.HasValue && saveDataId == _value.SaveDataId)
        {
            key = _key.ValueRo;
            return Result.Success;
        }

        return ResultFs.TargetNotFound.Log();
    }

    public Result GetValue(out SaveDataIndexerValue value, ulong saveDataId)
    {
        UnsafeHelpers.SkipParamInit(out value);

        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        if (_key.HasValue && saveDataId == _value.SaveDataId)
        {
            value = _value;
            return Result.Success;
        }

        return ResultFs.TargetNotFound.Log();
    }

    public Result SetValue(in SaveDataAttribute key, in SaveDataIndexerValue value)
    {
        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        if (_key.HasValue && _key.ValueRo == key)
        {
            _value = value;
            return Result.Success;
        }

        return ResultFs.TargetNotFound.Log();
    }

    public int GetIndexCount()
    {
        return 1;
    }

    public Result OpenSaveDataInfoReader(ref SharedRef<SaveDataInfoReaderImpl> outInfoReader)
    {
        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        if (_key.HasValue)
        {
            outInfoReader.Reset(new SaveDataIndexerLiteInfoReader(in _key.Value, in _value));
        }
        else
        {
            outInfoReader.Reset(new SaveDataIndexerLiteInfoReader());
        }

        return Result.Success;
    }
}