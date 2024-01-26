using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Os;
using LibHac.Util;

namespace LibHac.FsSrv.Impl;

public interface ISaveDataTransferCoreInterface : IDisposable
{
    Result GetFreeSpaceSizeForSaveData(out long outFreeSpaceSize, SaveDataSpaceId spaceId);
    Result QuerySaveDataTotalSize(out long outTotalSize, long dataSize, long journalSize);
    Result CheckSaveDataFile(long saveDataId, SaveDataSpaceId spaceId);
    Result CreateSaveDataFileSystemCore(in SaveDataAttribute attribute, in SaveDataCreationInfo creationInfo, in SaveDataMetaInfo metaInfo, in Optional<HashSalt> hashSalt, bool leaveUnfinalized);
    Result GetSaveDataInfo(out SaveDataInfo saveInfo, SaveDataSpaceId spaceId, SaveDataAttribute attribute);
    Result ReadSaveDataFileSystemExtraDataCore(out SaveDataExtraData extraData, SaveDataSpaceId spaceId, ulong saveDataId, bool isTemporarySaveData);
    Result WriteSaveDataFileSystemExtraDataCore(SaveDataSpaceId spaceId, ulong saveDataId, in SaveDataExtraData extraData, SaveDataType type, bool updateTimeStamp);
    Result FinalizeSaveDataCreation(ulong saveDataId, SaveDataSpaceId spaceId);
    Result CancelSaveDataCreation(ulong saveDataId, SaveDataSpaceId spaceId);
    Result OpenSaveDataFile(ref SharedRef<IFile> oufFile, SaveDataSpaceId spaceId, ulong saveDataId, OpenMode mode);
    Result OpenSaveDataMetaFileRaw(ref SharedRef<IFile> outFile, SaveDataSpaceId spaceId, ulong saveDataId, SaveDataMetaType metaType, OpenMode mode);
    Result OpenSaveDataInternalStorageFileSystemCore(ref SharedRef<IFileSystem> outFileSystem, SaveDataSpaceId spaceId, ulong saveDataId, bool isTemporaryTransferSave);
    Result OpenSaveDataFileSystemCore(ref SharedRef<IFileSystem> outFileSystem, out ulong outSaveDataId, SaveDataSpaceId spaceId, in SaveDataAttribute attribute, bool openReadOnly, bool cacheExtraData);
    Result ExtendSaveDataFileSystem(SaveDataSpaceId spaceId, ulong saveDataId, long dataSize, long journalSize);
    Result DeleteSaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId);
    Result SwapSaveDataKeyAndState(SaveDataSpaceId spaceId, ulong saveDataId1, ulong saveDataId2);
    Result SetSaveDataState(SaveDataSpaceId spaceId, ulong saveDataId, SaveDataState state);
    Result SetSaveDataRank(SaveDataSpaceId spaceId, ulong saveDataId, SaveDataRank rank);
    Result OpenSaveDataIndexerAccessor(ref UniqueRef<SaveDataIndexerAccessor> outAccessor, SaveDataSpaceId spaceId);
    bool IsProhibited(ref UniqueLock<SdkMutex> outLock, ApplicationId applicationId);
}