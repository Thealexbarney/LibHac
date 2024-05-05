using System;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Crypto;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv.FsCreator;
using LibHac.FsSrv.Impl;
using LibHac.FsSystem;
using LibHac.Lr;
using LibHac.Ncm;
using LibHac.Os;
using LibHac.Spl;
using LibHac.Util;
using static LibHac.Fs.Impl.CommonMountNames;
using static LibHac.FsSrv.Anonymous;
using Path = LibHac.Fs.Path;
using RightsId = LibHac.Fs.RightsId;

namespace LibHac.FsSrv;

file static class Anonymous
{
    public static Result GetDeviceHandleByMountName(out GameCardHandle outHandle, U8Span name)
    {
        const int handleStringLength = 8;

        UnsafeHelpers.SkipParamInit(out outHandle);

        if (StringUtils.GetLength(name, handleStringLength) < handleStringLength)
            return ResultFs.InvalidPath.Log();

        Span<byte> handleString = stackalloc byte[handleStringLength + 1];
        handleString.Clear();
        StringUtils.Copy(handleString, name, handleStringLength);

        bool handleParsed = Utf8Parser.TryParse(handleString, out GameCardHandle handle, out int bytesConsumed);
        if (!handleParsed || bytesConsumed != handleStringLength)
            return ResultFs.InvalidPath.Log();

        outHandle = handle;
        return Result.Success;
    }

    public static Result GetGameCardPartitionByMountName(out GameCardPartition outPartition, U8Span name)
    {
        if (StringUtils.Compare(name, GameCardFileSystemMountNameSuffixUpdate, 1) == 0)
        {
            outPartition = GameCardPartition.Update;
            return Result.Success;
        }

        if (StringUtils.Compare(name, GameCardFileSystemMountNameSuffixNormal, 1) == 0)
        {
            outPartition = GameCardPartition.Normal;
            return Result.Success;
        }

        if (StringUtils.Compare(name, GameCardFileSystemMountNameSuffixSecure, 1) == 0)
        {
            outPartition = GameCardPartition.Secure;
            return Result.Success;
        }

        outPartition = default;
        return ResultFs.InvalidPath.Log();
    }

    public static Result GetPartitionIndex(out int outIndex, FileSystemProxyType type)
    {
        switch (type)
        {
            case FileSystemProxyType.Code:
            case FileSystemProxyType.Control:
            case FileSystemProxyType.Manual:
            case FileSystemProxyType.Meta:
            case FileSystemProxyType.Data:
                outIndex = 0;
                return Result.Success;
            case FileSystemProxyType.Rom:
            case FileSystemProxyType.RegisteredUpdate:
                outIndex = 1;
                return Result.Success;
            case FileSystemProxyType.Logo:
                outIndex = 2;
                return Result.Success;
            default:
                UnsafeHelpers.SkipParamInit(out outIndex);
                return ResultFs.InvalidArgument.Log();
        }
    }

    public static void GenerateNcaDigest(out Hash outDigest, NcaReader reader1, NcaReader reader2)
    {
        UnsafeHelpers.SkipParamInit(out outDigest);

        Assert.SdkAssert(reader1 is not null || reader2 is not null);

        var generator = new Sha256Generator();
        generator.Initialize();

        if (reader1 is not null)
        {
            RuntimeNcaHeader header = reader1.GetHeader();
            generator.Update(SpanHelpers.AsReadOnlyByteSpan(in header));
        }

        if (reader2 is not null)
        {
            RuntimeNcaHeader header = reader2.GetHeader();
            generator.Update(SpanHelpers.AsReadOnlyByteSpan(in header));
        }

        generator.GetHash(SpanHelpers.AsByteSpan(ref outDigest));
    }

    public static Result LoadNspdVerificationData(ref CodeVerificationData outCodeVerificationData, IFileSystem fileSystem)
    {
        Assert.SdkRequiresNotNull(ref outCodeVerificationData);
        Assert.SdkRequiresNotNull(fileSystem);

        const int verificationDataSignatureSize = 0x100;
        const int verificationDataHashSize = 0x20;
        ReadOnlySpan<byte> verificationDataPath = "/verificationData"u8;

        using var verificationDataFile = new UniqueRef<IFile>();

        using var pathVerificationData = new Path();
        Result res = PathFunctions.SetUpFixedPath(ref pathVerificationData.Ref(), verificationDataPath);
        if (res.IsFailure()) return res.Miss();

        res = fileSystem.OpenFile(ref verificationDataFile.Ref, in pathVerificationData, OpenMode.Read);
        if (!res.IsSuccess())
        {
            if (ResultFs.PathNotFound.Includes(res))
            {
                return ResultFs.MissingNspdVerificationData.LogConverted(res);
            }

            return res.Miss();
        }

        res = ReadData(in verificationDataFile, ref outCodeVerificationData);
        if (res.IsFailure())
        {
            return ResultFs.InvalidNspdVerificationData.LogConverted(res);
        }

        return Result.Success;

        static Result ReadData(ref readonly UniqueRef<IFile> file, ref CodeVerificationData outData)
        {
            Result res = file.Get.GetSize(out long verificationDataSize);
            if (res.IsFailure()) return res.Miss();

            if (verificationDataSize != verificationDataSignatureSize + verificationDataHashSize)
                return ResultFs.InvalidNspdVerificationData.Log();

            return file.Get.Read(out _, 0, SpanHelpers.AsByteSpan(ref outData), ReadOption.None).Ret();
        }
    }
}

/// <summary>
/// Handles locating and opening NCA content files.
/// </summary>
/// <remarks>Based on nnSdk 17.5.0 (FS 17.0.0)</remarks>
public class NcaFileSystemServiceImpl : IDisposable
{
    private readonly Configuration _config;
    private readonly UpdatePartitionPath _updatePartitionPath;
    private readonly ExternalKeyManager _externalKeyManager;
    private readonly SystemDataUpdateEventManager _systemDataUpdateEventManager;
    private EncryptionSeed _encryptionSeed;
    private uint _romFsDeepRetryStartCount;
    private uint _romFsRemountForDataCorruptionCount;
    private uint _romfsUnrecoverableDataCorruptionByRemountCount;
    private uint _romFsRecoveredByInvalidateCacheCount;
    private uint _romFsUnrecoverableByGameCardAccessFailedCount;
    private SdkMutexType _romfsCountMutex;

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
        public INspRootFileSystemCreator NspRootFileSystemCreator;
        public LocationResolverSet LocationResolverSet;
        public ProgramRegistryServiceImpl ProgramRegistryService;
        public AccessFailureManagementServiceImpl AccessFailureManagementService;
        public InternalProgramIdRangeForSpeedEmulation SpeedEmulationRange;
        public long AddOnContentDivisionSize;
        public long RomDivisionSize;

        // LibHac additions
        public FileSystemServer FsServer;
    }

    private struct MountInfo
    {
        public enum FileSystemType
        {
            None,
            GameCard,
            HostFs,
            LocalFs
        }

        public FileSystemType FsType;
        public GameCardHandle GcHandle;
        public bool CanMountNca;

        public MountInfo()
        {
            FsType = FileSystemType.None;
            GcHandle = 0;
            CanMountNca = false;
        }

        public readonly bool IsGameCard() => FsType == FileSystemType.GameCard;
        public readonly bool IsHostOrLocalFs() => FsType == FileSystemType.HostFs || FsType == FileSystemType.LocalFs;
    }

    public FileSystemServer FsServer => _config.FsServer;

    public NcaFileSystemServiceImpl(in Configuration configuration)
    {
        _config = configuration;
        _updatePartitionPath = new UpdatePartitionPath();
        _externalKeyManager = new ExternalKeyManager();
        _systemDataUpdateEventManager = new SystemDataUpdateEventManager();

        _romFsDeepRetryStartCount = 0;
        _romFsRemountForDataCorruptionCount = 0;
        _romfsUnrecoverableDataCorruptionByRemountCount = 0;
        _romFsRecoveredByInvalidateCacheCount = 0;
        _romFsUnrecoverableByGameCardAccessFailedCount = 0;

        _romfsCountMutex = new SdkMutexType();
    }

    public void Dispose()
    {
        _updatePartitionPath.Dispose();
    }

    public long GetAddOnContentDivisionSize() => _config.AddOnContentDivisionSize;
    public long GetRomDivisionSize() => _config.RomDivisionSize;

    public Result OpenFileSystem(ref SharedRef<IFileSystem> outFileSystem, ref readonly Path path,
        ContentAttributes attributes, FileSystemProxyType type, bool canMountSystemDataPrivate, ulong id,
        bool isDirectory)
    {
        return OpenFileSystem(ref outFileSystem, ref Unsafe.NullRef<CodeVerificationData>(), in path, attributes, type,
            canMountSystemDataPrivate, id, isDirectory).Ret();
    }

    public Result OpenFileSystem(ref SharedRef<IFileSystem> outFileSystem, ref readonly Path path,
        ContentAttributes attributes, FileSystemProxyType type, ulong id, bool isDirectory)
    {
        return OpenFileSystem(ref outFileSystem, ref Unsafe.NullRef<CodeVerificationData>(), in path, attributes, type,
            canMountSystemDataPrivate: false, id, isDirectory).Ret();
    }

    public Result OpenFileSystem(ref SharedRef<IFileSystem> outFileSystem, ref CodeVerificationData outVerificationData,
        ref readonly Path path, ContentAttributes attributes, FileSystemProxyType type, bool canMountSystemDataPrivate,
        ulong id, bool isDirectory)
    {
        // Get a reference to the path that will be advanced as each part of the path is parsed
        var currentPath = new U8Span(path.GetString());

        var mountInfo = new MountInfo();

        if (!Unsafe.IsNullRef(ref outVerificationData))
            outVerificationData.HasData = false;

        // Open the root filesystem based on the path's mount name
        using var baseFileSystem = new SharedRef<IFileSystem>();
        Result res = ParseMountName(ref currentPath, ref baseFileSystem.Ref, ref mountInfo);
        if (res.IsFailure()) return res.Miss();

        if (mountInfo.IsGameCard() && type == FileSystemProxyType.Logo)
        {
            res = _config.BaseFsService.OpenGameCardFileSystem(ref outFileSystem, mountInfo.GcHandle, GameCardPartition.Logo);

            if (res.IsSuccess()) return Result.Success;

            if (!ResultFs.PartitionNotFound.Includes(res))
                return res.Miss();
        }

        if (isDirectory)
        {
            if (!mountInfo.IsHostOrLocalFs())
                return ResultFs.PermissionDenied.Log();

            using var directoryPath = new Path();
            res = directoryPath.InitializeWithNormalization(currentPath.Value);
            if (res.IsFailure()) return res.Miss();

            if (type == FileSystemProxyType.Manual)
            {
                using var hostFileSystem = new SharedRef<IFileSystem>();

                res = ParseDirWithPathCaseNormalizationOnCaseSensitiveHostOrLocalFs(ref hostFileSystem.Ref, in directoryPath, mountInfo.FsType);
                if (res.IsFailure()) return res.Miss();

                using var readOnlyFileSystem = new SharedRef<IFileSystem>(new ReadOnlyFileSystem(in hostFileSystem));
                outFileSystem.SetByMove(ref readOnlyFileSystem.Ref);

                return Result.Success;
            }

            using var ncdFileSystem = new SharedRef<IFileSystem>();
            res = _config.SubDirectoryFsCreator.Create(ref ncdFileSystem.Ref, in baseFileSystem, in directoryPath);
            if (res.IsFailure()) return res.Miss();

            using var fsPartitionOnNcdFileSystem = new SharedRef<IFileSystem>();
            res = ParseContentTypeForDirectory(ref fsPartitionOnNcdFileSystem.Ref, in ncdFileSystem, type);
            if (res.IsFailure()) return res.Miss();

            if (type == FileSystemProxyType.Code)
            {
                if (Unsafe.IsNullRef(ref outVerificationData))
                    return ResultFs.NullptrArgument.Log();

                res = LoadNspdVerificationData(ref outVerificationData, ncdFileSystem.Get);
                if (!res.IsSuccess())
                {
                    if (ResultFs.MissingNspdVerificationData.Includes(res))
                    {
                        outVerificationData.HasData = false;
                        outFileSystem.SetByMove(ref fsPartitionOnNcdFileSystem.Ref);
                        return Result.Success;
                    }

                    return res.Miss();
                }

                NpdmHash hash = default;
                outVerificationData.Hash[..].CopyTo(hash);

                using var codeFileSystem =
                    new SharedRef<NpdmVerificationFileSystem>(new NpdmVerificationFileSystem(in fsPartitionOnNcdFileSystem, hash));

                outFileSystem.SetByMove(ref codeFileSystem.Ref);

                return Result.Success;
            }
        }

        res = CheckNcaOrNsp(ref currentPath);
        if (res.IsFailure()) return res.Miss();

        bool foundNspPath;

        using (SharedRef<IFileSystem> baseFileSystemCopy = SharedRef<IFileSystem>.CreateCopy(in baseFileSystem))
        {
            res = ParseNsp(out foundNspPath, ref currentPath, ref baseFileSystem.Ref, in baseFileSystemCopy);
            if (res.IsFailure()) return res.Miss();
        }

        // Must be the end of the path to open Application Package FS type
        if (foundNspPath && currentPath.Value.At(0) == 0)
        {
            if (type != FileSystemProxyType.Package)
                return ResultFs.InvalidArgument.Log();

            outFileSystem.SetByMove(ref baseFileSystem.Ref);
            return Result.Success;
        }

        if (!mountInfo.CanMountNca)
            return ResultFs.UnexpectedInNcaFileSystemServiceImplA.Log();

        using var ncaReader = new SharedRef<NcaReader>();
        ulong openProgramId = mountInfo.IsHostOrLocalFs() ? ulong.MaxValue : id;
        res = ParseNca(ref ncaReader.Ref, ref baseFileSystem.Ref, currentPath, attributes, openProgramId);
        if (res.IsFailure()) return res.Miss();

        using var storage = new SharedRef<IStorage>();
        using var storageAccessSplitter = new SharedRef<IAsynchronousAccessSplitter>();
        res = OpenStorageByContentType(ref storage.Ref, ref storageAccessSplitter.Ref, in ncaReader,
            out NcaFsHeader.FsType fsType, type, mountInfo.IsGameCard(), canMountSystemDataPrivate);
        if (res.IsFailure()) return res.Miss();

        switch (fsType)
        {
            case NcaFsHeader.FsType.RomFs:
                Assert.SdkAssert(type != FileSystemProxyType.Code);

                return _config.RomFsCreator.Create(ref outFileSystem.Ref, in storage).Ret();

            case NcaFsHeader.FsType.PartitionFs:
                if (type == FileSystemProxyType.Code)
                {
                    if (Unsafe.IsNullRef(ref outVerificationData))
                        return ResultFs.NullptrArgument.Log();

                    ncaReader.Get.GetHeaderSign2(outVerificationData.Signature);
                    ncaReader.Get.GetHeaderSign2TargetHash(outVerificationData.Hash);
                    outVerificationData.HasData = true;
                }

                return _config.PartitionFsCreator.Create(ref outFileSystem.Ref, in storage).Ret();
            default:
                return ResultFs.InvalidNcaFileSystemType.Log();
        }
    }

    public Result OpenDataFileSystem(ref SharedRef<IFileSystem> outFileSystem, ref readonly Path path,
        ContentAttributes attributes, FileSystemProxyType type, ulong programId, bool isDirectory)
    {
        U8Span currentPath = path.GetString();
        var mountInfo = new MountInfo();

        if (!(type == FileSystemProxyType.Rom || type == FileSystemProxyType.Data && isDirectory))
        {
            return ResultFs.PreconditionViolation.Log();
        }

        using var fileSystem = new SharedRef<IFileSystem>();
        Result res = ParseMountName(ref currentPath, ref fileSystem.Ref, ref mountInfo);
        if (res.IsFailure()) return res.Miss();

        if (isDirectory)
        {
            if (!mountInfo.IsHostOrLocalFs())
                return ResultFs.PreconditionViolation.Log();

            using var hostFileSystem = new SharedRef<IFileSystem>();

            using var directoryPath = new Path();
            res = directoryPath.InitializeWithNormalization(currentPath);
            if (res.IsFailure()) return res.Miss();

            res = ParseDirWithPathCaseNormalizationOnCaseSensitiveHostOrLocalFs(ref hostFileSystem.Ref, in directoryPath, mountInfo.FsType);
            if (res.IsFailure()) return res.Miss();

            using var readOnlyFileSystem = new SharedRef<ReadOnlyFileSystem>(new ReadOnlyFileSystem(in hostFileSystem));

            outFileSystem.SetByMove(ref readOnlyFileSystem.Ref);
            return Result.Success;
        }

        res = CheckNcaOrNsp(ref currentPath);
        if (res.IsFailure()) return res.Miss();

        bool foundNspPath;

        using (SharedRef<IFileSystem> fileSystemCopy = SharedRef<IFileSystem>.CreateCopy(in fileSystem))
        {
            res = ParseNsp(out foundNspPath, ref currentPath, ref fileSystem.Ref, in fileSystemCopy);
            if (res.IsFailure()) return res.Miss();
        }

        // If we found an .nsp file in the file path, the portion of the path after the .nsp file will be used to open
        // a file inside the .nsp file.
        // We're trying to open an .nca file, so there must be something in the path after the .nsp file.
        if (foundNspPath && (currentPath.Length == 0 || currentPath[0] == 0))
            return ResultFs.TargetNotFound.Log();

        if (!mountInfo.CanMountNca)
            return ResultFs.UnexpectedInNcaFileSystemServiceImplA.Log();

        using var ncaReader = new SharedRef<NcaReader>();
        res = ParseNca(ref ncaReader.Ref, in fileSystem, currentPath, attributes, programId);
        if (res.IsFailure()) return res.Miss();

        using var storage = new SharedRef<IStorage>();
        using var storageAccessSplitter = new SharedRef<IAsynchronousAccessSplitter>();
        res = OpenStorageByContentType(ref storage.Ref, ref storageAccessSplitter.Ref, in ncaReader,
            out NcaFsHeader.FsType fsType, type, mountInfo.IsGameCard(), canMountSystemDataPrivate: false);
        if (res.IsFailure()) return res.Miss();

        if (fsType != NcaFsHeader.FsType.RomFs)
            return ResultFs.PreconditionViolation.Log();

        return _config.RomFsCreator.Create(ref outFileSystem, in storage).Ret();
    }

    public Result OpenDataStorage(ref SharedRef<IStorage> outStorage,
        ref SharedRef<IAsynchronousAccessSplitter> outStorageAccessSplitter, ref Hash outNcaDigest,
        ref readonly Path path, ContentAttributes attributes, FileSystemProxyType type, ulong id)
    {
        return OpenDataStorage(ref outStorage, ref outStorageAccessSplitter, ref outNcaDigest, in path, attributes,
            type, id, canMountSystemDataPrivate: false).Ret();
    }

    public Result OpenDataStorage(ref SharedRef<IStorage> outStorage,
        ref SharedRef<IAsynchronousAccessSplitter> outStorageAccessSplitter, ref Hash outNcaDigest,
        ref readonly Path path, ContentAttributes attributes, FileSystemProxyType type, ulong id,
        bool canMountSystemDataPrivate)
    {
        using var ncaReader = new SharedRef<NcaReader>();
        Result res = ParseNca(ref ncaReader.Ref, out bool isGameCard, path.GetString(), attributes, id);
        if (res.IsFailure()) return res.Miss();

        if (!Unsafe.IsNullRef(in outNcaDigest))
        {
            GenerateNcaDigest(out outNcaDigest, ncaReader.Get, null);
        }

        res = OpenStorageByContentType(ref outStorage, ref outStorageAccessSplitter, in ncaReader,
            out NcaFsHeader.FsType fsType, type, isGameCard, canMountSystemDataPrivate);
        if (res.IsFailure()) return res.Miss();

        if (fsType != NcaFsHeader.FsType.RomFs)
            return ResultFs.PreconditionViolation.Log();

        return Result.Success;
    }

    public Result OpenStorageWithPatch(ref SharedRef<IStorage> outStorage,
        ref SharedRef<IAsynchronousAccessSplitter> outStorageAccessSplitter, ref Hash outNcaDigest,
        ref readonly Path originalNcaPath, ContentAttributes originalAttributes, ref readonly Path currentNcaPath,
        ContentAttributes currentAttributes, FileSystemProxyType type, ulong originalId, ulong currentId)
    {
        return OpenStorageWithPatch(ref outStorage, ref outStorageAccessSplitter, ref outNcaDigest,
            in originalNcaPath, originalAttributes, in currentNcaPath, currentAttributes, type, originalId, currentId,
            false).Ret();
    }

    public Result OpenStorageWithPatch(ref SharedRef<IStorage> outStorage,
        ref SharedRef<IAsynchronousAccessSplitter> outStorageAccessSplitter, ref Hash outNcaDigest,
        ref readonly Path originalNcaPath, ContentAttributes originalAttributes, ref readonly Path currentNcaPath,
        ContentAttributes currentAttributes, FileSystemProxyType type, ulong originalId, ulong currentId,
        bool canMountSystemDataPrivate)
    {
        Result res;
        bool isOriginalGameCard = false;
        using var originalNcaReader = new SharedRef<NcaReader>();

        if (!PathExtensions.IsNullRef(in originalNcaPath))
        {
            res = ParseNca(ref originalNcaReader.Ref, out isOriginalGameCard, originalNcaPath.GetString(),
                originalAttributes, originalId);
            if (res.IsFailure()) return res.Miss();

            if (originalNcaReader.Get.GetDistributionType() == NcaHeader.DistributionType.GameCard && !isOriginalGameCard)
                return ResultFs.PermissionDenied.Log();
        }

        if (isOriginalGameCard)
            originalNcaReader.Get.PrioritizeSwAes();

        using var currentNcaReader = new SharedRef<NcaReader>();
        res = ParseNca(ref currentNcaReader.Ref, out bool isCurrentGameCard, currentNcaPath.GetString(),
            currentAttributes, currentId);
        if (res.IsFailure()) return res.Miss();

        if (currentNcaReader.Get.GetDistributionType() == NcaHeader.DistributionType.GameCard && !isCurrentGameCard)
            return ResultFs.PermissionDenied.Log();

        if (isCurrentGameCard)
            currentNcaReader.Get.PrioritizeSwAes();

        if (!Unsafe.IsNullRef(in outNcaDigest))
        {
            GenerateNcaDigest(out outNcaDigest, originalNcaReader.Get, currentNcaReader.Get);
        }

        res = OpenStorageWithPatchByContentType(ref outStorage, ref outStorageAccessSplitter, in originalNcaReader,
            in currentNcaReader, out NcaFsHeader.FsType fsType, type, canMountSystemDataPrivate);
        if (res.IsFailure()) return res.Miss();

        if (fsType != NcaFsHeader.FsType.RomFs)
            return ResultFs.PreconditionViolation.Log();

        return Result.Success;
    }

    public Result OpenFileSystemWithPatch(ref SharedRef<IFileSystem> outFileSystem, ref readonly Path originalNcaPath,
        ContentAttributes originalAttributes, ref readonly Path currentNcaPath, ContentAttributes currentAttributes,
        FileSystemProxyType type, ulong originalId, ulong currentId)
    {
        using var storage = new SharedRef<IStorage>();
        using var storageAccessSplitter = new SharedRef<IAsynchronousAccessSplitter>();
        using var fileSystem = new SharedRef<IFileSystem>();

        Result res = OpenStorageWithPatch(ref storage.Ref, ref storageAccessSplitter.Ref, ref Unsafe.NullRef<Hash>(),
            in originalNcaPath, originalAttributes, in currentNcaPath, currentAttributes, type, originalId, currentId,
            canMountSystemDataPrivate: false);
        if (res.IsFailure()) return res.Miss();

        res = _config.RomFsCreator.Create(ref fileSystem.Ref, in storage);
        if (res.IsFailure()) return res.Miss();

        outFileSystem.SetByMove(ref fileSystem.Ref);
        return Result.Success;
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
                res = _config.BaseFsService.OpenBisFileSystem(ref fileSystem.Ref, BisPartitionId.System);
                if (res.IsFailure()) return res.Miss();
                break;
            case ContentStorageId.User:
                res = _config.BaseFsService.OpenBisFileSystem(ref fileSystem.Ref, BisPartitionId.User);
                if (res.IsFailure()) return res.Miss();
                break;
            case ContentStorageId.SdCard:
                res = _config.BaseFsService.OpenSdCardProxyFileSystem(ref fileSystem.Ref);
                if (res.IsFailure()) return res.Miss();
                break;
            case ContentStorageId.System0:
                res = _config.BaseFsService.OpenBisFileSystem(ref fileSystem.Ref, BisPartitionId.System0);
                if (res.IsFailure()) return res.Miss();
                break;
            default:
                return ResultFs.InvalidArgument.Log();
        }

        Span<byte> contentStoragePathBuffer = stackalloc byte[64];

        // Build the appropriate path for the content storage ID
        if (contentStorageId == ContentStorageId.SdCard)
        {
            var sb = new U8StringBuilder(contentStoragePathBuffer);
            sb.Append(StringTraits.DirectorySeparator).Append(CommonDirNames.SdCardNintendoRootDirectoryName);
            sb.Append(StringTraits.DirectorySeparator).Append(CommonDirNames.ContentStorageDirectoryName);
        }
        else
        {
            var sb = new U8StringBuilder(contentStoragePathBuffer);
            sb.Append(StringTraits.DirectorySeparator).Append(CommonDirNames.ContentStorageDirectoryName);
        }

        using scoped var contentStoragePath = new Path();
        res = PathFunctions.SetUpFixedPath(ref contentStoragePath.Ref(), contentStoragePathBuffer);
        if (res.IsFailure()) return res.Miss();

        // Make sure the content storage path exists
        res = FsSystem.Utility.EnsureDirectory(fileSystem.Get, in contentStoragePath);
        if (res.IsFailure()) return res.Miss();

        using var subDirFs = new SharedRef<IFileSystem>();
        res = _config.SubDirectoryFsCreator.Create(ref subDirFs.Ref, in fileSystem, in contentStoragePath);
        if (res.IsFailure()) return res.Miss();

        // Only content on the SD card is encrypted
        if (contentStorageId == ContentStorageId.SdCard)
        {
            using SharedRef<IFileSystem> tempFileSystem = SharedRef<IFileSystem>.CreateMove(ref subDirFs.Ref);
            res = _config.EncryptedFsCreator.Create(ref subDirFs.Ref, in tempFileSystem,
                IEncryptedFileSystemCreator.KeyId.Content, in _encryptionSeed);
            if (res.IsFailure()) return res.Miss();
        }

        outFileSystem.SetByMove(ref subDirFs.Ref);
        return Result.Success;
    }

    public Result GetRightsId(out RightsId outRightsId, out byte outKeyGeneration, ref readonly Path path,
        ContentAttributes attributes, ProgramId programId)
    {
        UnsafeHelpers.SkipParamInit(out outRightsId, out outKeyGeneration);

        using var ncaReader = new SharedRef<NcaReader>();
        Result res = ParseNca(ref ncaReader.Ref, out _, path.GetString(), attributes, programId.Value);
        if (res.IsFailure()) return res.Miss();

        ncaReader.Get.GetRightsId(SpanHelpers.AsByteSpan(ref outRightsId));
        outKeyGeneration = ncaReader.Get.GetKeyGeneration();

        return Result.Success;
    }

    public Result GetProgramId(out ProgramId outProgramId, ref readonly Path path, ContentAttributes attributes)
    {
        UnsafeHelpers.SkipParamInit(out outProgramId);

        using var ncaReader = new SharedRef<NcaReader>();
        Result res = ParseNca(ref ncaReader.Ref, out _, path.GetString(), attributes, ulong.MaxValue);
        if (res.IsFailure()) return res.Miss();

        outProgramId = new ProgramId(ncaReader.Get.GetProgramId());
        return Result.Success;
    }

    public Result RegisterExternalKey(in RightsId rightsId, in AccessKey accessKey)
    {
        return _externalKeyManager.Register(in rightsId, in accessKey).Ret();
    }

    public Result UnregisterExternalKey(in RightsId rightsId)
    {
        return _externalKeyManager.Unregister(in rightsId).Ret();
    }

    public Result UnregisterAllExternalKey()
    {
        return _externalKeyManager.UnregisterAll().Ret();
    }

    public Result RegisterUpdatePartition(ulong programId, ref readonly Path path, ContentAttributes attributes)
    {
        return _updatePartitionPath.Set(programId, in path, attributes).Ret();
    }

    public Result OpenRegisteredUpdatePartition(ref SharedRef<IFileSystem> outFileSystem)
    {
        using var path = new Path();
        Result res = _updatePartitionPath.Get(ref path.Ref(), out ContentAttributes contentAttributes, out ulong updaterProgramId);
        if (res.IsFailure()) return res.Miss();

        return OpenFileSystem(ref outFileSystem, in path, contentAttributes, FileSystemProxyType.RegisteredUpdate,
            updaterProgramId, isDirectory: false).Ret();
    }

    private Result ParseMountName(ref U8Span path, ref SharedRef<IFileSystem> outFileSystem, ref MountInfo outMountInfo)
    {
        outMountInfo.FsType = MountInfo.FileSystemType.None;
        outMountInfo.CanMountNca = false;

        if (StringUtils.Compare(path, GameCardFileSystemMountName,
            GameCardFileSystemMountName.Length) == 0)
        {
            path = path.Slice(GameCardFileSystemMountName.Length);
            Result res = GetGameCardPartitionByMountName(out GameCardPartition partition, path);
            if (res.IsFailure()) return res.Miss();

            path = path.Slice(1);
            res = GetDeviceHandleByMountName(out GameCardHandle handle, path);
            if (res.IsFailure()) return res.Miss();

            path = path.Slice(8);
            res = _config.BaseFsService.OpenGameCardFileSystem(ref outFileSystem, handle, partition);
            if (res.IsFailure()) return res.Miss();

            outMountInfo.GcHandle = handle;
            outMountInfo.FsType = MountInfo.FileSystemType.GameCard;
            outMountInfo.CanMountNca = true;
        }

        else if (StringUtils.Compare(path, ContentStorageSystemMountName,
            ContentStorageSystemMountName.Length) == 0)
        {
            path = path.Slice(ContentStorageSystemMountName.Length);

            Result res = OpenContentStorageFileSystem(ref outFileSystem, ContentStorageId.System);
            if (res.IsFailure()) return res.Miss();

            outMountInfo.CanMountNca = true;
        }

        else if (StringUtils.Compare(path, ContentStorageUserMountName,
            ContentStorageUserMountName.Length) == 0)
        {
            path = path.Slice(ContentStorageUserMountName.Length);

            Result res = OpenContentStorageFileSystem(ref outFileSystem, ContentStorageId.User);
            if (res.IsFailure()) return res.Miss();

            outMountInfo.CanMountNca = true;
        }

        else if (StringUtils.Compare(path, ContentStorageSdCardMountName,
            ContentStorageSdCardMountName.Length) == 0)
        {
            path = path.Slice(ContentStorageSdCardMountName.Length);

            Result res = OpenContentStorageFileSystem(ref outFileSystem, ContentStorageId.SdCard);
            if (res.IsFailure()) return res.Miss();

            outMountInfo.CanMountNca = true;
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

        else if (StringUtils.Compare(path, BisSystemPartition0MountName,
            BisSystemPartition0MountName.Length) == 0)
        {
            path = path.Slice(BisSystemPartition0MountName.Length);

            Result res = _config.BaseFsService.OpenBisFileSystem(ref outFileSystem, BisPartitionId.System0);
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

            res = OpenHostFileSystem(ref outFileSystem, in rootPathEmpty, openCaseSensitive: false);
            if (res.IsFailure()) return res.Miss();

            outMountInfo.FsType = MountInfo.FileSystemType.HostFs;
            outMountInfo.CanMountNca = true;
        }

        else if (StringUtils.Compare(path, LocalRootFileSystemMountName,
            LocalRootFileSystemMountName.Length) == 0)
        {
            path = path.Slice(LocalRootFileSystemMountName.Length);

            using var rootPathEmpty = new Path();
            Result res = rootPathEmpty.InitializeAsEmpty();
            if (res.IsFailure()) return res.Miss();

            res = _config.LocalFsCreator.Create(ref outFileSystem, in rootPathEmpty, openCaseSensitive: false);
            if (res.IsFailure()) return res.Miss();

            outMountInfo.FsType = MountInfo.FileSystemType.LocalFs;
            outMountInfo.CanMountNca = true;
        }

        else if (StringUtils.Compare(path, RegisteredUpdatePartitionMountName,
            RegisteredUpdatePartitionMountName.Length) == 0)
        {
            path = path.Slice(RegisteredUpdatePartitionMountName.Length);

            Result res = OpenRegisteredUpdatePartition(ref outFileSystem);
            if (res.IsFailure()) return res.Miss();

            outMountInfo.CanMountNca = true;
        }
        else
        {
            return ResultFs.PathNotFound.Log();
        }

        if (StringUtils.GetLength(path, PathTool.EntryNameLengthMax) == 0)
            return ResultFs.PathNotFound.Log();

        if (path[0] == (byte)':')
        {
            path = path.Slice(1);
        }

        return Result.Success;
    }

    private Result CheckNcaOrNsp(ref U8Span path)
    {
        ReadOnlySpan<byte> ncaExtension = ".nca"u8;
        ReadOnlySpan<byte> nspExtension = ".nsp"u8;

        int pathLen = StringUtils.GetLength(path);

        // Now make sure the path has a content file extension
        if (pathLen <= 4)
            return ResultFs.PathNotFound.Log();

        ReadOnlySpan<byte> fileExtension = path.Value.Slice(pathLen - 4);

        if (StringUtils.CompareCaseInsensitive(fileExtension, ncaExtension) == 0)
            return Result.Success;

        if (StringUtils.CompareCaseInsensitive(fileExtension, nspExtension) == 0)
            return Result.Success;

        return ResultFs.PathNotFound.Log();
    }

    private Result ParseDirWithPathCaseNormalizationOnCaseSensitiveHostOrLocalFs(
        ref SharedRef<IFileSystem> outFileSystem, ref readonly Path path, MountInfo.FileSystemType fsType)
    {
        using var pathRoot = new Path();
        using var pathData = new Path();

        Result res = PathFunctions.SetUpFixedPath(ref pathData.Ref(), "/data"u8);
        if (res.IsFailure()) return res.Miss();

        res = pathRoot.Combine(in path, in pathData);
        if (res.IsFailure()) return res.Miss();

        switch (fsType)
        {
            case MountInfo.FileSystemType.HostFs:
                res = OpenHostFileSystem(ref outFileSystem, in pathRoot, openCaseSensitive: true);
                if (res.IsFailure()) return res.Miss();
                break;
            
            case MountInfo.FileSystemType.LocalFs:
                res = _config.LocalFsCreator.Create(ref outFileSystem, in pathRoot, openCaseSensitive: true);
                if (res.IsFailure()) return res.Miss();
                break;

            default:
                Abort.UnexpectedDefault();
                break;
        }

        return Result.Success;
    }

    private Result ParseNsp(out bool outFoundNspPath, ref U8Span path, ref SharedRef<IFileSystem> outFileSystem,
        ref readonly SharedRef<IFileSystem> baseFileSystem)
    {
        UnsafeHelpers.SkipParamInit(out outFoundNspPath);

        ReadOnlySpan<byte> nspExtension = ".nsp"u8;
        const int nspExtensionSize = 4;

        // Search for the end of the nsp part of the path
        int nspPathLen = 0;

        while (true)
        {
            U8Span currentSpan = path.Slice(nspPathLen);

            if (StringUtils.CompareCaseInsensitive(currentSpan, nspExtension, nspExtensionSize) == 0)
            {
                // The nsp filename must be the end of the entire path or the end of a path segment
                if (currentSpan.Length <= 4 || currentSpan[4] == 0 || currentSpan[4] == (byte)'/')
                    break;

                nspPathLen += nspExtensionSize;
            }
            else if (currentSpan.Length == 0 || currentSpan[0] == 0)
            {
                outFoundNspPath = false;
                return Result.Success;
            }
            else
            {
                nspPathLen++;
            }
        }

        nspPathLen += nspExtensionSize;

        using var nspPath = new Path();
        Result res = nspPath.InitializeWithNormalization(path, nspPathLen);
        if (res.IsFailure()) return res.Miss();

        using var fileStorage = new SharedRef<FileStorageBasedFileSystem>(new FileStorageBasedFileSystem());
        res = fileStorage.Get.Initialize(in baseFileSystem, in nspPath, OpenMode.Read);
        if (res.IsFailure()) return res.Miss();

        using SharedRef<IStorage> tempStorage = SharedRef<IStorage>.CreateMove(ref fileStorage.Ref);
        res = _config.NspRootFileSystemCreator.Create(ref outFileSystem, in tempStorage);
        if (res.IsFailure()) return res.Miss();

        path = path.Slice(nspPathLen);
        outFoundNspPath = true;

        return Result.Success;
    }

    private Result ParseNca(ref SharedRef<NcaReader> outNcaReader, ref readonly SharedRef<IFileSystem> baseFileSystem,
        U8Span path, ContentAttributes attributes, ulong programId)
    {
        using var fileStorage = new SharedRef<FileStorageBasedFileSystem>(new FileStorageBasedFileSystem());

        using var pathNca = new Path();
        Result res = pathNca.InitializeWithNormalization(path);
        if (res.IsFailure()) return res.Miss();

        res = fileStorage.Get.Initialize(in baseFileSystem, in pathNca, OpenMode.Read);
        if (res.IsFailure()) return res.Miss();

        using var ncaReader = new SharedRef<NcaReader>();
        using SharedRef<IStorage> tempStorage = SharedRef<IStorage>.CreateMove(ref fileStorage.Ref);
        res = _config.StorageOnNcaCreator.CreateNcaReader(ref ncaReader.Ref, in tempStorage, attributes);
        if (res.IsFailure()) return res.Miss();

        if (programId != ulong.MaxValue)
        {
            ulong ncaProgramId = ncaReader.Get.GetProgramId();

            if (ncaProgramId != ulong.MaxValue && programId != ncaProgramId)
            {
                return ResultFs.InvalidNcaId.Log();
            }
        }

        outNcaReader.SetByMove(ref ncaReader.Ref);

        return Result.Success;
    }

    private Result ParseNca(ref SharedRef<NcaReader> outNcaReader, out bool outIsGameCard, U8Span path,
        ContentAttributes attributes, ulong programId)
    {
        UnsafeHelpers.SkipParamInit(out outIsGameCard);

        U8Span currentPath = path;

        var mountInfo = new MountInfo();
        using var fileSystem = new SharedRef<IFileSystem>();
        Result res = ParseMountName(ref currentPath, ref fileSystem.Ref, ref mountInfo);
        if (res.IsFailure()) return res.Miss();

        outIsGameCard = mountInfo.IsGameCard();

        res = CheckNcaOrNsp(ref currentPath);
        if (res.IsFailure()) return res.Miss();

        bool foundNspPath;

        using (SharedRef<IFileSystem> fileSystemCopy = SharedRef<IFileSystem>.CreateCopy(in fileSystem))
        {
            res = ParseNsp(out foundNspPath, ref currentPath, ref fileSystem.Ref, in fileSystemCopy);
            if (res.IsFailure()) return res.Miss();
        }

        // If we found an .nsp file in the file path, the portion of the path after the .nsp file will be used to open
        // a file inside the .nsp file.
        // We're trying to open an .nca file, so there must be something in the path after the .nsp file.
        if (foundNspPath && (currentPath.Length == 0 || currentPath[0] == 0))
            return ResultFs.TargetNotFound.Log();

        if (!mountInfo.CanMountNca)
            return ResultFs.UnexpectedInNcaFileSystemServiceImplA.Log();

        res = ParseNca(ref outNcaReader, in fileSystem, currentPath, attributes, programId);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    private Result ParseContentTypeForDirectory(ref SharedRef<IFileSystem> outFileSystem,
        ref readonly SharedRef<IFileSystem> baseFileSystem, FileSystemProxyType type)
    {
        Span<byte> directoryPathBuffer = stackalloc byte[0x10];

        // Get the name of the subdirectory for the filesystem type
        switch (type)
        {
            case FileSystemProxyType.Code:
                StringUtils.Strlcpy(directoryPathBuffer, "/code"u8, 16);
                break;

            case FileSystemProxyType.Logo:
                StringUtils.Strlcpy(directoryPathBuffer, "/logo"u8, 16);
                break;

            case FileSystemProxyType.Rom:
            case FileSystemProxyType.Control:
            case FileSystemProxyType.Manual:
            case FileSystemProxyType.Meta:
            case FileSystemProxyType.RegisteredUpdate:
                StringUtils.Strlcpy(directoryPathBuffer, "/data"u8, 16);
                break;

            case FileSystemProxyType.Package:
                outFileSystem.SetByCopy(in baseFileSystem);
                return Result.Success;

            default:
                return ResultFs.InvalidArgument.Log();
        }

        using scoped var directoryPath = new Path();
        Result res = PathFunctions.SetUpFixedPath(ref directoryPath.Ref(), directoryPathBuffer);
        if (res.IsFailure()) return res.Miss();

        if (directoryPath.IsEmpty())
            return ResultFs.InvalidArgument.Log();

        // Open the subdirectory filesystem
        using var fileSystem = new SharedRef<IFileSystem>();
        res = _config.SubDirectoryFsCreator.Create(ref fileSystem.Ref, in baseFileSystem, in directoryPath);
        if (res.IsFailure()) return res.Miss();

        outFileSystem.SetByMove(ref fileSystem.Ref);
        return Result.Success;
    }

    public Result SetExternalKeyForRightsId(NcaReader ncaReader)
    {
        Span<byte> zeroRightsId = stackalloc byte[Unsafe.SizeOf<RightsId>()];
        Span<byte> rightsId = stackalloc byte[Unsafe.SizeOf<RightsId>()];

        zeroRightsId.Clear();
        ncaReader.GetRightsId(rightsId);

        bool hasRightsId = !CryptoUtil.IsSameBytes(rightsId, zeroRightsId, Unsafe.SizeOf<RightsId>());

        if (hasRightsId)
        {
            Result res = _externalKeyManager.Find(out AccessKey keySource, SpanHelpers.AsStruct<RightsId>(rightsId));
            if (res.IsFailure()) return res.Miss();

            ncaReader.SetExternalDecryptionKey(in keySource);
        }

        return Result.Success;
    }

    public bool IsAvailableKeySource(ReadOnlySpan<byte> keySource)
    {
        return _externalKeyManager.IsAvailableKeySource(keySource);
    }

    public Result OpenStorageByContentType(ref SharedRef<IStorage> outNcaStorage,
        ref SharedRef<IAsynchronousAccessSplitter> outStorageAccessSplitter,
        ref readonly SharedRef<NcaReader> ncaReader, out NcaFsHeader.FsType outFsType, FileSystemProxyType fsProxyType,
        bool isGameCard, bool canMountSystemDataPrivate)
    {
        UnsafeHelpers.SkipParamInit(out outFsType);

        NcaHeader.ContentType contentType = ncaReader.Get.GetContentType();

        switch (fsProxyType)
        {
            case FileSystemProxyType.Code:
                if (contentType != NcaHeader.ContentType.Program)
                    return ResultFs.PreconditionViolation.Log();
                break;

            case FileSystemProxyType.Rom:
                if (contentType != NcaHeader.ContentType.Program)
                    return ResultFs.PreconditionViolation.Log();
                break;

            case FileSystemProxyType.Logo:
                if (contentType != NcaHeader.ContentType.Program)
                    return ResultFs.PreconditionViolation.Log();
                break;

            case FileSystemProxyType.Control:
                if (contentType != NcaHeader.ContentType.Control)
                    return ResultFs.PreconditionViolation.Log();
                break;

            case FileSystemProxyType.Manual:
                if (contentType != NcaHeader.ContentType.Manual)
                    return ResultFs.PreconditionViolation.Log();
                break;

            case FileSystemProxyType.Meta:
                if (contentType != NcaHeader.ContentType.Meta)
                    return ResultFs.PreconditionViolation.Log();
                break;

            case FileSystemProxyType.Data:
                if (contentType != NcaHeader.ContentType.Data && contentType != NcaHeader.ContentType.PublicData)
                    return ResultFs.PreconditionViolation.Log();
                break;

            case FileSystemProxyType.RegisteredUpdate:
                if (contentType != NcaHeader.ContentType.Program)
                    return ResultFs.PreconditionViolation.Log();
                break;

            default:
                return ResultFs.InvalidArgument.Log();
        }

        if (contentType == NcaHeader.ContentType.Data && !canMountSystemDataPrivate)
            return ResultFs.PermissionDenied.Log();

        if (ncaReader.Get.GetDistributionType() == NcaHeader.DistributionType.GameCard && !isGameCard)
            return ResultFs.PermissionDenied.Log();

        if (isGameCard)
        {
            ncaReader.Get.PrioritizeSwAes();
        }

        Result res = SetExternalKeyForRightsId(ncaReader.Get);
        if (res.IsFailure()) return res.Miss();

        res = GetPartitionIndex(out int partitionIndex, fsProxyType);
        if (res.IsFailure()) return res.Miss();

        var ncaFsHeaderReader = new NcaFsHeaderReader();

        res = _config.StorageOnNcaCreator.Create(ref outNcaStorage, ref outStorageAccessSplitter, ref ncaFsHeaderReader,
            in ncaReader, partitionIndex);
        if (res.IsFailure()) return res.Miss();

        outFsType = ncaFsHeaderReader.GetFsType();
        return Result.Success;
    }

    public Result OpenStorageWithPatchByContentType(ref SharedRef<IStorage> outNcaStorage,
        ref SharedRef<IAsynchronousAccessSplitter> outStorageAccessSplitter,
        ref readonly SharedRef<NcaReader> originalNcaReader, ref readonly SharedRef<NcaReader> currentNcaReader,
        out NcaFsHeader.FsType outFsType, FileSystemProxyType fsProxyType, bool canMountSystemDataPrivate)
    {
        UnsafeHelpers.SkipParamInit(out outFsType);

        NcaHeader.ContentType contentType = currentNcaReader.Get.GetContentType();

        switch (fsProxyType)
        {
            case FileSystemProxyType.Rom:
                if (contentType != NcaHeader.ContentType.Program)
                    return ResultFs.PreconditionViolation.Log();
                break;

            case FileSystemProxyType.Manual:
                if (contentType != NcaHeader.ContentType.Manual)
                    return ResultFs.PreconditionViolation.Log();
                break;

            case FileSystemProxyType.Data:
                if (contentType != NcaHeader.ContentType.Data && contentType != NcaHeader.ContentType.PublicData)
                    return ResultFs.PreconditionViolation.Log();
                break;

            default:
                return ResultFs.InvalidArgument.Log();
        }

        if (contentType == NcaHeader.ContentType.Data && !canMountSystemDataPrivate)
            return ResultFs.PermissionDenied.Log();

        Result res = SetExternalKeyForRightsId(currentNcaReader.Get);
        if (res.IsFailure()) return res.Miss();

        if (originalNcaReader.HasValue)
        {
            if (originalNcaReader.Get.GetContentType() != contentType)
                return ResultFs.PreconditionViolation.Log();

            res = SetExternalKeyForRightsId(originalNcaReader.Get);
            if (res.IsFailure()) return res.Miss();
        }

        res = GetPartitionIndex(out int partitionIndex, fsProxyType);
        if (res.IsFailure()) return res.Miss();

        var ncaFsHeaderReader = new NcaFsHeaderReader();

        res = _config.StorageOnNcaCreator.CreateWithPatch(ref outNcaStorage, ref outStorageAccessSplitter,
            ref ncaFsHeaderReader, in originalNcaReader, in currentNcaReader, partitionIndex);
        if (res.IsFailure()) return res.Miss();

        outFsType = ncaFsHeaderReader.GetFsType();
        return Result.Success;
    }

    public Result SetSdCardEncryptionSeed(in EncryptionSeed encryptionSeed)
    {
        _encryptionSeed = encryptionSeed;

        return Result.Success;
    }

    public Result ResolveRomReferenceProgramId(out ProgramId outTargetProgramId, ProgramId programId,
        byte programIndex)
    {
        UnsafeHelpers.SkipParamInit(out outTargetProgramId);

        ProgramId targetProgramId = _config.ProgramRegistryService.GetProgramIdByIndex(programId, programIndex);
        if (targetProgramId == ProgramId.InvalidId)
            return ResultFs.ProgramIndexNotFound.Log();

        outTargetProgramId = targetProgramId;
        return Result.Success;
    }

    public Result ResolveProgramPath(out bool outIsDirectory, ref Path outPath, out ContentAttributes outContentAttributes,
        ProgramId programId, StorageId storageId)
    {
        Result res = _config.LocationResolverSet.ResolveProgramPath(out outIsDirectory, ref outPath,
            out outContentAttributes, programId.Value, storageId);
        if (res.IsSuccess())
            return Result.Success;

        outIsDirectory = false;

        res = _config.LocationResolverSet.ResolveDataPath(ref outPath, out outContentAttributes,
            new DataId(programId.Value), storageId);
        if (res.IsSuccess())
            return Result.Success;

        return ResultFs.TargetNotFound.Log();
    }

    public Result ResolveApplicationControlPath(ref Path outPath, out ContentAttributes outContentAttributes,
        Ncm.ApplicationId applicationId, StorageId storageId)
    {
        return _config.LocationResolverSet
            .ResolveApplicationControlPath(ref outPath, out outContentAttributes, applicationId, storageId).Ret();
    }

    public Result ResolveRomPath(out bool outIsDirectory, ref Path outPath, out ContentAttributes outContentAttributes,
        out ulong outOriginalProgramId, ulong programId, StorageId storageId)
    {
        UnsafeHelpers.SkipParamInit(out outOriginalProgramId);

        Result res = _config.LocationResolverSet.ResolveRomPath(out outIsDirectory, ref outPath,
            out outContentAttributes, programId, storageId);

        if (!res.IsSuccess())
        {
            if (ResultLr.DebugProgramNotFound.Includes(res))
            {
                ProgramId targetProgramId = _config.ProgramRegistryService.GetApplicationProgramProgramIdByPatchProgramProgramId(new ProgramId(programId));
                if (targetProgramId == ProgramId.InvalidId)
                    return res.Rethrow();

                res = _config.LocationResolverSet.ResolveRomPath(out outIsDirectory, ref outPath,
                    out outContentAttributes, targetProgramId.Value, storageId);
                if (res.IsFailure()) return res.Miss();

                outOriginalProgramId = targetProgramId.Value;
            }

            return res.Miss();
        }

        outOriginalProgramId = programId;
        return Result.Success;
    }

    public Result ResolveApplicationHtmlDocumentPath(out bool outIsDirectory, ref Path outPath,
        out ContentAttributes outContentAttributes, out ulong outOriginalProgramId, ulong programId,
        StorageId storageId)
    {
        UnsafeHelpers.SkipParamInit(out outOriginalProgramId);

        Result res = _config.LocationResolverSet.ResolveApplicationHtmlDocumentPath(out outIsDirectory, ref outPath,
            out outContentAttributes, programId, storageId);

        if (!res.IsSuccess())
        {
            if (ResultLr.HtmlDocumentNotFound.Includes(res))
            {
                ProgramId targetProgramId = _config.ProgramRegistryService.GetApplicationHtmlDocumentProgramIdByPatchProgramProgramId(new ProgramId(programId));
                if (targetProgramId == ProgramId.InvalidId)
                    return res.Rethrow();

                res = _config.LocationResolverSet.ResolveApplicationHtmlDocumentPath(out outIsDirectory, ref outPath,
                    out outContentAttributes, targetProgramId.Value, storageId);
                if (res.IsFailure()) return res.Miss();

                outOriginalProgramId = targetProgramId.Value;
            }

            return res.Miss();
        }

        outOriginalProgramId = programId;
        return Result.Success;
    }

    public Result ResolveDataPath(ref Path outPath, out ContentAttributes outContentAttributes, DataId dataId,
        StorageId storageId)
    {
        return _config.LocationResolverSet.ResolveDataPath(ref outPath, out outContentAttributes, dataId, storageId).Ret();
    }

    public Result ResolveAddOnContentPath(ref Path outPath, out ContentAttributes outContentAttributes,
        ref Path outPatchPath, out ContentAttributes outPatchContentAttributes, DataId dataId)
    {
        return _config.LocationResolverSet.ResolveAddOnContentPath(ref outPath, out outContentAttributes,
            ref outPatchPath, out outPatchContentAttributes, dataId).Ret();
    }

    public Result ResolveRegisteredProgramPath(ref Path outPath, out ContentAttributes outContentAttributes,
        ulong programId)
    {
        return _config.LocationResolverSet.ResolveRegisteredProgramPath(ref outPath, out outContentAttributes, programId).Ret();
    }

    public Result ResolveRegisteredHtmlDocumentPath(ref Path outPath, out ContentAttributes outContentAttributes,
        ulong programId)
    {
        return _config.LocationResolverSet.ResolveRegisteredHtmlDocumentPath(ref outPath, out outContentAttributes, programId).Ret();
    }

    internal StorageLayoutType GetStorageFlag(ulong programId)
    {
        Assert.SdkRequiresNotEqual(_config.SpeedEmulationRange.ProgramIdWithoutPlatformIdMax, 0ul);

        ulong programIdWithoutPlatformId = Impl.Utility.ClearPlatformIdInProgramId(programId);

        if (programIdWithoutPlatformId >= _config.SpeedEmulationRange.ProgramIdWithoutPlatformIdMin &&
            programIdWithoutPlatformId <= _config.SpeedEmulationRange.ProgramIdWithoutPlatformIdMax)
            return StorageLayoutType.Bis;
        else
            return StorageLayoutType.All;
    }

    public Result HandleResolubleAccessFailure(out bool wasDeferred, Result nonDeferredResult,
        ulong processId)
    {
        return _config.AccessFailureManagementService
            .HandleResolubleAccessFailure(out wasDeferred, nonDeferredResult, processId).Ret();
    }

    public void IncrementRomFsDeepRetryStartCount()
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _romfsCountMutex);
        _romFsDeepRetryStartCount++;
    }

    public void IncrementRomFsRemountForDataCorruptionCount()
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _romfsCountMutex);
        _romFsRemountForDataCorruptionCount++;
    }

    public void IncrementRomFsUnrecoverableDataCorruptionByRemountCount()
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _romfsCountMutex);
        _romfsUnrecoverableDataCorruptionByRemountCount++;
    }

    public void IncrementRomFsRecoveredByInvalidateCacheCount()
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _romfsCountMutex);
        _romFsRecoveredByInvalidateCacheCount++;
    }

    public void IncrementRomFsUnrecoverableByGameCardAccessFailedCount()
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _romfsCountMutex);
        _romFsUnrecoverableByGameCardAccessFailedCount++;
    }

    public void GetAndClearRomFsErrorInfo(out uint outDeepRetryStartCount, out uint outRemountForDataCorruptionCount,
        out uint outUnrecoverableDataCorruptionByRemountCount, out uint outRecoveredByInvalidateCacheCount,
        out uint outUnrecoverableByGameCardAccessFailedCount)
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _romfsCountMutex);

        outDeepRetryStartCount = _romFsDeepRetryStartCount;
        outRemountForDataCorruptionCount = _romFsRemountForDataCorruptionCount;
        outUnrecoverableDataCorruptionByRemountCount = _romfsUnrecoverableDataCorruptionByRemountCount;
        outRecoveredByInvalidateCacheCount = _romFsRecoveredByInvalidateCacheCount;
        outUnrecoverableByGameCardAccessFailedCount = _romFsUnrecoverableByGameCardAccessFailedCount;

        _romFsDeepRetryStartCount = 0;
        _romFsRemountForDataCorruptionCount = 0;
        _romfsUnrecoverableDataCorruptionByRemountCount = 0;
        _romFsRecoveredByInvalidateCacheCount = 0;
        _romFsUnrecoverableByGameCardAccessFailedCount = 0;
    }

    public Result CreateNotifier(ref UniqueRef<SystemDataUpdateEventNotifier> outNotifier)
    {
        return _systemDataUpdateEventManager.CreateNotifier(ref outNotifier).Ret();
    }

    public Result NotifySystemDataUpdateEvent()
    {
        return _systemDataUpdateEventManager.NotifySystemDataUpdateEvent().Ret();
    }

    public Result OpenHostFileSystem(ref SharedRef<IFileSystem> outFileSystem, ref readonly Path rootPath, bool openCaseSensitive)
    {
        return _config.TargetManagerFsCreator.Create(ref outFileSystem, in rootPath, openCaseSensitive, false,
            Result.Success).Ret();
    }
}

public readonly struct InternalProgramIdRangeForSpeedEmulation
{
    public readonly ulong ProgramIdWithoutPlatformIdMin;
    public readonly ulong ProgramIdWithoutPlatformIdMax;

    public InternalProgramIdRangeForSpeedEmulation(ulong min, ulong max)
    {
        ProgramIdWithoutPlatformIdMin = min;
        ProgramIdWithoutPlatformIdMax = max;
    }
}