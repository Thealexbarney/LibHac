using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Crypto;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using LibHac.FsSrv.Impl;
using LibHac.FsSystem;
using LibHac.FsSystem.Save;
using LibHac.Util;
using OpenType = LibHac.FsSrv.SaveDataOpenTypeSetFileStorage.OpenType;
using ValueSubStorage = LibHac.Fs.ValueSubStorage;

namespace LibHac.FsSrv.FsCreator;

public enum DeviceUniqueMacType
{
    Normal,
    Temporary
}

/// <summary>
/// Extends <see cref="LibHac.FsSystem.Save.SaveDataInternalStorageFileSystem"/> to allow initializing it with a
/// <see cref="SaveDataFileSystem"/> instead of directly using an <see cref="IInternalStorageFileSystem"/>.
/// </summary>
/// <remarks>Based on nnSdk 17.5.0 (FS 17.0.0)</remarks>
file class SaveDataInternalStorageFileSystem : LibHac.FsSystem.Save.SaveDataInternalStorageFileSystem
{
    private SharedRef<SaveDataFileSystem> _saveDataFileSystem;

    public SaveDataInternalStorageFileSystem()
    {
        _saveDataFileSystem = new SharedRef<SaveDataFileSystem>();
    }

    public override void Dispose()
    {
        _saveDataFileSystem.Destroy();
        base.Dispose();
    }

    public Result Initialize(ref readonly SharedRef<SaveDataFileSystem> saveDataFileSystem,
        IMacGenerator normalMacGenerator, IMacGenerator temporaryMacGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector)
    {
        Assert.SdkRequiresNotNull(in saveDataFileSystem);
        Assert.SdkRequiresNotNull(normalMacGenerator);
        Assert.SdkRequiresNotNull(temporaryMacGenerator);
        Assert.SdkRequiresNotNull(hashGeneratorFactorySelector);

        _saveDataFileSystem.SetByCopy(in saveDataFileSystem);

        Result res = Initialize(_saveDataFileSystem.Get.GetInternalStorageFileSystem(), normalMacGenerator,
            temporaryMacGenerator, hashGeneratorFactorySelector);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }
}

file static class Anonymous
{
    public static Result GetSaveDataFormatType(out SaveDataFormatType outFormatType, IStorage saveImageStorage,
        IBufferManager bufferManager, IMacGenerator macGenerator,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector, uint minimumVersion)
    {
        UnsafeHelpers.SkipParamInit(out outFormatType);

        Result res = saveImageStorage.GetSize(out long storageSize);
        if (res.IsFailure()) return res.Miss();

        using var saveSubStorage = new ValueSubStorage(saveImageStorage, 0, storageSize);

        Result resultJournalIntegrity = JournalIntegritySaveDataFileSystem.VerifyMasterHeader(in saveSubStorage,
            bufferManager, macGenerator, hashGeneratorFactorySelector, minimumVersion);
        if (resultJournalIntegrity.IsSuccess())
        {
            outFormatType = SaveDataFormatType.Normal;
            return Result.Success;
        }

        Result resultIntegrity = IntegritySaveDataFileSystem.VerifyMasterHeader(in saveSubStorage, bufferManager,
            macGenerator, hashGeneratorFactorySelector);
        if (resultIntegrity.IsSuccess())
        {
            outFormatType = SaveDataFormatType.NoJournal;
            return Result.Success;
        }

        return resultJournalIntegrity.Miss();
    }
}

/// <summary>
/// Used by <see cref="SaveDataFileSystemServiceImpl"/> for opening, formatting, and working with save data.
/// This class directly operates on save data <see cref="IStorage"/>s and the directories containing the save data,
/// whereas <see cref="SaveDataFileSystemServiceImpl"/> handles higher level tasks such as opening or creating
/// the save data images on the file system, managing caches, etc.
/// </summary>
/// <remarks>Based on nnSdk 17.5.0 (FS 17.0.0)</remarks>
public class SaveDataFileSystemCreator : ISaveDataFileSystemCreator
{
    // Option to disable some restrictions enforced in actual FS.
    private static readonly bool EnforceSaveTypeRestrictions = false;

    private class MacGenerationSeed
    {
        public const uint Size = Aes.KeySize128;

        public Array16<byte> Value;
    }

    private struct DeviceUniqueMacTypeNormal : IConstant<DeviceUniqueMacType>
    {
        public static DeviceUniqueMacType Value => DeviceUniqueMacType.Normal;
    }

    private struct DeviceUniqueMacTypeTemporary : IConstant<DeviceUniqueMacType>
    {
        public static DeviceUniqueMacType Value => DeviceUniqueMacType.Temporary;
    }

    private class DeviceUniqueMacGenerator<TMacType> : IMacGenerator where TMacType : IConstant<DeviceUniqueMacType>
    {
        private GenerateDeviceUniqueMac _generatorFunction;

        public DeviceUniqueMacGenerator(GenerateDeviceUniqueMac generatorFunction)
        {
            _generatorFunction = generatorFunction;
        }

        public Result Generate(Span<byte> macDestBuffer, ReadOnlySpan<byte> data)
        {
            return _generatorFunction(macDestBuffer, data, TMacType.Value).Ret();
        }
    }

    private class SeedUniqueMacGenerator : IMacGenerator
    {
        private GenerateSeedUniqueMac _generatorFunction;
        private MacGenerationSeed _seed;

        public SeedUniqueMacGenerator(GenerateSeedUniqueMac generatorFunction, MacGenerationSeed seed)
        {
            _generatorFunction = generatorFunction;
            _seed = seed;
        }

        public Result Generate(Span<byte> macDestBuffer, ReadOnlySpan<byte> data)
        {
            return _generatorFunction(macDestBuffer, data, _seed.Value).Ret();
        }
    }

    // ReSharper disable once NotAccessedField.Local
    private IBufferManager _bufferManager;
    private DeviceUniqueMacGenerator<DeviceUniqueMacTypeNormal> _deviceUniqueMacGeneratorNormal;
    private DeviceUniqueMacGenerator<DeviceUniqueMacTypeTemporary> _deviceUniqueMacGeneratorTemporary;
    private SeedUniqueMacGenerator _seedUniqueMacGenerator;
    private MacGenerationSeed _macGenerationSeed;
    private IHash256GeneratorFactorySelector _hashGeneratorFactorySelector;
    private uint _saveDataMinimumVersion;
    private RandomDataGenerator _randomDataGenerator;
    private Func<DebugOptionKey, long, long> _debugValueGetter;

    // LibHac Additions
    private FileSystemServer _fsServer;

    public SaveDataFileSystemCreator(FileSystemServer fsServer, IBufferManager bufferManager,
        GenerateDeviceUniqueMac deviceUniqueMacGenerator, GenerateSeedUniqueMac seedUniqueMacGenerator,
        RandomDataGenerator randomDataGenerator, IHash256GeneratorFactorySelector hashGeneratorFactorySelector,
        uint minimumVersion, Func<DebugOptionKey, long, long> debugValueGetter)
    {
        _fsServer = fsServer;
        _macGenerationSeed = new MacGenerationSeed();

        _bufferManager = bufferManager;
        _deviceUniqueMacGeneratorNormal = new DeviceUniqueMacGenerator<DeviceUniqueMacTypeNormal>(deviceUniqueMacGenerator);
        _deviceUniqueMacGeneratorTemporary = new DeviceUniqueMacGenerator<DeviceUniqueMacTypeTemporary>(deviceUniqueMacGenerator);
        _seedUniqueMacGenerator = new SeedUniqueMacGenerator(seedUniqueMacGenerator, _macGenerationSeed);
        _hashGeneratorFactorySelector = hashGeneratorFactorySelector;
        _saveDataMinimumVersion = minimumVersion;
        _randomDataGenerator = randomDataGenerator;
        _debugValueGetter = debugValueGetter;

        JournalIntegritySaveDataFileSystem.SetGenerateRandomFunction(fsServer, randomDataGenerator);
    }

    public SaveDataFileSystemCreator(FileSystemServer fsServer, IBufferManager bufferManager,
        RandomDataGenerator randomDataGenerator)
    {
        _bufferManager = bufferManager;
        _randomDataGenerator = randomDataGenerator;
        _fsServer = fsServer;
    }

    public void Dispose() { }

    private IMacGenerator GetDeviceUniqueMacGenerator(DeviceUniqueMacType macType)
    {
        switch (macType)
        {
            case DeviceUniqueMacType.Normal:
                return _deviceUniqueMacGeneratorNormal;
            case DeviceUniqueMacType.Temporary:
                return _deviceUniqueMacGeneratorTemporary;
            default:
                Abort.UnexpectedDefault();
                return default;
        }
    }

    private IMacGenerator GetMacGenerator(bool isDeviceUnique, DeviceUniqueMacType macType)
    {
        return isDeviceUnique ? GetDeviceUniqueMacGenerator(macType) : _seedUniqueMacGenerator;
    }

    public Result Format(in ValueSubStorage saveImageStorage, long blockSize, int countExpandMax, uint blockCount,
        uint journalBlockCount, IBufferManager bufferManager, bool isDeviceUniqueMac, in HashSalt hashSalt,
        RandomDataGenerator encryptionKeyGenerator, bool isReconstructible, uint version)
    {
        Result res = JournalIntegritySaveDataFileSystemDriver.Format(in saveImageStorage, blockSize, blockCount,
            journalBlockCount, countExpandMax, bufferManager,
            GetMacGenerator(isDeviceUniqueMac, DeviceUniqueMacType.Normal), _hashGeneratorFactorySelector, hashSalt,
            encryptionKeyGenerator, version);

        res = SaveDataResultConverter.ConvertSaveDataFsResult(res, isReconstructible);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result FormatAsIntegritySaveData(in ValueSubStorage saveImageStorage, long blockSize, uint blockCount,
        IBufferManager bufferManager, bool isDeviceUniqueMac, RandomDataGenerator encryptionKeyGenerator,
        bool isReconstructible, uint version)
    {
        Result res = IntegritySaveDataFileSystemDriver.Format(in saveImageStorage, blockSize, blockCount, bufferManager,
            GetMacGenerator(isDeviceUniqueMac, DeviceUniqueMacType.Normal), _hashGeneratorFactorySelector,
            encryptionKeyGenerator, version);

        res = SaveDataResultConverter.ConvertSaveDataFsResult(res, isReconstructible);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result ExtractSaveDataParameters(out JournalIntegritySaveDataParameters outParams, IStorage saveFileStorage,
        bool isDeviceUniqueMac, bool isReconstructible)
    {
        Result res = SaveDataFileSystem.ExtractParameters(out outParams, saveFileStorage, _bufferManager,
            GetMacGenerator(isDeviceUniqueMac, DeviceUniqueMacType.Normal), _hashGeneratorFactorySelector,
            _saveDataMinimumVersion);

        res = SaveDataResultConverter.ConvertSaveDataFsResult(res, isReconstructible);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result ExtendSaveData(SaveDataExtender extender, in ValueSubStorage baseStorage,
        in ValueSubStorage logStorage, bool isDeviceUniqueMac, bool isReconstructible)
    {
        Assert.SdkRequiresNotNull(extender);

        Result res = extender.Extend(in baseStorage, in logStorage, _bufferManager,
            GetMacGenerator(isDeviceUniqueMac, DeviceUniqueMacType.Normal), _hashGeneratorFactorySelector,
            _saveDataMinimumVersion);

        res = SaveDataResultConverter.ConvertSaveDataFsResult(res, isReconstructible);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public void SetMacGenerationSeed(ReadOnlySpan<byte> seed)
    {
        Assert.SdkRequires(seed.Length == MacGenerationSeed.Size);
        seed.CopyTo(_macGenerationSeed.Value);
    }

    public Result CreateRaw(ref SharedRef<IFile> outFile, in SharedRef<IFileSystem> fileSystem, ulong saveDataId, OpenMode openMode)
    {
        Unsafe.SkipInit(out Array18<byte> saveImageNameBuffer);

        using scoped var saveImageName = new Path();
        Result res = PathFunctions.SetUpFixedPathSaveId(ref saveImageName.Ref(), saveImageNameBuffer, saveDataId);
        if (res.IsFailure()) return res.Miss();

        res = fileSystem.Get.GetEntryType(out DirectoryEntryType type, in saveImageName);

        if (res.IsFailure())
        {
            if (ResultFs.PathNotFound.Includes(res))
                return ResultFs.TargetNotFound.LogConverted(res);

            return res.Miss();
        }

        if (type == DirectoryEntryType.Directory)
        {
            return ResultFs.TargetNotFound.Log();
        }

        using var file = new UniqueRef<IFile>();
        res = fileSystem.Get.OpenFile(ref file.Ref, in saveImageName, openMode);
        if (res.IsFailure()) return res.Miss();

        outFile.Set(ref file.Ref);
        return Result.Success;
    }

    public Result Create(ref SharedRef<ISaveDataFileSystem> outFileSystem, ref SharedRef<IFileSystem> baseFileSystem,
        SaveDataSpaceId spaceId, ulong saveDataId, bool allowDirectorySaveData, bool isDeviceUniqueMac,
        bool isJournalingSupported, bool isMultiCommitSupported, bool openReadOnly, bool openShared,
        ISaveDataCommitTimeStampGetter timeStampGetter, bool isReconstructible)
    {
        Unsafe.SkipInit(out Array18<byte> saveImageNameBuffer);

        using scoped var saveImageName = new Path();
        Result res = PathFunctions.SetUpFixedPathSaveId(ref saveImageName.Ref(), saveImageNameBuffer, saveDataId);
        if (res.IsFailure()) return res.Miss();

        res = baseFileSystem.Get.GetEntryType(out DirectoryEntryType type, in saveImageName);

        if (res.IsFailure())
        {
            if (ResultFs.PathNotFound.Includes(res))
                return ResultFs.TargetNotFound.LogConverted(res);

            return res.Miss();
        }

        using var saveDataFs = new SharedRef<ISaveDataFileSystem>();

        if (type == DirectoryEntryType.Directory)
        {
            if (EnforceSaveTypeRestrictions)
            {
                if (!allowDirectorySaveData)
                    return ResultFs.InvalidSaveDataEntryType.Log();
            }

            // Get a file system over the save directory
            using var baseFs = new UniqueRef<SubdirectoryFileSystem>(new SubdirectoryFileSystem(ref baseFileSystem));

            if (!baseFs.HasValue)
                return ResultFs.AllocationMemoryFailedInSaveDataFileSystemCreatorA.Log();

            res = baseFs.Get.Initialize(in saveImageName);
            if (res.IsFailure()) return res.Miss();

            // Create and initialize the directory save data FS
            using UniqueRef<IFileSystem> tempFs = UniqueRef<IFileSystem>.Create(ref baseFs.Ref);
            using var saveDirFs = new SharedRef<DirectorySaveDataFileSystem>(
                new DirectorySaveDataFileSystem(ref tempFs.Ref, _fsServer.Hos.Fs));

            if (!saveDirFs.HasValue)
                return ResultFs.AllocationMemoryFailedInSaveDataFileSystemCreatorB.Log();

            res = saveDirFs.Get.Initialize(isJournalingSupported, isMultiCommitSupported, !openReadOnly,
                timeStampGetter, _randomDataGenerator);
            if (res.IsFailure()) return res.Miss();

            saveDataFs.SetByMove(ref saveDirFs.Ref);
        }
        else
        {
            using var fileStorage = new SharedRef<IStorage>();

            Optional<OpenType> openType =
                openShared ? new Optional<OpenType>(OpenType.Normal) : new Optional<OpenType>();

            res = _fsServer.OpenSaveDataStorage(ref fileStorage.Ref, ref baseFileSystem, spaceId, saveDataId,
                OpenMode.ReadWrite, openType);
            if (res.IsFailure()) return res.Miss();

            res = Anonymous.GetSaveDataFormatType(out SaveDataFormatType formatType, fileStorage.Get, _bufferManager,
                GetMacGenerator(isDeviceUniqueMac, DeviceUniqueMacType.Normal), _hashGeneratorFactorySelector,
                _saveDataMinimumVersion);
            if (res.IsFailure()) return res.Miss();

            if (!SaveDataProperties.IsJournalingSupported(formatType))
            {
                using var fs = new SharedRef<ApplicationTemporaryFileSystem>(new ApplicationTemporaryFileSystem());
                res = fs.Get.Initialize(in fileStorage, _bufferManager,
                    GetMacGenerator(isDeviceUniqueMac, DeviceUniqueMacType.Normal), _hashGeneratorFactorySelector);

                res = SaveDataResultConverter.ConvertSaveDataFsResult(res, isReconstructible);
                if (res.IsFailure()) return res.Miss();

                saveDataFs.SetByMove(ref fs.Ref);
            }
            else
            {
                using var fs = new SharedRef<SaveDataFileSystem>(new SaveDataFileSystem());
                if (!fs.HasValue)
                    return ResultFs.AllocationMemoryFailedInSaveDataFileSystemCreatorD.Log();

                res = fs.Get.Initialize(in fileStorage, _bufferManager,
                    GetMacGenerator(isDeviceUniqueMac, DeviceUniqueMacType.Normal), _hashGeneratorFactorySelector,
                    timeStampGetter, _randomDataGenerator, _saveDataMinimumVersion, isMultiCommitSupported);

                res = SaveDataResultConverter.ConvertSaveDataFsResult(res, isReconstructible);
                if (res.IsFailure()) return res.Miss();

                saveDataFs.SetByMove(ref fs.Ref);
            }
        }

        // Wrap the save FS in a result convert FS and set it as the output FS
        using var resultConvertFs = new SharedRef<SaveDataResultConvertFileSystem>(
            new SaveDataResultConvertFileSystem(ref saveDataFs.Ref, isReconstructible));

        outFileSystem.SetByMove(ref resultConvertFs.Ref);
        return Result.Success;
    }

    public Result CreateExtraDataAccessor(ref SharedRef<ISaveDataExtraDataAccessor> outExtraDataAccessor,
        in SharedRef<IStorage> baseStorage, bool isDeviceUniqueMac, bool isIntegritySaveData, bool isReconstructible)
    {
        using var saveDataFs = new SharedRef<ISaveDataFileSystem>();

        if (!isIntegritySaveData)
        {
            using var fs = new SharedRef<ApplicationTemporaryFileSystem>(new ApplicationTemporaryFileSystem());
            Result res = fs.Get.Initialize(in baseStorage, _bufferManager,
                GetMacGenerator(isDeviceUniqueMac, DeviceUniqueMacType.Normal), _hashGeneratorFactorySelector);

            res = SaveDataResultConverter.ConvertSaveDataFsResult(res, isReconstructible);
            if (res.IsFailure()) return res.Miss();

            saveDataFs.SetByMove(ref fs.Ref);
        }
        else
        {
            using var fs = new SharedRef<SaveDataFileSystem>(new SaveDataFileSystem());
            Result res = fs.Get.Initialize(in baseStorage, _bufferManager,
                GetMacGenerator(isDeviceUniqueMac, DeviceUniqueMacType.Normal), _hashGeneratorFactorySelector,
                _saveDataMinimumVersion, canCommitProvisionally: false);

            res = SaveDataResultConverter.ConvertSaveDataFsResult(res, isReconstructible);
            if (res.IsFailure()) return res.Miss();

            saveDataFs.SetByMove(ref fs.Ref);
        }

        using var resultConvertFs = new SharedRef<SaveDataResultConvertFileSystem>(
            new SaveDataResultConvertFileSystem(ref saveDataFs.Ref, isReconstructible));

        outExtraDataAccessor.SetByMove(ref resultConvertFs.Ref);
        return Result.Success;
    }

    public Result CreateInternalStorage(ref SharedRef<IFileSystem> outFileSystem,
        in SharedRef<IFileSystem> baseFileSystem, SaveDataSpaceId spaceId, ulong saveDataId, bool isDeviceUniqueMac,
        bool useUniqueKey1, ISaveDataCommitTimeStampGetter timeStampGetter, bool isReconstructible)
    {
        Result res;
        Unsafe.SkipInit(out Array18<byte> saveImageNameBuffer);

        using (scoped var saveImageName = new Path())
        {
            res = PathFunctions.SetUpFixedPathSaveId(ref saveImageName.Ref(), saveImageNameBuffer, saveDataId);
            if (res.IsFailure()) return res.Miss();

            res = baseFileSystem.Get.GetEntryType(out DirectoryEntryType entryType, in saveImageName);

            if (res.IsFailure())
            {
                if (ResultFs.PathNotFound.Includes(res))
                    return ResultFs.TargetNotFound.LogConverted(res);

                return res.Miss();
            }

            if (entryType == DirectoryEntryType.Directory)
                return ResultFs.InvalidSaveDataEntryType.Log();
        }

        IMacGenerator normalMacGenerator = GetMacGenerator(isDeviceUniqueMac, DeviceUniqueMacType.Normal);
        IMacGenerator temporaryMacGenerator = GetMacGenerator(isDeviceUniqueMac, DeviceUniqueMacType.Temporary);
        IMacGenerator macGenerator = useUniqueKey1 ? temporaryMacGenerator : normalMacGenerator;

        using var fileStorage = new SharedRef<IStorage>();
        res = _fsServer.OpenSaveDataStorage(ref fileStorage.Ref, in baseFileSystem, spaceId, saveDataId,
            OpenMode.ReadWrite, OpenType.Internal);
        if (res.IsFailure()) return res.Miss();

        using var saveFs = new SharedRef<SaveDataFileSystem>(new SaveDataFileSystem());
        res = saveFs.Get.Initialize(in fileStorage, _bufferManager, macGenerator, _hashGeneratorFactorySelector,
            timeStampGetter, _randomDataGenerator, _saveDataMinimumVersion, canCommitProvisionally: false);

        res = SaveDataResultConverter.ConvertSaveDataFsResult(res, isReconstructible);
        if (res.IsFailure()) return res.Miss();

        using var internalStorage = new SharedRef<SaveDataInternalStorageFileSystem>(new SaveDataInternalStorageFileSystem());
        res = internalStorage.Get.Initialize(in saveFs, normalMacGenerator, temporaryMacGenerator, _hashGeneratorFactorySelector);

        res = SaveDataResultConverter.ConvertSaveDataFsResult(res, isReconstructible);
        if (res.IsFailure()) return res.Miss();

        using SharedRef<ISaveDataFileSystem> tempSaveFs = SharedRef<ISaveDataFileSystem>.CreateMove(ref saveFs.Ref);
        using var resultConvertFs = new SharedRef<SaveDataResultConvertFileSystem>(
            new SaveDataResultConvertFileSystem(ref tempSaveFs.Ref, isReconstructible));

        outFileSystem.SetByMove(ref resultConvertFs.Ref);
        return Result.Success;
    }

    public Result RecoverMasterHeader(in SharedRef<IFileSystem> baseFileSystem, ulong saveDataId,
        IBufferManager bufferManager, bool isDeviceUniqueMac, bool isReconstructible)
    {
        Unsafe.SkipInit(out Array18<byte> saveImageNameBuffer);

        using scoped var saveImageName = new Path();
        Result res = PathFunctions.SetUpFixedPathSaveId(ref saveImageName.Ref(), saveImageNameBuffer, saveDataId);
        if (res.IsFailure()) return res.Miss();

        res = baseFileSystem.Get.GetEntryType(out DirectoryEntryType entryType, in saveImageName);

        if (res.IsFailure())
        {
            if (ResultFs.PathNotFound.Includes(res))
                return ResultFs.TargetNotFound.LogConverted(res);

            return res.Miss();
        }

        if (entryType == DirectoryEntryType.Directory)
        {
            // Directory save data doesn't have a master header
            return Result.Success;
        }

        using var fileStorage = new SharedRef<FileStorageBasedFileSystem>();
        res = fileStorage.Get.Initialize(in baseFileSystem, in saveImageName, OpenMode.ReadWrite);
        if (res.IsFailure()) return res.Miss();

        res = fileStorage.Get.GetSize(out long size);
        if (res.IsFailure()) return res.Miss();

        using var fileSubStorage = new ValueSubStorage(fileStorage.Get, 0, size);
        IMacGenerator macGenerator = GetMacGenerator(isDeviceUniqueMac, DeviceUniqueMacType.Normal);

        res = JournalIntegritySaveDataFileSystem.RecoverMasterHeader(in fileSubStorage, bufferManager, macGenerator,
            _hashGeneratorFactorySelector, _saveDataMinimumVersion);

        res = SaveDataResultConverter.ConvertSaveDataFsResult(res, isReconstructible);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result UpdateMac(in SharedRef<IFileSystem> baseFileSystem, ulong saveDataId, bool isDeviceUniqueMac,
        bool isReconstructible)
    {
        Unsafe.SkipInit(out Array18<byte> saveImageNameBuffer);

        using scoped var saveImageName = new Path();
        Result res = PathFunctions.SetUpFixedPathSaveId(ref saveImageName.Ref(), saveImageNameBuffer, saveDataId);
        if (res.IsFailure()) return res.Miss();

        res = baseFileSystem.Get.GetEntryType(out DirectoryEntryType entryType, in saveImageName);

        if (res.IsFailure())
        {
            if (ResultFs.PathNotFound.Includes(res))
                return ResultFs.TargetNotFound.LogConverted(res);

            return res.Miss();
        }

        if (entryType == DirectoryEntryType.Directory)
        {
            // Directory save data doesn't contain a MAC
            return Result.Success;
        }

        using var fileStorage = new SharedRef<FileStorageBasedFileSystem>();
        res = fileStorage.Get.Initialize(in baseFileSystem, in saveImageName, OpenMode.ReadWrite);
        if (res.IsFailure()) return res.Miss();

        res = fileStorage.Get.GetSize(out long size);
        if (res.IsFailure()) return res.Miss();

        using var fileSubStorage = new ValueSubStorage(fileStorage.Get, 0, size);
        IMacGenerator macGenerator = GetMacGenerator(isDeviceUniqueMac, DeviceUniqueMacType.Normal);

        res = JournalIntegritySaveDataFileSystem.UpdateMac(in fileSubStorage, macGenerator,
            _hashGeneratorFactorySelector, _saveDataMinimumVersion);

        res = SaveDataResultConverter.ConvertSaveDataFsResult(res, isReconstructible);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result IsProvisionallyCommittedSaveData(out bool outIsProvisionallyCommitted,
        in SharedRef<IFileSystem> baseFileSystem, in SaveDataInfo info, bool isDeviceUniqueMac,
        ISaveDataCommitTimeStampGetter timeStampGetter, bool isReconstructible)
    {
        UnsafeHelpers.SkipParamInit(out outIsProvisionallyCommitted);
        Unsafe.SkipInit(out Array18<byte> saveImageNameBuffer);

        using scoped var saveImageName = new Path();
        Result res = PathFunctions.SetUpFixedPathSaveId(ref saveImageName.Ref(), saveImageNameBuffer, info.SaveDataId);
        if (res.IsFailure()) return res.Miss();

        res = baseFileSystem.Get.GetEntryType(out DirectoryEntryType entryType, in saveImageName);

        if (res.IsFailure())
        {
            if (ResultFs.PathNotFound.Includes(res))
                return ResultFs.TargetNotFound.LogConverted(res);

            return res.Miss();
        }

        if (entryType == DirectoryEntryType.Directory)
            return ResultFs.InvalidSaveDataEntryType.Log();

        using var fileStorage = new SharedRef<IStorage>();
        res = _fsServer.OpenSaveDataStorage(ref fileStorage.Ref, in baseFileSystem, info.SpaceId, info.SaveDataId,
            OpenMode.ReadWrite, OpenType.Internal);
        if (res.IsFailure()) return res.Miss();

        using var saveFs = new SharedRef<SaveDataFileSystem>(new SaveDataFileSystem());

        res = saveFs.Get.Initialize(in fileStorage, _bufferManager,
            GetMacGenerator(isDeviceUniqueMac, DeviceUniqueMacType.Normal), _hashGeneratorFactorySelector,
            timeStampGetter, _randomDataGenerator, _saveDataMinimumVersion, canCommitProvisionally: false);

        res = SaveDataResultConverter.ConvertSaveDataFsResult(res, isReconstructible);
        if (res.IsFailure()) return res.Miss();

        outIsProvisionallyCommitted = saveFs.Get.GetCounterForBundledCommit() != 0;
        return Result.Success;
    }

    public IMacGenerator GetMacGenerator(bool isDeviceUniqueMac, bool isTemporaryTransferSave)
    {
        DeviceUniqueMacType macType = isTemporaryTransferSave ? DeviceUniqueMacType.Temporary : DeviceUniqueMacType.Normal;
        return GetMacGenerator(isDeviceUniqueMac, macType);
    }
}