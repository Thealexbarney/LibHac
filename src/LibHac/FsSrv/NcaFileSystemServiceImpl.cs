using System;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv.FsCreator;
using LibHac.FsSrv.Impl;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Os;
using LibHac.Spl;
using LibHac.Tools.FsSystem.NcaUtils;
using LibHac.Util;
using static LibHac.Fs.Impl.CommonMountNames;
using NcaFsHeader = LibHac.Tools.FsSystem.NcaUtils.NcaFsHeader;
using RightsId = LibHac.Fs.RightsId;
using Utility = LibHac.FsSystem.Utility;

namespace LibHac.FsSrv;

public class NcaFileSystemServiceImpl
{
    private Configuration _config;
    // UpdatePartitionPath
    private ExternalKeySet _externalKeyManager;
    private LocationResolverSet _locationResolverSet;
    // SystemDataUpdateEventManager
    private EncryptionSeed _encryptionSeed;
    private int _romFsRemountForDataCorruptionCount;
    private int _romfsUnrecoverableDataCorruptionByRemountCount;
    private int _romFsRecoveredByInvalidateCacheCount;
    private SdkMutexType _romfsCountMutex;

    public NcaFileSystemServiceImpl(in Configuration configuration, ExternalKeySet externalKeySet)
    {
        _config = configuration;
        _externalKeyManager = externalKeySet;
        _locationResolverSet = new LocationResolverSet(_config.FsServer);
        _romfsCountMutex.Initialize();
    }

    public struct Configuration
    {
        public BaseFileSystemServiceImpl BaseFsService;
        public ILocalFileSystemCreator LocalFsCreator;
        public ITargetManagerFileSystemCreator TargetManagerFsCreator;
        public IPartitionFileSystemCreator PartitionFsCreator;
        public IRomFileSystemCreator RomFsCreator;
        public IStorageOnNcaCreator StorageOnNcaCreator;
        public ISubDirectoryFileSystemCreator SubDirectoryFsCreator;
        public IEncryptedFileSystemCreator EncryptedFsCreator;
        public ProgramRegistryServiceImpl ProgramRegistryService;
        public AccessFailureManagementServiceImpl AccessFailureManagementService;
        public InternalProgramIdRangeForSpeedEmulation SpeedEmulationRange;

        // LibHac additions
        public FileSystemServer FsServer;
    }

    private struct MountInfo
    {
        public bool IsGameCard;
        public int GcHandle;
        public bool IsHostFs;
        public bool CanMountNca;
    }

    public Result OpenFileSystem(ref SharedRef<IFileSystem> outFileSystem, in Path path, FileSystemProxyType type,
        ulong id, bool isDirectory)
    {
        return OpenFileSystem(ref outFileSystem, out Unsafe.NullRef<CodeVerificationData>(), in path, type, false,
            id, isDirectory);
    }

    public Result OpenFileSystem(ref SharedRef<IFileSystem> outFileSystem, in Path path, FileSystemProxyType type,
        bool canMountSystemDataPrivate, ulong id, bool isDirectory)
    {
        return OpenFileSystem(ref outFileSystem, out Unsafe.NullRef<CodeVerificationData>(), in path, type,
            canMountSystemDataPrivate, id, isDirectory);
    }

    public Result OpenFileSystem(ref SharedRef<IFileSystem> outFileSystem,
        out CodeVerificationData verificationData, in Path path, FileSystemProxyType type,
        bool canMountSystemDataPrivate, ulong id, bool isDirectory)
    {
        UnsafeHelpers.SkipParamInit(out verificationData);

        if (!Unsafe.IsNullRef(ref verificationData))
            verificationData.HasData = false;

        // Get a reference to the path that will be advanced as each part of the path is parsed
        var currentPath = new U8Span(path.GetString());

        // Open the root filesystem based on the path's mount name
        using var baseFileSystem = new SharedRef<IFileSystem>();
        Result res = ParseMountName(ref currentPath, ref baseFileSystem.Ref(), out bool shouldContinue,
            out MountInfo mountNameInfo);
        if (res.IsFailure()) return res.Miss();

        // Don't continue if the rest of the path is empty
        if (!shouldContinue)
            return ResultFs.InvalidArgument.Log();

        if (type == FileSystemProxyType.Logo && mountNameInfo.IsGameCard)
        {
            res = _config.BaseFsService.OpenGameCardFileSystem(ref outFileSystem, (uint)mountNameInfo.GcHandle,
                GameCardPartition.Logo);

            if (res.IsSuccess())
                return Result.Success;

            if (!ResultFs.PartitionNotFound.Includes(res))
                return res;
        }

        res = CheckDirOrNcaOrNsp(ref currentPath, out isDirectory);
        if (res.IsFailure()) return res.Miss();

        if (isDirectory)
        {
            if (!mountNameInfo.IsHostFs)
                return ResultFs.PermissionDenied.Log();

            using var directoryPath = new Path();
            res = directoryPath.InitializeWithNormalization(currentPath.Value);
            if (res.IsFailure()) return res.Miss();

            if (type == FileSystemProxyType.Manual)
            {
                using var hostFileSystem = new SharedRef<IFileSystem>();
                using var readOnlyFileSystem = new SharedRef<IFileSystem>();

                res = ParseDirWithPathCaseNormalizationOnCaseSensitiveHostFs(ref hostFileSystem.Ref(),
                    in directoryPath);
                if (res.IsFailure()) return res.Miss();

                readOnlyFileSystem.Reset(new ReadOnlyFileSystem(ref hostFileSystem.Ref()));
                outFileSystem.SetByMove(ref readOnlyFileSystem.Ref());

                return Result.Success;
            }

            return ParseDir(in directoryPath, ref outFileSystem, ref baseFileSystem.Ref(), type, true);
        }

        using var nspFileSystem = new SharedRef<IFileSystem>();
        using SharedRef<IFileSystem> tempFileSystem = SharedRef<IFileSystem>.CreateCopy(in baseFileSystem);
        res = ParseNsp(ref currentPath, ref nspFileSystem.Ref(), ref baseFileSystem.Ref());

        if (res.IsSuccess())
        {
            // Must be the end of the path to open Application Package FS type
            if (currentPath.Value.At(0) == 0)
            {
                if (type == FileSystemProxyType.Package)
                {
                    outFileSystem.SetByMove(ref nspFileSystem.Ref());
                    return Result.Success;
                }

                return ResultFs.InvalidArgument.Log();
            }

            baseFileSystem.SetByMove(ref nspFileSystem.Ref());
        }

        if (!mountNameInfo.CanMountNca)
        {
            return ResultFs.UnexpectedInNcaFileSystemServiceImplA.Log();
        }

        ulong openProgramId = mountNameInfo.IsHostFs ? ulong.MaxValue : id;

        res = ParseNca(ref currentPath, out Nca nca, ref baseFileSystem.Ref(), openProgramId);
        if (res.IsFailure()) return res.Miss();

        using var ncaSectionStorage = new SharedRef<IStorage>();
        res = OpenStorageByContentType(ref ncaSectionStorage.Ref(), nca, out NcaFormatType fsType, type,
            mountNameInfo.IsGameCard, canMountSystemDataPrivate);
        if (res.IsFailure()) return res.Miss();

        switch (fsType)
        {
            case NcaFormatType.Romfs:
                return _config.RomFsCreator.Create(ref outFileSystem, ref ncaSectionStorage.Ref());
            case NcaFormatType.Pfs0:
                return _config.PartitionFsCreator.Create(ref outFileSystem, ref ncaSectionStorage.Ref());
            default:
                return ResultFs.InvalidNcaFileSystemType.Log();
        }
    }

    public Result OpenDataFileSystem(ref SharedRef<IFileSystem> outFileSystem, in Path path,
        FileSystemProxyType fsType, ulong programId, bool isDirectory)
    {
        throw new NotImplementedException();
    }

    public Result OpenStorageWithPatch(ref SharedRef<IStorage> outStorage, out Hash ncaHeaderDigest,
        in Path originalNcaPath, in Path currentNcaPath, FileSystemProxyType fsType, ulong id)
    {
        throw new NotImplementedException();
    }

    public Result OpenFileSystemWithPatch(ref SharedRef<IFileSystem> outFileSystem,
        in Path originalNcaPath, in Path currentNcaPath, FileSystemProxyType fsType, ulong id)
    {
        using var romFsStorage = new SharedRef<IStorage>();
        Result res = OpenStorageWithPatch(ref romFsStorage.Ref(), out Unsafe.NullRef<Hash>(), in originalNcaPath,
            in currentNcaPath, fsType, id);
        if (res.IsFailure()) return res.Miss();

        return _config.RomFsCreator.Create(ref outFileSystem, ref romFsStorage.Ref());
    }

    public Result OpenContentStorageFileSystem(ref SharedRef<IFileSystem> outFileSystem,
        ContentStorageId contentStorageId)
    {
        using var fileSystem = new SharedRef<IFileSystem>();
        Result res;

        // Open the appropriate base file system for the content storage ID
        switch (contentStorageId)
        {
            case ContentStorageId.System:
                res = _config.BaseFsService.OpenBisFileSystem(ref fileSystem.Ref(), BisPartitionId.System);
                if (res.IsFailure()) return res.Miss();
                break;
            case ContentStorageId.User:
                res = _config.BaseFsService.OpenBisFileSystem(ref fileSystem.Ref(), BisPartitionId.User);
                if (res.IsFailure()) return res.Miss();
                break;
            case ContentStorageId.SdCard:
                res = _config.BaseFsService.OpenSdCardProxyFileSystem(ref fileSystem.Ref());
                if (res.IsFailure()) return res.Miss();
                break;
            default:
                return ResultFs.InvalidArgument.Log();
        }

        Unsafe.SkipInit(out Array64<byte> contentStoragePathBuffer);

        // Build the appropriate path for the content storage ID
        if (contentStorageId == ContentStorageId.SdCard)
        {
            var sb = new U8StringBuilder(contentStoragePathBuffer.Items);
            sb.Append(StringTraits.DirectorySeparator).Append(CommonDirNames.SdCardNintendoRootDirectoryName);
            sb.Append(StringTraits.DirectorySeparator).Append(CommonDirNames.ContentStorageDirectoryName);
        }
        else
        {
            var sb = new U8StringBuilder(contentStoragePathBuffer.Items);
            sb.Append(StringTraits.DirectorySeparator).Append(CommonDirNames.ContentStorageDirectoryName);
        }

        using var contentStoragePath = new Path();
        res = PathFunctions.SetUpFixedPath(ref contentStoragePath.Ref(), contentStoragePathBuffer);
        if (res.IsFailure()) return res.Miss();

        // Make sure the content storage path exists
        res = Utility.EnsureDirectory(fileSystem.Get, in contentStoragePath);
        if (res.IsFailure()) return res.Miss();

        using var subDirFs = new SharedRef<IFileSystem>();
        res = _config.SubDirectoryFsCreator.Create(ref subDirFs.Ref(), ref fileSystem.Ref(), in contentStoragePath);
        if (res.IsFailure()) return res.Miss();

        // Only content on the SD card is encrypted
        if (contentStorageId == ContentStorageId.SdCard)
        {
            using SharedRef<IFileSystem> tempFileSystem = SharedRef<IFileSystem>.CreateMove(ref subDirFs.Ref());
            res = _config.EncryptedFsCreator.Create(ref subDirFs.Ref(), ref tempFileSystem.Ref(),
               IEncryptedFileSystemCreator.KeyId.Content, in _encryptionSeed);
            if (res.IsFailure()) return res.Miss();
        }
        outFileSystem.SetByMove(ref subDirFs.Ref());

        return Result.Success;
    }

    public Result GetRightsId(out RightsId rightsId, out byte keyGeneration, in Path path, ProgramId programId)
    {
        throw new NotImplementedException();
    }

    public Result RegisterExternalKey(in RightsId rightsId, in AccessKey accessKey)
    {
        return _externalKeyManager.Add(rightsId, accessKey);
    }

    public Result UnregisterExternalKey(in RightsId rightsId)
    {
        _externalKeyManager.Remove(rightsId);

        return Result.Success;
    }

    public Result UnregisterAllExternalKey()
    {
        _externalKeyManager.Clear();

        return Result.Success;
    }

    public Result RegisterUpdatePartition(ulong programId, in Path path)
    {
        throw new NotImplementedException();
    }

    public Result OpenRegisteredUpdatePartition(ref SharedRef<IFileSystem> outFileSystem)
    {
        throw new NotImplementedException();
    }

    private Result ParseMountName(ref U8Span path, ref SharedRef<IFileSystem> outFileSystem,
        out bool shouldContinue, out MountInfo info)
    {
        info = new MountInfo();
        shouldContinue = true;

        if (StringUtils.Compare(path, GameCardFileSystemMountName,
            GameCardFileSystemMountName.Length) == 0)
        {
            path = path.Slice(GameCardFileSystemMountName.Length);

            if (StringUtils.GetLength(path.Value, 9) < 9)
                return ResultFs.InvalidPath.Log();

            GameCardPartition partition;
            if (StringUtils.CompareCaseInsensitive(path, GameCardFileSystemMountNameSuffixUpdate) == 0)
                partition = GameCardPartition.Update;
            else if (StringUtils.CompareCaseInsensitive(path, GameCardFileSystemMountNameSuffixNormal) == 0)
                partition = GameCardPartition.Normal;
            else if (StringUtils.CompareCaseInsensitive(path, GameCardFileSystemMountNameSuffixSecure) == 0)
                partition = GameCardPartition.Secure;
            else
                return ResultFs.InvalidPath.Log();

            path = path.Slice(1);
            bool handleParsed = Utf8Parser.TryParse(path, out int handle, out int bytesConsumed);

            if (!handleParsed || handle == -1 || bytesConsumed != 8)
                return ResultFs.InvalidPath.Log();

            path = path.Slice(8);

            Result res = _config.BaseFsService.OpenGameCardFileSystem(ref outFileSystem, (uint)handle, partition);
            if (res.IsFailure()) return res.Miss();

            info.GcHandle = handle;
            info.IsGameCard = true;
            info.CanMountNca = true;
        }

        else if (StringUtils.Compare(path, ContentStorageSystemMountName,
                ContentStorageSystemMountName.Length) == 0)
        {
            path = path.Slice(ContentStorageSystemMountName.Length);

            Result res = OpenContentStorageFileSystem(ref outFileSystem, ContentStorageId.System);
            if (res.IsFailure()) return res.Miss();

            info.CanMountNca = true;
        }

        else if (StringUtils.Compare(path, ContentStorageUserMountName,
                ContentStorageUserMountName.Length) == 0)
        {
            path = path.Slice(ContentStorageUserMountName.Length);

            Result res = OpenContentStorageFileSystem(ref outFileSystem, ContentStorageId.User);
            if (res.IsFailure()) return res.Miss();

            info.CanMountNca = true;
        }

        else if (StringUtils.Compare(path, ContentStorageSdCardMountName,
                ContentStorageSdCardMountName.Length) == 0)
        {
            path = path.Slice(ContentStorageSdCardMountName.Length);

            Result res = OpenContentStorageFileSystem(ref outFileSystem, ContentStorageId.SdCard);
            if (res.IsFailure()) return res.Miss();

            info.CanMountNca = true;
        }

        else if (StringUtils.Compare(path, BisCalibrationFilePartitionMountName,
                BisCalibrationFilePartitionMountName.Length) == 0)
        {
            path = path.Slice(BisCalibrationFilePartitionMountName.Length);

            Result res = _config.BaseFsService.OpenBisFileSystem(ref outFileSystem, BisPartitionId.CalibrationFile);
            if (res.IsFailure()) return res.Miss();
        }

        else if (StringUtils.Compare(path, BisSafeModePartitionMountName,
                BisSafeModePartitionMountName.Length) == 0)
        {
            path = path.Slice(BisSafeModePartitionMountName.Length);

            Result res = _config.BaseFsService.OpenBisFileSystem(ref outFileSystem, BisPartitionId.SafeMode);
            if (res.IsFailure()) return res.Miss();
        }

        else if (StringUtils.Compare(path, BisUserPartitionMountName,
                BisUserPartitionMountName.Length) == 0)
        {
            path = path.Slice(BisUserPartitionMountName.Length);

            Result res = _config.BaseFsService.OpenBisFileSystem(ref outFileSystem, BisPartitionId.User);
            if (res.IsFailure()) return res.Miss();
        }

        else if (StringUtils.Compare(path, BisSystemPartitionMountName,
                BisSystemPartitionMountName.Length) == 0)
        {
            path = path.Slice(BisSystemPartitionMountName.Length);

            Result res = _config.BaseFsService.OpenBisFileSystem(ref outFileSystem, BisPartitionId.System);
            if (res.IsFailure()) return res.Miss();
        }

        else if (StringUtils.Compare(path, SdCardFileSystemMountName,
                SdCardFileSystemMountName.Length) == 0)
        {
            path = path.Slice(SdCardFileSystemMountName.Length);

            Result res = _config.BaseFsService.OpenSdCardProxyFileSystem(ref outFileSystem);
            if (res.IsFailure()) return res.Miss();
        }

        else if (StringUtils.Compare(path, HostRootFileSystemMountName,
                HostRootFileSystemMountName.Length) == 0)
        {
            path = path.Slice(HostRootFileSystemMountName.Length);

            using var rootPathEmpty = new Path();
            Result res = rootPathEmpty.InitializeAsEmpty();
            if (res.IsFailure()) return res.Miss();

            info.IsHostFs = true;
            info.CanMountNca = true;

            res = OpenHostFileSystem(ref outFileSystem, in rootPathEmpty, openCaseSensitive: false);
            if (res.IsFailure()) return res.Miss();
        }

        else if (StringUtils.Compare(path, RegisteredUpdatePartitionMountName,
                RegisteredUpdatePartitionMountName.Length) == 0)
        {
            path = path.Slice(RegisteredUpdatePartitionMountName.Length);

            info.CanMountNca = true;

            throw new NotImplementedException();
        }

        else
        {
            return ResultFs.PathNotFound.Log();
        }

        if (StringUtils.GetLength(path, PathTool.EntryNameLengthMax) == 0)
        {
            shouldContinue = false;
        }

        return Result.Success;
    }

    private Result CheckDirOrNcaOrNsp(ref U8Span path, out bool isDirectory)
    {
        UnsafeHelpers.SkipParamInit(out isDirectory);

        ReadOnlySpan<byte> mountSeparator = ":/"u8;

        if (StringUtils.Compare(mountSeparator, path, mountSeparator.Length) != 0)
        {
            return ResultFs.PathNotFound.Log();
        }

        path = path.Slice(1);
        int pathLen = StringUtils.GetLength(path);

        if (path[pathLen - 1] == '/')
        {
            isDirectory = true;
            return Result.Success;
        }

        // Now make sure the path has a content file extension
        if (pathLen < 5)
            return ResultFs.PathNotFound.Log();

        ReadOnlySpan<byte> fileExtension = path.Value.Slice(pathLen - 4);

        ReadOnlySpan<byte> ncaExtension = ".nca"u8;
        ReadOnlySpan<byte> nspExtension = ".nsp"u8;

        if (StringUtils.CompareCaseInsensitive(fileExtension, ncaExtension) == 0 ||
            StringUtils.CompareCaseInsensitive(fileExtension, nspExtension) == 0)
        {
            isDirectory = false;
            return Result.Success;
        }

        return ResultFs.PathNotFound.Log();
    }

    private Result ParseDir(in Path path, ref SharedRef<IFileSystem> outContentFileSystem,
        ref SharedRef<IFileSystem> baseFileSystem, FileSystemProxyType fsType, bool preserveUnc)
    {
        using var fileSystem = new SharedRef<IFileSystem>();
        Result res = _config.SubDirectoryFsCreator.Create(ref fileSystem.Ref(), ref baseFileSystem, in path);
        if (res.IsFailure()) return res.Miss();

        return ParseContentTypeForDirectory(ref outContentFileSystem, ref fileSystem.Ref(), fsType);
    }

    private Result ParseDirWithPathCaseNormalizationOnCaseSensitiveHostFs(ref SharedRef<IFileSystem> outFileSystem,
        in Path path)
    {
        using var pathRoot = new Path();
        using var pathData = new Path();

        Result res = PathFunctions.SetUpFixedPath(ref pathData.Ref(), "/data"u8);
        if (res.IsFailure()) return res.Miss();

        res = pathRoot.Combine(in path, in pathData);
        if (res.IsFailure()) return res.Miss();

        res = _config.TargetManagerFsCreator.NormalizeCaseOfPath(out bool isSupported, ref pathRoot.Ref());
        if (res.IsFailure()) return res.Miss();

        res = _config.TargetManagerFsCreator.Create(ref outFileSystem, in pathRoot, isSupported, false,
            Result.Success);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    private Result ParseNsp(ref U8Span path, ref SharedRef<IFileSystem> outFileSystem,
        ref SharedRef<IFileSystem> baseFileSystem)
    {
        ReadOnlySpan<byte> nspExtension = ".nsp"u8;

        // Search for the end of the nsp part of the path
        int nspPathLen = 0;

        while (true)
        {
            U8Span currentSpan;

            while (true)
            {
                currentSpan = path.Slice(nspPathLen);
                if (StringUtils.CompareCaseInsensitive(nspExtension, currentSpan, 4) == 0)
                    break;

                if (currentSpan.Length == 0 || currentSpan[0] == 0)
                {
                    return ResultFs.PathNotFound.Log();
                }

                nspPathLen++;
            }

            // The nsp filename must be the end of the entire path or the end of a path segment
            if (currentSpan.Length <= 4 || currentSpan[4] == 0 || currentSpan[4] == (byte)'/')
                break;

            nspPathLen += 4;
        }

        nspPathLen += 4;

        using var pathNsp = new Path();
        Result res = pathNsp.InitializeWithNormalization(path, nspPathLen);
        if (res.IsFailure()) return res.Miss();

        using var nspFileStorage = new SharedRef<FileStorageBasedFileSystem>(new FileStorageBasedFileSystem());

        res = nspFileStorage.Get.Initialize(ref baseFileSystem, in pathNsp, OpenMode.Read);
        if (res.IsFailure()) return res.Miss();

        using SharedRef<IStorage> tempStorage = SharedRef<IStorage>.CreateMove(ref nspFileStorage.Ref());
        res = _config.PartitionFsCreator.Create(ref outFileSystem, ref tempStorage.Ref());

        if (res.IsSuccess())
        {
            path = path.Slice(nspPathLen);
        }

        return res;
    }

    private Result ParseNca(ref U8Span path, out Nca nca, ref SharedRef<IFileSystem> baseFileSystem, ulong ncaId)
    {
        UnsafeHelpers.SkipParamInit(out nca);

        // Todo: Create ref-counted storage
        var ncaFileStorage = new FileStorageBasedFileSystem();

        using var pathNca = new Path();
        Result res = pathNca.InitializeWithNormalization(path);
        if (res.IsFailure()) return res.Miss();

        res = ncaFileStorage.Initialize(ref baseFileSystem, in pathNca, OpenMode.Read);
        if (res.IsFailure()) return res.Miss();

        res = _config.StorageOnNcaCreator.OpenNca(out Nca ncaTemp, ncaFileStorage);
        if (res.IsFailure()) return res.Miss();

        if (ncaId == ulong.MaxValue)
        {
            ulong ncaProgramId = ncaTemp.Header.TitleId;

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (ncaProgramId != ulong.MaxValue && ncaId != ncaProgramId)
            {
                return ResultFs.InvalidNcaId.Log();
            }
        }

        nca = ncaTemp;
        return Result.Success;
    }

    private Result ParseContentTypeForDirectory(ref SharedRef<IFileSystem> outFileSystem,
        ref SharedRef<IFileSystem> baseFileSystem, FileSystemProxyType fsType)
    {
        ReadOnlySpan<byte> dirName;

        // Get the name of the subdirectory for the filesystem type
        switch (fsType)
        {
            case FileSystemProxyType.Package:
                outFileSystem.SetByMove(ref baseFileSystem);
                return Result.Success;

            case FileSystemProxyType.Code:
                dirName = "/code/"u8;
                break;
            case FileSystemProxyType.Rom:
            case FileSystemProxyType.Control:
            case FileSystemProxyType.Manual:
            case FileSystemProxyType.Meta:
            case FileSystemProxyType.RegisteredUpdate:
                dirName = "/data/"u8;
                break;
            case FileSystemProxyType.Logo:
                dirName = "/logo/"u8;
                break;

            default:
                return ResultFs.InvalidArgument.Log();
        }

        using var subDirFs = new SharedRef<IFileSystem>();

        using var directoryPath = new Path();
        Result res = PathFunctions.SetUpFixedPath(ref directoryPath.Ref(), dirName);
        if (res.IsFailure()) return res.Miss();

        if (directoryPath.IsEmpty())
            return ResultFs.InvalidArgument.Log();

        // Open the subdirectory filesystem
        res = _config.SubDirectoryFsCreator.Create(ref subDirFs.Ref(), ref baseFileSystem, in directoryPath);
        if (res.IsFailure()) return res.Miss();

        outFileSystem.SetByMove(ref subDirFs.Ref());
        return Result.Success;
    }

    private Result SetExternalKeyForRightsId(Nca nca)
    {
        var rightsId = new RightsId(nca.Header.RightsId);
        var zero = new RightsId();

        if (Crypto.CryptoUtil.IsSameBytes(rightsId.Value, zero.Value, Unsafe.SizeOf<RightsId>()))
            return Result.Success;

        // ReSharper disable once UnusedVariable
        Result res = _externalKeyManager.Get(rightsId, out AccessKey accessKey);
        if (res.IsFailure()) return res.Miss();

        // todo: Set key in nca reader

        return Result.Success;
    }

    private Result OpenStorageByContentType(ref SharedRef<IStorage> outNcaStorage, Nca nca,
        out NcaFormatType fsType, FileSystemProxyType fsProxyType, bool isGameCard, bool canMountSystemDataPrivate)
    {
        UnsafeHelpers.SkipParamInit(out fsType);

        NcaContentType contentType = nca.Header.ContentType;

        switch (fsProxyType)
        {
            case FileSystemProxyType.Code:
            case FileSystemProxyType.Rom:
            case FileSystemProxyType.Logo:
            case FileSystemProxyType.RegisteredUpdate:
                if (contentType != NcaContentType.Program)
                    return ResultFs.PreconditionViolation.Log();

                break;

            case FileSystemProxyType.Control:
                if (contentType != NcaContentType.Control)
                    return ResultFs.PreconditionViolation.Log();

                break;
            case FileSystemProxyType.Manual:
                if (contentType != NcaContentType.Manual)
                    return ResultFs.PreconditionViolation.Log();

                break;
            case FileSystemProxyType.Meta:
                if (contentType != NcaContentType.Meta)
                    return ResultFs.PreconditionViolation.Log();

                break;
            case FileSystemProxyType.Data:
                if (contentType != NcaContentType.Data && contentType != NcaContentType.PublicData)
                    return ResultFs.PreconditionViolation.Log();

                if (contentType == NcaContentType.Data && !canMountSystemDataPrivate)
                    return ResultFs.PermissionDenied.Log();

                break;
            default:
                return ResultFs.InvalidArgument.Log();
        }

        if (nca.Header.DistributionType == DistributionType.GameCard && !isGameCard)
            return ResultFs.PermissionDenied.Log();

        Result res = SetExternalKeyForRightsId(nca);
        if (res.IsFailure()) return res.Miss();

        res = GetPartitionIndex(out int sectionIndex, fsProxyType);
        if (res.IsFailure()) return res.Miss();

        res = _config.StorageOnNcaCreator.Create(ref outNcaStorage, out NcaFsHeader fsHeader, nca,
            sectionIndex, fsProxyType == FileSystemProxyType.Code);
        if (res.IsFailure()) return res.Miss();

        fsType = fsHeader.FormatType;
        return Result.Success;
    }

    public Result SetSdCardEncryptionSeed(in EncryptionSeed encryptionSeed)
    {
        _encryptionSeed = encryptionSeed;

        return Result.Success;
    }

    public Result ResolveRomReferenceProgramId(out ProgramId targetProgramId, ProgramId programId,
        byte programIndex)
    {
        UnsafeHelpers.SkipParamInit(out targetProgramId);

        ProgramId mainProgramId = _config.ProgramRegistryService.GetProgramIdByIndex(programId, programIndex);
        if (mainProgramId == ProgramId.InvalidId)
            return ResultFs.ProgramIndexNotFound.Log();

        targetProgramId = mainProgramId;
        return Result.Success;
    }

    public Result ResolveProgramPath(out bool isDirectory, ref Path path, ProgramId programId, StorageId storageId)
    {
        Result res = _locationResolverSet.ResolveProgramPath(out isDirectory, ref path, programId, storageId);
        if (res.IsSuccess())
            return Result.Success;

        isDirectory = false;

        res = _locationResolverSet.ResolveDataPath(ref path, new DataId(programId.Value), storageId);
        if (res.IsSuccess())
            return Result.Success;

        return ResultFs.TargetNotFound.Log();
    }

    public Result ResolveRomPath(out bool isDirectory, ref Path path, ulong id, StorageId storageId)
    {
        return _locationResolverSet.ResolveRomPath(out isDirectory, ref path, new ProgramId(id), storageId);
    }

    public Result ResolveApplicationHtmlDocumentPath(out bool isDirectory, ref Path path,
        Ncm.ApplicationId applicationId, StorageId storageId)
    {
        return _locationResolverSet.ResolveApplicationHtmlDocumentPath(out isDirectory, ref path, applicationId,
            storageId);
    }

    public Result ResolveRegisteredHtmlDocumentPath(ref Path path, ulong id)
    {
        return _locationResolverSet.ResolveRegisteredHtmlDocumentPath(ref path, id);
    }

    internal StorageLayoutType GetStorageFlag(ulong programId)
    {
        if (programId >= _config.SpeedEmulationRange.ProgramIdMin &&
            programId <= _config.SpeedEmulationRange.ProgramIdMax)
            return StorageLayoutType.Bis;
        else
            return StorageLayoutType.All;
    }

    public Result HandleResolubleAccessFailure(out bool wasDeferred, Result resultForNoFailureDetected,
        ulong processId)
    {
        throw new NotImplementedException();
    }

    public void IncrementRomFsRemountForDataCorruptionCount()
    {
        using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _romfsCountMutex);

        _romFsRemountForDataCorruptionCount++;
    }

    public void IncrementRomFsUnrecoverableDataCorruptionByRemountCount()
    {
        using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _romfsCountMutex);

        _romfsUnrecoverableDataCorruptionByRemountCount++;
    }

    public void IncrementRomFsRecoveredByInvalidateCacheCount()
    {
        using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _romfsCountMutex);

        _romFsRecoveredByInvalidateCacheCount++;
    }

    public void GetAndClearRomFsErrorInfo(out int recoveredByRemountCount, out int unrecoverableCount,
        out int recoveredByCacheInvalidationCount)
    {
        using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _romfsCountMutex);

        recoveredByRemountCount = _romFsRemountForDataCorruptionCount;
        unrecoverableCount = _romfsUnrecoverableDataCorruptionByRemountCount;
        recoveredByCacheInvalidationCount = _romFsRecoveredByInvalidateCacheCount;

        _romFsRemountForDataCorruptionCount = 0;
        _romfsUnrecoverableDataCorruptionByRemountCount = 0;
        _romFsRecoveredByInvalidateCacheCount = 0;
    }

    public Result OpenHostFileSystem(ref SharedRef<IFileSystem> outFileSystem, in Path rootPath, bool openCaseSensitive)
    {
        return _config.TargetManagerFsCreator.Create(ref outFileSystem, in rootPath, openCaseSensitive, false,
            Result.Success);
    }

    internal Result GetProgramInfoByProcessId(out ProgramInfo programInfo, ulong processId)
    {
        var registry = new ProgramRegistryImpl(_config.FsServer);
        return registry.GetProgramInfo(out programInfo, processId);
    }

    internal Result GetProgramInfoByProgramId(out ProgramInfo programInfo, ulong programId)
    {
        var registry = new ProgramRegistryImpl(_config.FsServer);
        return registry.GetProgramInfoByProgramId(out programInfo, programId);
    }

    private Result GetPartitionIndex(out int index, FileSystemProxyType fspType)
    {
        switch (fspType)
        {
            case FileSystemProxyType.Code:
            case FileSystemProxyType.Control:
            case FileSystemProxyType.Manual:
            case FileSystemProxyType.Meta:
            case FileSystemProxyType.Data:
                index = 0;
                return Result.Success;
            case FileSystemProxyType.Rom:
            case FileSystemProxyType.RegisteredUpdate:
                index = 1;
                return Result.Success;
            case FileSystemProxyType.Logo:
                index = 2;
                return Result.Success;
            default:
                UnsafeHelpers.SkipParamInit(out index);
                return ResultFs.InvalidArgument.Log();
        }
    }
}

public readonly struct InternalProgramIdRangeForSpeedEmulation
{
    public readonly ulong ProgramIdMin;
    public readonly ulong ProgramIdMax;

    public InternalProgramIdRangeForSpeedEmulation(ulong min, ulong max)
    {
        ProgramIdMin = min;
        ProgramIdMax = max;
    }
}