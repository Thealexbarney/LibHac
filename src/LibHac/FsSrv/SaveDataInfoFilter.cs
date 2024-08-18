using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Ncm;
using LibHac.Sf;
using LibHac.Util;

namespace LibHac.FsSrv;

/// <summary>
/// Contains filter parameters for <see cref="SaveDataInfo"/> and can check
/// to see if a <see cref="SaveDataInfo"/> matches those parameters.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
internal struct SaveDataInfoFilter
{
    private Optional<SaveDataSpaceId> _spaceId;
    private Optional<ProgramId> _programId;
    private Optional<SaveDataType> _saveDataType;
    private Optional<UserId> _userId;
    private Optional<ulong> _saveDataId;
    private Optional<ushort> _index;
    private int _rank;

    public SaveDataInfoFilter(in SaveDataInfoFilter filter)
    {
        this = filter;
    }

    public SaveDataInfoFilter(SaveDataSpaceId spaceId, in SaveDataFilter filter)
    {
        // Start out with no optional values
        this = default;

        _spaceId = new Optional<SaveDataSpaceId>(spaceId);
        _rank = (int)filter.Rank;

        if (filter.FilterByProgramId)
        {
            _programId = new Optional<ProgramId>(filter.Attribute.ProgramId);
        }

        if (filter.FilterBySaveDataType)
        {
            _saveDataType = new Optional<SaveDataType>(filter.Attribute.Type);
        }

        if (filter.FilterByUserId)
        {
            _userId = new Optional<UserId>(in filter.Attribute.UserId);
        }

        if (filter.FilterBySaveDataId)
        {
            _saveDataId = new Optional<ulong>(filter.Attribute.StaticSaveDataId);
        }

        if (filter.FilterByIndex)
        {
            _index = new Optional<ushort>(filter.Attribute.Index);
        }
    }

    public SaveDataInfoFilter(Optional<SaveDataSpaceId> spaceId, Optional<ProgramId> programId,
        Optional<SaveDataType> saveDataType, Optional<UserId> userId, Optional<ulong> saveDataId,
        Optional<ushort> index, int rank)
    {
        _spaceId = spaceId;
        _programId = programId;
        _saveDataType = saveDataType;
        _userId = userId;
        _saveDataId = saveDataId;
        _index = index;
        _rank = rank;
    }

    public bool Includes(in SaveDataInfo saveInfo)
    {
        if (_spaceId.HasValue && saveInfo.SpaceId != _spaceId.Value)
            return false;

        if (_programId.HasValue && saveInfo.ProgramId != _programId.Value)
            return false;

        if (_saveDataType.HasValue && saveInfo.Type != _saveDataType.Value)
            return false;

        if (_userId.HasValue && saveInfo.UserId != _userId.Value)
            return false;

        if (_saveDataId.HasValue && saveInfo.SaveDataId != _saveDataId.Value)
            return false;

        if (_index.HasValue && saveInfo.Index != _index.Value)
            return false;

        var filterRank = (SaveDataRank)(_rank & 1);

        // When filtering by secondary rank, match on both primary and secondary ranks
        if (filterRank == SaveDataRank.Primary && saveInfo.Rank == SaveDataRank.Secondary)
            return false;

        return true;
    }
}

/// <summary>
/// Wraps a <see cref="SaveDataInfoReaderImpl"/> and only allows <see cref="SaveDataInfo"/>
/// that match a provided <see cref="SaveDataInfoFilter"/> to be returned.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
internal class SaveDataInfoFilterReader : SaveDataInfoReaderImpl
{
    private SharedRef<SaveDataInfoReaderImpl> _reader;
    private SaveDataInfoFilter _infoFilter;

    public SaveDataInfoFilterReader(ref readonly SharedRef<SaveDataInfoReaderImpl> reader, in SaveDataInfoFilter infoFilter)
    {
        _reader = SharedRef<SaveDataInfoReaderImpl>.CreateCopy(in reader);
        _infoFilter = infoFilter;
    }

    public void Dispose()
    {
        _reader.Destroy();
    }

    [SkipLocalsInit]
    public Result Read(out long readCount, OutBuffer saveDataInfoBuffer)
    {
        UnsafeHelpers.SkipParamInit(out readCount);

        Span<SaveDataInfo> outInfo = saveDataInfoBuffer.AsSpan<SaveDataInfo>();
        int count = 0;

        while (count < outInfo.Length)
        {
            Unsafe.SkipInit(out SaveDataInfo info);
            Result res = _reader.Get.Read(out long baseReadCount, OutBuffer.FromStruct(ref info));
            if (res.IsFailure()) return res.Miss();

            if (baseReadCount == 0) break;

            if (_infoFilter.Includes(in info))
            {
                outInfo[count] = info;
                count++;
            }
        }

        readCount = count;
        return Result.Success;
    }
}