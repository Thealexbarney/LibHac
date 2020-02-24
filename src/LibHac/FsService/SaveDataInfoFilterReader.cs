using System;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Ncm;

namespace LibHac.FsService
{
    internal class SaveDataInfoFilterReader : ISaveDataInfoReader
    {
        private ISaveDataInfoReader Reader { get; }
        private SaveDataFilterInternal Filter { get; }

        public SaveDataInfoFilterReader(ISaveDataInfoReader reader, ref SaveDataFilterInternal filter)
        {
            Reader = reader;
            Filter = filter;
        }

        public Result ReadSaveDataInfo(out long readCount, Span<byte> saveDataInfoBuffer)
        {
            readCount = default;

            Span<SaveDataInfo> outInfo = MemoryMarshal.Cast<byte, SaveDataInfo>(saveDataInfoBuffer);

            SaveDataInfo tempInfo = default;
            Span<byte> tempInfoBytes = SpanHelpers.AsByteSpan(ref tempInfo);

            int count = 0;

            while (count < outInfo.Length)
            {
                Result rc = Reader.ReadSaveDataInfo(out long baseReadCount, tempInfoBytes);
                if (rc.IsFailure()) return rc;

                if (baseReadCount == 0) break;

                if (Filter.Matches(ref tempInfo))
                {
                    outInfo[count] = tempInfo;

                    count++;
                }
            }

            readCount = count;

            return Result.Success;
        }

        public void Dispose()
        {
            Reader?.Dispose();
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x50)]
    internal struct SaveDataFilterInternal
    {
        [FieldOffset(0x00)] public bool FilterBySaveDataSpaceId;
        [FieldOffset(0x01)] public SaveDataSpaceId SpaceId;

        [FieldOffset(0x08)] public bool FilterByProgramId;
        [FieldOffset(0x10)] public TitleId ProgramId;

        [FieldOffset(0x18)] public bool FilterBySaveDataType;
        [FieldOffset(0x19)] public SaveDataType SaveDataType;

        [FieldOffset(0x20)] public bool FilterByUserId;
        [FieldOffset(0x28)] public UserId UserId;

        [FieldOffset(0x38)] public bool FilterBySaveDataId;
        [FieldOffset(0x40)] public ulong SaveDataId;

        [FieldOffset(0x48)] public bool FilterByIndex;
        [FieldOffset(0x4A)] public short Index;

        [FieldOffset(0x4C)] public int Rank;

        public SaveDataFilterInternal(ref SaveDataFilter filter, SaveDataSpaceId spaceId)
        {
            this = default;

            FilterBySaveDataSpaceId = true;
            SpaceId = spaceId;

            Rank = (int)filter.Rank;

            if (filter.FilterByProgramId)
            {
                FilterByProgramId = true;
                ProgramId = filter.ProgramId;
            }

            if (filter.FilterBySaveDataType)
            {
                FilterBySaveDataType = true;
                SaveDataType = filter.SaveDataType;
            }

            if (filter.FilterByUserId)
            {
                FilterByUserId = true;
                UserId = filter.UserId;
            }

            if (filter.FilterBySaveDataId)
            {
                FilterBySaveDataId = true;
                SaveDataId = filter.SaveDataId;
            }

            if (filter.FilterByIndex)
            {
                FilterByIndex = true;
                Index = filter.Index;
            }
        }

        public void SetSaveDataSpaceId(SaveDataSpaceId spaceId)
        {
            FilterBySaveDataSpaceId = true;
            SpaceId = spaceId;
        }

        public void SetProgramId(TitleId value)
        {
            FilterByProgramId = true;
            ProgramId = value;
        }

        public void SetSaveDataType(SaveDataType value)
        {
            FilterBySaveDataType = true;
            SaveDataType = value;
        }

        public void SetUserId(UserId value)
        {
            FilterByUserId = true;
            UserId = value;
        }

        public void SetSaveDataId(ulong value)
        {
            FilterBySaveDataId = true;
            SaveDataId = value;
        }

        public void SetIndex(short value)
        {
            FilterByIndex = true;
            Index = value;
        }

        public bool Matches(ref SaveDataInfo info)
        {
            if (FilterBySaveDataSpaceId && info.SpaceId != SpaceId)
            {
                return false;
            }

            if (FilterByProgramId && info.TitleId != ProgramId)
            {
                return false;
            }

            if (FilterBySaveDataType && info.Type != SaveDataType)
            {
                return false;
            }

            if (FilterByUserId && info.UserId != UserId)
            {
                return false;
            }

            if (FilterBySaveDataId && info.SaveDataId != SaveDataId)
            {
                return false;
            }

            if (FilterByIndex && info.Index != Index)
            {
                return false;
            }

            var filterRank = (SaveDataRank)(Rank & 1);

            // When filtering by secondary rank, match on both primary and secondary ranks
            if (filterRank == SaveDataRank.Primary && info.Rank == SaveDataRank.Secondary)
            {
                return false;
            }

            return true;
        }
    }
}
