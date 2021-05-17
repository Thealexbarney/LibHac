using System;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.FsSystem.Save;
using LibHac.Util;

using OpenType = LibHac.FsSrv.SaveDataOpenTypeSetFileStorage.OpenType;

namespace LibHac.FsSrv.FsCreator
{
    public class SaveDataFileSystemCreator : ISaveDataFileSystemCreator
    {
        private IBufferManager _bufferManager;
        private RandomDataGenerator _randomGenerator;

        // LibHac Additions
        private KeySet _keySet;
        private FileSystemServer _fsServer;

        public SaveDataFileSystemCreator(FileSystemServer fsServer, KeySet keySet, IBufferManager bufferManager,
            RandomDataGenerator randomGenerator)
        {
            _bufferManager = bufferManager;
            _randomGenerator = randomGenerator;
            _fsServer = fsServer;
            _keySet = keySet;
        }

        public Result CreateFile(out IFile file, IFileSystem sourceFileSystem, ulong saveDataId, OpenMode openMode)
        {
            throw new NotImplementedException();
        }

        public Result Create(out ReferenceCountedDisposable<IFileSystem> fileSystem,
            out ReferenceCountedDisposable<ISaveDataExtraDataAccessor> extraDataAccessor,
            ISaveDataFileSystemCacheManager cacheManager, ref ReferenceCountedDisposable<IFileSystem> baseFileSystem,
            SaveDataSpaceId spaceId, ulong saveDataId, bool allowDirectorySaveData, bool useDeviceUniqueMac,
            bool isJournalingSupported, bool isMultiCommitSupported, bool openReadOnly, bool openShared,
            ISaveDataCommitTimeStampGetter timeStampGetter)
        {
            Span<byte> saveImageName = stackalloc byte[0x12];
            UnsafeHelpers.SkipParamInit(out fileSystem, out extraDataAccessor);

            Assert.SdkRequiresNotNull(cacheManager);

            var sb = new U8StringBuilder(saveImageName);
            sb.Append((byte)'/').AppendFormat(saveDataId, 'x', 16);

            Result rc = baseFileSystem.Target.GetEntryType(out DirectoryEntryType type, new U8Span(saveImageName));

            if (rc.IsFailure())
            {
                return ResultFs.PathNotFound.Includes(rc) ? ResultFs.TargetNotFound.LogConverted(rc) : rc;
            }

            if (type == DirectoryEntryType.Directory)
            {
                if (!allowDirectorySaveData)
                    return ResultFs.InvalidSaveDataEntryType.Log();

                SubdirectoryFileSystem subDirFs = null;
                ReferenceCountedDisposable<DirectorySaveDataFileSystem> saveFs = null;
                try
                {
                    subDirFs = new SubdirectoryFileSystem(ref baseFileSystem);

                    rc = subDirFs.Initialize(new U8Span(saveImageName));
                    if (rc.IsFailure()) return rc;

                    saveFs = DirectorySaveDataFileSystem.CreateShared(Shared.Move(ref subDirFs), _fsServer.Hos.Fs);

                    rc = saveFs.Target.Initialize(timeStampGetter, _randomGenerator, isJournalingSupported,
                        isMultiCommitSupported, !openReadOnly);
                    if (rc.IsFailure()) return rc;

                    fileSystem = saveFs.AddReference<IFileSystem>();
                    extraDataAccessor = saveFs.AddReference<ISaveDataExtraDataAccessor>();

                    return Result.Success;
                }
                finally
                {
                    subDirFs?.Dispose();
                    saveFs?.Dispose();
                }
            }

            ReferenceCountedDisposable<IStorage> fileStorage = null;
            try
            {
                Optional<OpenType> openType =
                    openShared ? new Optional<OpenType>(OpenType.Normal) : new Optional<OpenType>();

                rc = _fsServer.OpenSaveDataStorage(out fileStorage, ref baseFileSystem, spaceId, saveDataId,
                    OpenMode.ReadWrite, openType);
                if (rc.IsFailure()) return rc;

                if (!isJournalingSupported)
                {
                    throw new NotImplementedException();
                }

                // Todo: Properly handle shared storage
                fileSystem = new ReferenceCountedDisposable<IFileSystem>(new SaveDataFileSystem(_keySet,
                    fileStorage.Target, IntegrityCheckLevel.ErrorOnInvalid, false));

                // Todo: ISaveDataExtraDataAccessor

                return Result.Success;
            }
            finally
            {
                fileStorage?.Dispose();
            }
        }

        public Result CreateExtraDataAccessor(
            out ReferenceCountedDisposable<ISaveDataExtraDataAccessor> extraDataAccessor,
            ReferenceCountedDisposable<IFileSystem> sourceFileSystem)
        {
            throw new NotImplementedException();
        }

        public void SetSdCardEncryptionSeed(ReadOnlySpan<byte> seed)
        {
            throw new NotImplementedException();
        }
    }
}