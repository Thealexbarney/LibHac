using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Spl;

namespace LibHac.FsService
{
    public class FileSystemProxy : IFileSystemProxy
    {
        private FileSystemProxyCore FsProxyCore { get; }

        /// <summary>The client instance to be used for internal operations like save indexer access.</summary>
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        private FileSystemClient FsClient { get; }

        public long CurrentProcess { get; private set; }

        public long SaveDataSize { get; private set; }
        public long SaveDataJournalSize { get; private set; }
        public FsPath SaveDataRootPath { get; } = default;
        public bool AutoCreateSaveData { get; private set; }

        private const ulong SaveIndexerId = 0x8000000000000000;

        internal FileSystemProxy(FileSystemProxyCore fsProxyCore, FileSystemClient fsClient)
        {
            FsProxyCore = fsProxyCore;
            FsClient = fsClient;

            CurrentProcess = -1;
            SaveDataSize = 0x2000000;
            SaveDataJournalSize = 0x1000000;
            AutoCreateSaveData = true;
        }

        public Result OpenFileSystemWithId(out IFileSystem fileSystem, ref FsPath path, TitleId titleId, FileSystemType type)
        {
            throw new NotImplementedException();
        }

        public Result OpenFileSystemWithPatch(out IFileSystem fileSystem, TitleId titleId, FileSystemType type)
        {
            throw new NotImplementedException();
        }

        public Result SetCurrentProcess(long processId)
        {
            CurrentProcess = processId;

            return Result.Success;
        }

        public Result GetFreeSpaceSizeForSaveData(out long freeSpaceSize, SaveDataSpaceId spaceId)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataFileSystemByCurrentProcess(out IFileSystem fileSystem)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataFileSystemByProgramId(out IFileSystem fileSystem, TitleId titleId)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataStorageByCurrentProcess(out IStorage storage)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataStorageByProgramId(out IStorage storage, TitleId programId)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataStorageByDataId(out IStorage storage, TitleId dataId, StorageId storageId)
        {
            throw new NotImplementedException();
        }

        public Result OpenPatchDataStorageByCurrentProcess(out IStorage storage)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataFileSystemWithProgramIndex(out IFileSystem fileSystem, byte programIndex)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataStorageWithProgramIndex(out IStorage storage, byte programIndex)
        {
            throw new NotImplementedException();
        }

        public Result RegisterSaveDataFileSystemAtomicDeletion(ReadOnlySpan<ulong> saveDataIds)
        {
            throw new NotImplementedException();
        }

        public Result DeleteSaveDataFileSystem(ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result DeleteSaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result DeleteSaveDataFileSystemBySaveDataAttribute(SaveDataSpaceId spaceId, ref SaveDataAttribute2 attribute)
        {
            throw new NotImplementedException();
        }

        public Result UpdateSaveDataMacForDebug(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result CreateSaveDataFileSystem(ref SaveDataAttribute2 attribute, ref SaveDataCreateInfo createInfo,
            ref SaveMetaCreateInfo metaCreateInfo)
        {
            throw new NotImplementedException();
        }

        public Result CreateSaveDataFileSystemWithHashSalt(ref SaveDataAttribute2 attribute, ref SaveDataCreateInfo createInfo,
            ref SaveMetaCreateInfo metaCreateInfo, ref HashSalt hashSalt)
        {
            throw new NotImplementedException();
        }

        public Result CreateSaveDataFileSystemBySystemSaveDataId(ref SaveDataAttribute2 attribute, ref SaveDataCreateInfo createInfo)
        {
            throw new NotImplementedException();
        }

        public Result ExtendSaveDataFileSystem(SaveDataSpaceId spaceId, ulong saveDataId, long dataSize, long journalSize)
        {
            throw new NotImplementedException();
        }

        private Result OpenSaveDataFileSystemImpl(out IFileSystem fileSystem, out ulong saveDataId,
            SaveDataSpaceId spaceId, ref SaveDataAttribute attribute, bool openReadOnly, bool cacheExtraData)
        {
            bool hasFixedId = attribute.SaveId != 0 && attribute.UserId.Id == Id128.InvalidId;

            if (hasFixedId)
            {
                saveDataId = attribute.SaveId;
            }
            else
            {
                throw new NotImplementedException();
            }

            Result saveFsResult = FsProxyCore.OpenSaveDataFileSystem(out fileSystem, spaceId, saveDataId,
                SaveDataRootPath.ToString(), openReadOnly, attribute.Type, cacheExtraData);

            if (saveFsResult.IsSuccess()) return Result.Success;

            if (saveFsResult == ResultFs.PathNotFound || saveFsResult == ResultFs.TargetNotFound) return saveFsResult;

            if (saveDataId != SaveIndexerId)
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (hasFixedId)
                {
                    // todo: remove save indexer entry
                }
            }

            return ResultFs.TargetNotFound;
        }

        public Result OpenSaveDataFileSystem(out IFileSystem fileSystem, SaveDataSpaceId spaceId, ref SaveDataAttribute attribute)
        {
            throw new NotImplementedException();
        }

        public Result OpenReadOnlySaveDataFileSystem(out IFileSystem fileSystem, SaveDataSpaceId spaceId,
            ref SaveDataAttribute attribute)
        {
            throw new NotImplementedException();
        }

        public Result OpenSaveDataFileSystemBySystemSaveDataId(out IFileSystem fileSystem, SaveDataSpaceId spaceId,
            ref SaveDataAttribute attribute)
        {
            // Missing permission check, speed emulation storage type wrapper, and FileSystemInterfaceAdapter
            fileSystem = default;

            if (!IsSystemSaveDataId(attribute.SaveId)) return ResultFs.InvalidArgument.Log();

            Result rc = OpenSaveDataFileSystemImpl(out IFileSystem saveFs, out _, spaceId,
                ref attribute, false, true);
            if (rc.IsFailure()) return rc;

            // Missing check if the current title owns the save data or can open it

            fileSystem = saveFs;

            return Result.Success;
        }

        public Result ReadSaveDataFileSystemExtraData(Span<byte> extraDataBuffer, ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result ReadSaveDataFileSystemExtraDataBySaveDataSpaceId(Span<byte> extraDataBuffer, SaveDataSpaceId spaceId,
            ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result ReadSaveDataFileSystemExtraDataBySaveDataAttribute(Span<byte> extraDataBuffer, SaveDataSpaceId spaceId,
            ref SaveDataAttribute2 attribute)
        {
            throw new NotImplementedException();
        }

        public Result WriteSaveDataFileSystemExtraData(ulong saveDataId, SaveDataSpaceId spaceId, ReadOnlySpan<byte> extraDataBuffer)
        {
            throw new NotImplementedException();
        }

        public Result WriteSaveDataFileSystemExtraDataBySaveDataAttribute(ref SaveDataAttribute2 attribute, SaveDataSpaceId spaceId,
            ReadOnlySpan<byte> extraDataBuffer, ReadOnlySpan<byte> maskBuffer)
        {
            throw new NotImplementedException();
        }

        public Result WriteSaveDataFileSystemExtraDataWithMask(ulong saveDataId, SaveDataSpaceId spaceId, ReadOnlySpan<byte> extraDataBuffer,
            ReadOnlySpan<byte> maskBuffer)
        {
            throw new NotImplementedException();
        }

        public Result OpenImageDirectoryFileSystem(out IFileSystem fileSystem, ImageDirectoryId dirId)
        {
            throw new NotImplementedException();
        }

        public Result SetBisRootForHost(BisPartitionId partitionId, ref FsPath path)
        {
            throw new NotImplementedException();
        }

        public Result OpenBisFileSystem(out IFileSystem fileSystem, ref FsPath rootPath, BisPartitionId partitionId)
        {
            fileSystem = default;

            // Missing permission check, speed emulation storage type wrapper, and FileSystemInterfaceAdapter

            Result rc = PathTools.Normalize(out U8Span normalizedPath, rootPath);
            if (rc.IsFailure()) return rc;

            return FsProxyCore.OpenBisFileSystem(out fileSystem, normalizedPath.ToString(), partitionId);
        }

        public Result OpenBisStorage(out IStorage storage, BisPartitionId partitionId)
        {
            throw new NotImplementedException();
        }

        public Result InvalidateBisCache()
        {
            throw new NotImplementedException();
        }

        public Result OpenHostFileSystem(out IFileSystem fileSystem, ref FsPath subPath)
        {
            throw new NotImplementedException();
        }

        public Result OpenSdCardFileSystem(out IFileSystem fileSystem)
        {
            // Missing permission check, speed emulation storage type wrapper, and FileSystemInterfaceAdapter

            return FsProxyCore.OpenSdCardFileSystem(out fileSystem);
        }

        public Result FormatSdCardFileSystem()
        {
            throw new NotImplementedException();
        }

        public Result FormatSdCardDryRun()
        {
            throw new NotImplementedException();
        }

        public Result IsExFatSupported(out bool isSupported)
        {
            throw new NotImplementedException();
        }

        public Result OpenGameCardStorage(out IStorage storage, GameCardHandle handle, GameCardPartitionRaw partitionId)
        {
            throw new NotImplementedException();
        }

        public Result OpenDeviceOperator(out IDeviceOperator deviceOperator)
        {
            throw new NotImplementedException();
        }

        public Result OpenSaveDataInfoReader(out ISaveDataInfoReader infoReader)
        {
            throw new NotImplementedException();
        }

        public Result OpenSaveDataInfoReaderBySaveDataSpaceId(out ISaveDataInfoReader infoReader, SaveDataSpaceId spaceId)
        {
            throw new NotImplementedException();
        }

        public Result OpenSaveDataInfoReaderWithFilter(out ISaveDataInfoReader infoReader, SaveDataSpaceId spaceId,
            ref SaveDataFilter filter)
        {
            throw new NotImplementedException();
        }

        public Result FindSaveDataWithFilter(out long count, Span<byte> saveDataInfoBuffer, SaveDataSpaceId spaceId,
            ref SaveDataFilter filter)
        {
            throw new NotImplementedException();
        }

        public Result OpenSaveDataInternalStorageFileSystem(out IFileSystem fileSystem, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result QuerySaveDataInternalStorageTotalSize(out long size, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result GetSaveDataCommitId(out long commitId, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result OpenSaveDataInfoReaderOnlyCacheStorage(out ISaveDataInfoReader infoReader)
        {
            throw new NotImplementedException();
        }

        public Result OpenSaveDataMetaFile(out IFile file, SaveDataSpaceId spaceId, ref SaveDataAttribute2 attribute,
            SaveMetaType type)
        {
            throw new NotImplementedException();
        }

        public Result DeleteCacheStorage(short index)
        {
            throw new NotImplementedException();
        }

        public Result GetCacheStorageSize(out long dataSize, out long journalSize, short index)
        {
            throw new NotImplementedException();
        }

        public Result ListAccessibleSaveDataOwnerId(out int readCount, Span<TitleId> idBuffer, TitleId programId, int startIndex,
            int bufferIdCount)
        {
            throw new NotImplementedException();
        }

        public Result SetSaveDataSize(long saveDataSize, long saveDataJournalSize)
        {
            if (saveDataSize < 0 || saveDataJournalSize < 0)
            {
                return ResultFs.InvalidSize;
            }

            SaveDataSize = saveDataSize;
            SaveDataJournalSize = saveDataJournalSize;

            return Result.Success;
        }
        public Result SetSaveDataRootPath(ref FsPath path)
        {
            // Missing permission check

            if (StringUtils.GetLength(path.Str, FsPath.MaxLength + 1) > FsPath.MaxLength)
            {
                return ResultFs.TooLongPath;
            }

            StringUtils.Copy(SaveDataRootPath.Str, path.Str);

            return Result.Success;
        }

        public Result OpenContentStorageFileSystem(out IFileSystem fileSystem, ContentStorageId storageId)
        {
            // Missing permission check, speed emulation storage type wrapper, and FileSystemInterfaceAdapter

            return FsProxyCore.OpenContentStorageFileSystem(out fileSystem, storageId);
        }

        public Result OpenCloudBackupWorkStorageFileSystem(out IFileSystem fileSystem, CloudBackupWorkStorageId storageId)
        {
            throw new NotImplementedException();
        }

        public Result OpenCustomStorageFileSystem(out IFileSystem fileSystem, CustomStorageId storageId)
        {
            // Missing permission check, speed emulation storage type wrapper, and FileSystemInterfaceAdapter

            return FsProxyCore.OpenCustomStorageFileSystem(out fileSystem, storageId);
        }

        public Result OpenGameCardFileSystem(out IFileSystem fileSystem, GameCardHandle handle, GameCardPartition partitionId)
        {
            throw new NotImplementedException();
        }

        public Result QuerySaveDataTotalSize(out long totalSize, long dataSize, long journalSize)
        {
            throw new NotImplementedException();
        }

        public Result SetCurrentPosixTimeWithTimeDifference(long time, int difference)
        {
            throw new NotImplementedException();
        }

        public Result GetRightsId(out RightsId rightsId, TitleId programId, StorageId storageId)
        {
            throw new NotImplementedException();
        }

        public Result GetRightsIdByPath(out RightsId rightsId, ref FsPath path)
        {
            throw new NotImplementedException();
        }

        public Result GetRightsIdAndKeyGenerationByPath(out RightsId rightsId, out byte keyGeneration, ref FsPath path)
        {
            throw new NotImplementedException();
        }

        public Result RegisterExternalKey(ref RightsId rightsId, ref AccessKey externalKey)
        {
            throw new NotImplementedException();
        }

        public Result UnregisterExternalKey(ref RightsId rightsId)
        {
            throw new NotImplementedException();
        }

        public Result UnregisterAllExternalKey()
        {
            throw new NotImplementedException();
        }
        public Result SetSdCardEncryptionSeed(ReadOnlySpan<byte> seed)
        {
            // todo: use struct instead of byte span
            if (seed.Length != 0x10) return ResultFs.InvalidSize;

            // Missing permission check

            Result rc = FsProxyCore.SetSdCardEncryptionSeed(seed);
            if (rc.IsFailure()) return rc;

            // todo: Reset save data indexer

            return Result.Success;
        }

        public Result VerifySaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId, Span<byte> readBuffer)
        {
            throw new NotImplementedException();
        }

        public Result VerifySaveDataFileSystem(ulong saveDataId, Span<byte> readBuffer)
        {
            throw new NotImplementedException();
        }

        public Result CorruptSaveDataFileSystemByOffset(SaveDataSpaceId spaceId, ulong saveDataId, long offset)
        {
            throw new NotImplementedException();
        }

        public Result CorruptSaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result CorruptSaveDataFileSystem(ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result CreatePaddingFile(long size)
        {
            throw new NotImplementedException();
        }

        public Result DeleteAllPaddingFiles()
        {
            throw new NotImplementedException();
        }

        public Result DisableAutoSaveDataCreation()
        {
            AutoCreateSaveData = false;

            return Result.Success;
        }

        public Result SetGlobalAccessLogMode(int mode)
        {
            throw new NotImplementedException();
        }

        public Result GetGlobalAccessLogMode(out int mode)
        {
            throw new NotImplementedException();
        }

        public Result GetProgramIndexForAccessLog(out int programIndex, out int programCount)
        {
            throw new NotImplementedException();
        }

        public Result OutputAccessLogToSdCard(U8Span logString)
        {
            throw new NotImplementedException();
        }

        public Result RegisterUpdatePartition()
        {
            throw new NotImplementedException();
        }

        public Result OpenRegisteredUpdatePartition(out IFileSystem fileSystem)
        {
            throw new NotImplementedException();
        }

        public Result OverrideSaveDataTransferTokenSignVerificationKey(ReadOnlySpan<byte> key)
        {
            throw new NotImplementedException();
        }

        public Result SetSdCardAccessibility(bool isAccessible)
        {
            throw new NotImplementedException();
        }

        public Result IsSdCardAccessible(out bool isAccessible)
        {
            throw new NotImplementedException();
        }

        private static bool IsSystemSaveDataId(ulong id)
        {
            return (long)id < 0;
        }
    }
}
