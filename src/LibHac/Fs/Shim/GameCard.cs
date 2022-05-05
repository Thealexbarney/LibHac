using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Gc;
using LibHac.Os;
using LibHac.Sf;
using LibHac.Util;

using static LibHac.Fs.Impl.AccessLogStrings;
using static LibHac.Gc.Values;

using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions used for mounting and interacting with the game card.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
[SkipLocalsInit]
public static class GameCard
{
    private static ReadOnlySpan<byte> GetGameCardMountNameSuffix(GameCardPartition partition)
    {
        switch (partition)
        {
            case GameCardPartition.Update: return CommonMountNames.GameCardFileSystemMountNameUpdateSuffix;
            case GameCardPartition.Normal: return CommonMountNames.GameCardFileSystemMountNameNormalSuffix;
            case GameCardPartition.Secure: return CommonMountNames.GameCardFileSystemMountNameSecureSuffix;
            default:
                Abort.UnexpectedDefault();
                return default;
        }
    }

    private class GameCardCommonMountNameGenerator : ICommonMountNameGenerator
    {
        private readonly GameCardHandle _handle;
        private readonly GameCardPartition _partitionId;

        public GameCardCommonMountNameGenerator(GameCardHandle handle, GameCardPartition partitionId)
        {
            _handle = handle;
            _partitionId = partitionId;
        }

        public void Dispose() { }

        public Result GenerateCommonMountName(Span<byte> nameBuffer)
        {
            int handleDigitCount = Unsafe.SizeOf<GameCardHandle>() * 2;

            // Determine how much space we need.
            int requiredNameBufferSize =
                StringUtils.GetLength(CommonMountNames.GameCardFileSystemMountName, PathTool.MountNameLengthMax) +
                StringUtils.GetLength(GetGameCardMountNameSuffix(_partitionId), PathTool.MountNameLengthMax) +
                handleDigitCount + 2;

            Assert.SdkRequiresGreaterEqual(nameBuffer.Length, requiredNameBufferSize);

            // Generate the name.
            var sb = new U8StringBuilder(nameBuffer);
            sb.Append(CommonMountNames.GameCardFileSystemMountName)
                .Append(GetGameCardMountNameSuffix(_partitionId))
                .AppendFormat(_handle, 'x', (byte)handleDigitCount)
                .Append(StringTraits.DriveSeparator);

            Assert.SdkEqual(sb.Length, requiredNameBufferSize - 1);

            return Result.Success;
        }
    }

    public static Result GetGameCardHandle(this FileSystemClient fs, out GameCardHandle outHandle)
    {
        UnsafeHelpers.SkipParamInit(out outHandle);

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        rc = deviceOperator.Get.GetGameCardHandle(out GameCardHandle handle);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        outHandle = handle;
        return Result.Success;
    }

    public static Result MountGameCardPartition(this FileSystemClient fs, U8Span mountName, GameCardHandle handle,
        GameCardPartition partitionId)
    {
        Result rc;
        Span<byte> logBuffer = stackalloc byte[0x60];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            rc = Mount(fs, mountName, handle, partitionId);
            Tick end = fs.Hos.Os.GetSystemTick();

            var idString = new IdString();
            var sb = new U8StringBuilder(logBuffer, true);

            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogGameCardHandle).AppendFormat(handle, 'X')
                .Append(LogGameCardPartition).Append(idString.ToString(partitionId));

            fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            rc = Mount(fs, mountName, handle, partitionId);
        }

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;

        static Result Mount(FileSystemClient fs, U8Span mountName, GameCardHandle handle, GameCardPartition partitionId)
        {
            Result rc = fs.Impl.CheckMountNameAcceptingReservedMountName(mountName);
            if (rc.IsFailure()) return rc.Miss();

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
            using var fileSystem = new SharedRef<IFileSystemSf>();

            rc = fileSystemProxy.Get.OpenGameCardFileSystem(ref fileSystem.Ref(), handle, partitionId);
            if (rc.IsFailure()) return rc.Miss();

            using var fileSystemAdapter =
                new UniqueRef<IFileSystem>(new FileSystemServiceObjectAdapter(ref fileSystem.Ref()));

            if (!fileSystemAdapter.HasValue)
                return ResultFs.AllocationMemoryFailedInGameCardC.Log();

            using var mountNameGenerator =
                new UniqueRef<ICommonMountNameGenerator>(new GameCardCommonMountNameGenerator(handle, partitionId));

            if (!mountNameGenerator.HasValue)
                return ResultFs.AllocationMemoryFailedInGameCardD.Log();

            return fs.Register(mountName, ref fileSystemAdapter.Ref(), ref mountNameGenerator.Ref()).Ret();
        }
    }

    public static bool IsGameCardInserted(this FileSystemClient fs)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.LogResultErrorMessage(rc);
        Abort.DoAbortUnless(rc.IsSuccess());

        rc = deviceOperator.Get.IsGameCardInserted(out bool isInserted);
        fs.Impl.LogResultErrorMessage(rc);
        Abort.DoAbortUnless(rc.IsSuccess());

        return isInserted;
    }

    public static Result OpenGameCardPartition(this FileSystemClient fs, ref UniqueRef<IStorage> outStorage,
        GameCardHandle handle, GameCardPartitionRaw partitionType)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var storage = new SharedRef<IStorageSf>();

        Result rc = fileSystemProxy.Get.OpenGameCardStorage(ref storage.Ref(), handle, partitionType);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        using var storageAdapter = new UniqueRef<IStorage>(new StorageServiceObjectAdapter(ref storage.Ref()));

        if (!storageAdapter.HasValue)
            return ResultFs.AllocationMemoryFailedInGameCardB.Log();

        outStorage.Set(ref storageAdapter.Ref());
        return Result.Success;
    }

    public static Result EraseGameCard(this FileSystemClient fs, GameCardSize cardSize, ulong romAreaStartPageAddress)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        rc = deviceOperator.Get.EraseGameCard((uint)cardSize, romAreaStartPageAddress);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result GetGameCardUpdatePartitionInfo(this FileSystemClient fs,
        out GameCardUpdatePartitionInfo outPartitionInfo, GameCardHandle handle)
    {
        outPartitionInfo = default;

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        rc = deviceOperator.Get.GetGameCardUpdatePartitionInfo(out uint cupVersion, out ulong cupId, handle);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        outPartitionInfo.CupVersion = cupVersion;
        outPartitionInfo.CupId = cupId;

        return Result.Success;
    }

    public static void FinalizeGameCardDriver(this FileSystemClient fs)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.LogResultErrorMessage(rc);
        Abort.DoAbortUnless(rc.IsSuccess());

        rc = deviceOperator.Get.FinalizeGameCardDriver();
        fs.Impl.LogResultErrorMessage(rc);
        Abort.DoAbortUnless(rc.IsSuccess());
    }

    public static Result GetGameCardAttribute(this FileSystemClient fs, out GameCardAttribute outAttribute,
        GameCardHandle handle)
    {
        UnsafeHelpers.SkipParamInit(out outAttribute);

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        rc = deviceOperator.Get.GetGameCardAttribute(out byte gameCardAttribute, handle);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        outAttribute = (GameCardAttribute)gameCardAttribute;

        return Result.Success;
    }

    public static Result GetGameCardCompatibilityType(this FileSystemClient fs,
        out GameCardCompatibilityType outCompatibilityType, GameCardHandle handle)
    {
        UnsafeHelpers.SkipParamInit(out outCompatibilityType);

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        rc = deviceOperator.Get.GetGameCardCompatibilityType(out byte gameCardCompatibilityType, handle);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        outCompatibilityType = (GameCardCompatibilityType)gameCardCompatibilityType;

        return Result.Success;
    }

    public static Result GetGameCardDeviceCertificate(this FileSystemClient fs, Span<byte> outBuffer,
        GameCardHandle handle)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        rc = deviceOperator.Get.GetGameCardDeviceCertificate(new OutBuffer(outBuffer), outBuffer.Length, handle);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result ChallengeCardExistence(this FileSystemClient fs, Span<byte> responseBuffer,
        ReadOnlySpan<byte> challengeSeedBuffer, ReadOnlySpan<byte> challengeValueBuffer, GameCardHandle handle)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        rc = deviceOperator.Get.ChallengeCardExistence(new OutBuffer(responseBuffer), new InBuffer(challengeSeedBuffer),
            new InBuffer(challengeValueBuffer), handle);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result GetGameCardAsicInfo(this FileSystemClient fs, out RmaInformation outRmaInfo,
        ReadOnlySpan<byte> asicFirmwareBuffer)
    {
        UnsafeHelpers.SkipParamInit(out outRmaInfo);

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        Unsafe.SkipInit(out RmaInformation rmaInformation);

        rc = deviceOperator.Get.GetGameCardAsicInfo(OutBuffer.FromStruct(ref rmaInformation),
            Unsafe.SizeOf<RmaInformation>(), new InBuffer(asicFirmwareBuffer), asicFirmwareBuffer.Length);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        outRmaInfo = rmaInformation;

        return Result.Success;
    }

    public static Result GetGameCardIdSet(this FileSystemClient fs, out GameCardIdSet outGcIdSet)
    {
        UnsafeHelpers.SkipParamInit(out outGcIdSet);

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        Unsafe.SkipInit(out GameCardIdSet gcIdSet);

        rc = deviceOperator.Get.GetGameCardIdSet(OutBuffer.FromStruct(ref gcIdSet), Unsafe.SizeOf<GameCardIdSet>());
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        outGcIdSet = gcIdSet;

        return Result.Success;
    }

    public static Result GetGameCardCid(this FileSystemClient fs, Span<byte> outCidBuffer)
    {
        Result rc;

        if (outCidBuffer.Length < Unsafe.SizeOf<GameCardIdSet>())
        {
            rc = ResultFs.InvalidSize.Value;
            fs.Impl.AbortIfNeeded(rc);
            return rc.Log();
        }

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        Unsafe.SkipInit(out GameCardIdSet gcIdSet);

        rc = deviceOperator.Get.GetGameCardIdSet(OutBuffer.FromStruct(ref gcIdSet), Unsafe.SizeOf<GameCardIdSet>());
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        SpanHelpers.AsByteSpan(ref gcIdSet).CopyTo(outCidBuffer);

        return Result.Success;
    }

    public static Result WriteToGameCard(this FileSystemClient fs, long offset, Span<byte> buffer)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        rc = deviceOperator.Get.WriteToGameCardDirectly(offset, new OutBuffer(buffer), buffer.Length);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result SetVerifyWriteEnableFlag(this FileSystemClient fs, bool isEnabled)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        rc = deviceOperator.Get.SetVerifyWriteEnableFlag(isEnabled);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result GetGameCardImageHash(this FileSystemClient fs, Span<byte> outBuffer, GameCardHandle handle)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        rc = deviceOperator.Get.GetGameCardImageHash(new OutBuffer(outBuffer), outBuffer.Length, handle);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result GetGameCardDeviceIdForProdCard(this FileSystemClient fs, Span<byte> outIdBuffer,
        ReadOnlySpan<byte> devHeaderBuffer)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        rc = deviceOperator.Get.GetGameCardDeviceIdForProdCard(new OutBuffer(outIdBuffer), outIdBuffer.Length,
            new InBuffer(devHeaderBuffer), devHeaderBuffer.Length);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result EraseAndWriteParamDirectly(this FileSystemClient fs, ReadOnlySpan<byte> devParamBuffer)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        rc = deviceOperator.Get.EraseAndWriteParamDirectly(new InBuffer(devParamBuffer), devParamBuffer.Length);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result ReadParamDirectly(this FileSystemClient fs, Span<byte> outBuffer)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        rc = deviceOperator.Get.ReadParamDirectly(new OutBuffer(outBuffer), outBuffer.Length);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result ForceEraseGameCard(this FileSystemClient fs)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        rc = deviceOperator.Get.ForceEraseGameCard();
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result GetGameCardErrorInfo(this FileSystemClient fs, out GameCardErrorInfo outErrorInfo)
    {
        UnsafeHelpers.SkipParamInit(out outErrorInfo);

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        rc = deviceOperator.Get.GetGameCardErrorInfo(out GameCardErrorInfo gameCardErrorInfo);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        outErrorInfo = gameCardErrorInfo;

        return Result.Success;
    }

    public static Result GetGameCardErrorReportInfo(this FileSystemClient fs, out GameCardErrorReportInfo outErrorInfo)
    {
        UnsafeHelpers.SkipParamInit(out outErrorInfo);

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        rc = deviceOperator.Get.GetGameCardErrorReportInfo(out GameCardErrorReportInfo gameCardErrorReportInfo);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        outErrorInfo = gameCardErrorReportInfo;

        return Result.Success;
    }

    public static Result CheckGameCardPartitionAvailability(this FileSystemClient fs, GameCardHandle handle,
        GameCardPartition partitionId)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var fileSystem = new SharedRef<IFileSystemSf>();

        Result rc = fileSystemProxy.Get.OpenGameCardFileSystem(ref fileSystem.Ref(), handle, partitionId);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result GetGameCardDeviceId(this FileSystemClient fs, Span<byte> outBuffer)
    {
        Result rc;

        // Note: Nintendo checks for length 8 here rather than GcCardDeviceIdSize (0x10)
        if (outBuffer.Length < GcCardDeviceIdSize)
        {
            rc = ResultFs.InvalidSize.Value;
            fs.Impl.AbortIfNeeded(rc);
            return rc.Log();
        }

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        Span<byte> buffer = stackalloc byte[GcCardDeviceIdSize];

        rc = deviceOperator.Get.GetGameCardDeviceId(new OutBuffer(buffer), GcCardDeviceIdSize);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        buffer.CopyTo(outBuffer);

        return Result.Success;
    }

    private static Result SetGameCardSimulationEventImpl(FileSystemClient fs,
        SimulatingDeviceTargetOperation simulatedOperationType,
        SimulatingDeviceAccessFailureEventType simulatedFailureType, Result failureResult, bool autoClearEvent)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        if (rc.IsFailure()) return rc.Miss();

        rc = deviceOperator.Get.SetDeviceSimulationEvent((uint)SdmmcPort.GcAsic, (uint)simulatedOperationType,
            (uint)simulatedFailureType, failureResult.Value, autoClearEvent);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result SimulateGameCardDetectionEvent(this FileSystemClient fs, SimulatingDeviceDetectionMode mode,
        bool signalEvent)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result rc = fileSystemProxy.Get.SimulateDeviceDetectionEvent(SdmmcPort.GcAsic, mode, signalEvent);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result SetGameCardSimulationEvent(this FileSystemClient fs,
        SimulatingDeviceTargetOperation simulatedOperationType,
        SimulatingDeviceAccessFailureEventType simulatedFailureType)
    {
        Result rc = SetGameCardSimulationEventImpl(fs, simulatedOperationType, simulatedFailureType, Result.Success,
            autoClearEvent: false);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result SetGameCardSimulationEvent(this FileSystemClient fs,
        SimulatingDeviceTargetOperation simulatedOperationType,
        SimulatingDeviceAccessFailureEventType simulatedFailureType, bool autoClearEvent)
    {
        Result rc = SetGameCardSimulationEventImpl(fs, simulatedOperationType, simulatedFailureType, Result.Success,
            autoClearEvent);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result SetGameCardSimulationEvent(this FileSystemClient fs,
        SimulatingDeviceTargetOperation simulatedOperationType, Result failureResult, bool autoClearEvent)
    {
        Result rc = SetGameCardSimulationEventImpl(fs, simulatedOperationType,
            SimulatingDeviceAccessFailureEventType.AccessFailure, failureResult, autoClearEvent);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result ClearGameCardSimulationEvent(this FileSystemClient fs)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result rc = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        rc = deviceOperator.Get.ClearDeviceSimulationEvent((uint)SdmmcPort.GcAsic);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }
}