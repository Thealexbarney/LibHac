using System;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;
using IFileSf = LibHac.FsSrv.Sf.IFile;

namespace LibHac.FsSrv.Impl
{
    public interface ISaveDataTransferCoreInterface : IDisposable
    {
        Result GetFreeSpaceSizeForSaveData(out long freeSpaceSize, SaveDataSpaceId spaceId);
        Result QuerySaveDataTotalSize(out long totalSize, long dataSize, long journalSize);
        Result CheckSaveDataFile(long saveDataId, SaveDataSpaceId spaceId);
        Result CreateSaveDataFileSystemCore(in SaveDataAttribute attribute, in SaveDataCreationInfo creationInfo, in SaveDataMetaInfo metaInfo, in Optional<HashSalt> hashSalt, bool leaveUnfinalized);
        Result GetSaveDataInfo(out SaveDataInfo saveInfo, SaveDataSpaceId spaceId, in SaveDataAttribute attribute);
        Result ReadSaveDataFileSystemExtraDataCore(out SaveDataExtraData extraData, SaveDataSpaceId spaceId, ulong saveDataId, bool isTemporarySaveData);
        Result WriteSaveDataFileSystemExtraDataCore(SaveDataSpaceId spaceId, ulong saveDataId, in SaveDataExtraData extraData, SaveDataType type, bool updateTimeStamp);
        Result FinalizeSaveDataCreation(ulong saveDataId, SaveDataSpaceId spaceId);
        Result CancelSaveDataCreation(ulong saveDataId, SaveDataSpaceId spaceId);
        Result OpenSaveDataFile(out ReferenceCountedDisposable<IFileSf> file, SaveDataSpaceId spaceId, in SaveDataAttribute attribute, SaveDataMetaType metaType);
        Result OpenSaveDataMetaFileRaw(out ReferenceCountedDisposable<IFile> file, SaveDataSpaceId spaceId, ulong saveDataId, SaveDataMetaType metaType, OpenMode mode);
        Result OpenSaveDataInternalStorageFileSystemCore(out ReferenceCountedDisposable<IFileSystem> fileSystem, SaveDataSpaceId spaceId, ulong saveDataId, bool useSecondMacKey);
        Result ExtendSaveDataFileSystem(SaveDataSpaceId spaceId, ulong saveDataId, long dataSize, long journalSize);
        Result DeleteSaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId);
        Result SwapSaveDataKeyAndState(SaveDataSpaceId spaceId, ulong saveDataId1, ulong saveDataId2);
        Result SetSaveDataState(SaveDataSpaceId spaceId, ulong saveDataId, SaveDataState state);
        Result SetSaveDataRank(SaveDataSpaceId spaceId, ulong saveDataId, SaveDataRank rank);
        Result OpenSaveDataIndexerAccessor(out SaveDataIndexerAccessor accessor, SaveDataSpaceId spaceId);
    }
}
