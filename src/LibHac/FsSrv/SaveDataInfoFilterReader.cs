using System;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Sf;
using LibHac.Ncm;
using LibHac.Sf;
using LibHac.Util;

namespace LibHac.FsSrv
{
    internal class SaveDataInfoFilterReader : SaveDataInfoReaderImpl, ISaveDataInfoReader
    {
        private ReferenceCountedDisposable<SaveDataInfoReaderImpl> Reader { get; }
        private SaveDataInfoFilter InfoFilter { get; }

        public SaveDataInfoFilterReader(ReferenceCountedDisposable<SaveDataInfoReaderImpl> reader,
            in SaveDataInfoFilter infoFilter)
        {
            Reader = reader.AddReference();
            InfoFilter = infoFilter;
        }

        public Result Read(out long readCount, OutBuffer saveDataInfoBuffer)
        {
            UnsafeHelpers.SkipParamInit(out readCount);

            Span<SaveDataInfo> outInfo = MemoryMarshal.Cast<byte, SaveDataInfo>(saveDataInfoBuffer.Buffer);

            SaveDataInfo tempInfo = default;
            Span<byte> tempInfoBytes = SpanHelpers.AsByteSpan(ref tempInfo);

            SaveDataInfoReaderImpl reader = Reader.Target;
            int count = 0;

            while (count < outInfo.Length)
            {
                Result rc = reader.Read(out long baseReadCount, new OutBuffer(tempInfoBytes));
                if (rc.IsFailure()) return rc;

                if (baseReadCount == 0) break;

                if (InfoFilter.Includes(in tempInfo))
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

    internal struct SaveDataInfoFilter
    {
        public Optional<SaveDataSpaceId> SpaceId;
        public Optional<ProgramId> ProgramId;
        public Optional<SaveDataType> SaveDataType;
        public Optional<UserId> UserId;
        public Optional<ulong> SaveDataId;
        public Optional<ushort> Index;
        public int Rank;

        public SaveDataInfoFilter(in SaveDataInfoFilter filter)
        {
            this = filter;
        }

        public SaveDataInfoFilter(SaveDataSpaceId spaceId, in SaveDataFilter filter)
        {
            // Start out with no optional values
            this = new SaveDataInfoFilter();

            SpaceId = new Optional<SaveDataSpaceId>(spaceId);
            Rank = (int)filter.Rank;

            if (filter.FilterByProgramId)
            {
                ProgramId = new Optional<ProgramId>(in filter.Attribute.ProgramId);
            }

            if (filter.FilterBySaveDataType)
            {
                SaveDataType = new Optional<SaveDataType>(in filter.Attribute.Type);
            }

            if (filter.FilterByUserId)
            {
                UserId = new Optional<UserId>(in filter.Attribute.UserId);
            }

            if (filter.FilterBySaveDataId)
            {
                SaveDataId = new Optional<ulong>(in filter.Attribute.StaticSaveDataId);
            }

            if (filter.FilterByIndex)
            {
                Index = new Optional<ushort>(in filter.Attribute.Index);
            }
        }

        public SaveDataInfoFilter(Optional<SaveDataSpaceId> spaceId, Optional<ProgramId> programId,
            Optional<SaveDataType> saveDataType, Optional<UserId> userId, Optional<ulong> saveDataId,
            Optional<ushort> index, int rank)
        {
            SpaceId = spaceId;
            ProgramId = programId;
            SaveDataType = saveDataType;
            UserId = userId;
            SaveDataId = saveDataId;
            Index = index;
            Rank = rank;
        }

        public bool Includes(in SaveDataInfo saveInfo)
        {
            if (SpaceId.HasValue && saveInfo.SpaceId != SpaceId.Value)
            {
                return false;
            }

            if (ProgramId.HasValue && saveInfo.ProgramId != ProgramId.Value)
            {
                return false;
            }

            if (SaveDataType.HasValue && saveInfo.Type != SaveDataType.Value)
            {
                return false;
            }

            if (UserId.HasValue && saveInfo.UserId != UserId.Value)
            {
                return false;
            }

            if (SaveDataId.HasValue && saveInfo.SaveDataId != SaveDataId.Value)
            {
                return false;
            }

            if (Index.HasValue && saveInfo.Index != Index.Value)
            {
                return false;
            }

            var filterRank = (SaveDataRank)(Rank & 1);

            // When filtering by secondary rank, match on both primary and secondary ranks
            if (filterRank == SaveDataRank.Primary && saveInfo.Rank == SaveDataRank.Secondary)
            {
                return false;
            }

            return true;
        }
    }
}
