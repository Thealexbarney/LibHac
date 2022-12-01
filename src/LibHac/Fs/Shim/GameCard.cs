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
            case GameCardPartition.Update: return CommonMountNames.GameCardFileSystemMountNameSuffixUpdate;
            case GameCardPartition.Normal: return CommonMountNames.GameCardFileSystemMountNameSuffixNormal;
            case GameCardPartition.Secure: return CommonMountNames.GameCardFileSystemMountNameSuffixSecure;
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

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.GetGameCardHandle(out GameCardHandle handle);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        outHandle = handle;
        return Result.Success;
    }

    public static Result MountGameCardPartition(this FileSystemClient fs, U8Span mountName, GameCardHandle handle,
        GameCardPartition partitionId)
    {
        Result res;
        Span<byte> logBuffer = stackalloc byte[0x60];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = Mount(fs, mountName, handle, partitionId);
            Tick end = fs.Hos.Os.GetSystemTick();

            var idString = new IdString();
            var sb = new U8StringBuilder(logBuffer, true);

            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogGameCardHandle).AppendFormat(handle, 'X')
                .Append(LogGameCardPartition).Append(idString.ToString(partitionId));

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            res = Mount(fs, mountName, handle, partitionId);
        }

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;

        static Result Mount(FileSystemClient fs, U8Span mountName, GameCardHandle handle, GameCardPartition partitionId)
        {
            Result res = fs.Impl.CheckMountNameAcceptingReservedMountName(mountName);
            if (res.IsFailure()) return res.Miss();

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
            using var fileSystem = new SharedRef<IFileSystemSf>();

            res = fileSystemProxy.Get.OpenGameCardFileSystem(ref fileSystem.Ref(), handle, partitionId);
            if (res.IsFailure()) return res.Miss();

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

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.LogResultErrorMessage(res);
        Abort.DoAbortUnless(res.IsSuccess());

        res = deviceOperator.Get.IsGameCardInserted(out bool isInserted);
        fs.Impl.LogResultErrorMessage(res);
        Abort.DoAbortUnless(res.IsSuccess());

        return isInserted;
    }

    public static Result OpenGameCardPartition(this FileSystemClient fs, ref UniqueRef<IStorage> outStorage,
        GameCardHandle handle, GameCardPartitionRaw partitionType)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var storage = new SharedRef<IStorageSf>();

        Result res = fileSystemProxy.Get.OpenGameCardStorage(ref storage.Ref(), handle, partitionType);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

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

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.EraseGameCard((uint)cardSize, romAreaStartPageAddress);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result GetGameCardUpdatePartitionInfo(this FileSystemClient fs,
        out GameCardUpdatePartitionInfo outPartitionInfo, GameCardHandle handle)
    {
        outPartitionInfo = default;

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.GetGameCardUpdatePartitionInfo(out uint cupVersion, out ulong cupId, handle);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        outPartitionInfo.CupVersion = cupVersion;
        outPartitionInfo.CupId = cupId;

        return Result.Success;
    }

    public static void FinalizeGameCardDriver(this FileSystemClient fs)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.LogResultErrorMessage(res);
        Abort.DoAbortUnless(res.IsSuccess());

        res = deviceOperator.Get.FinalizeGameCardDriver();
        fs.Impl.LogResultErrorMessage(res);
        Abort.DoAbortUnless(res.IsSuccess());
    }

    public static Result GetGameCardAttribute(this FileSystemClient fs, out GameCardAttribute outAttribute,
        GameCardHandle handle)
    {
        UnsafeHelpers.SkipParamInit(out outAttribute);

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.GetGameCardAttribute(out byte gameCardAttribute, handle);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        outAttribute = (GameCardAttribute)gameCardAttribute;

        return Result.Success;
    }

    public static Result GetGameCardCompatibilityType(this FileSystemClient fs,
        out GameCardCompatibilityType outCompatibilityType, GameCardHandle handle)
    {
        UnsafeHelpers.SkipParamInit(out outCompatibilityType);

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.GetGameCardCompatibilityType(out byte gameCardCompatibilityType, handle);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        outCompatibilityType = (GameCardCompatibilityType)gameCardCompatibilityType;

        return Result.Success;
    }

    public static Result GetGameCardDeviceCertificate(this FileSystemClient fs, Span<byte> outBuffer,
        GameCardHandle handle)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.GetGameCardDeviceCertificate(new OutBuffer(outBuffer), outBuffer.Length, handle);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result ChallengeCardExistence(this FileSystemClient fs, Span<byte> responseBuffer,
        ReadOnlySpan<byte> challengeSeedBuffer, ReadOnlySpan<byte> challengeValueBuffer, GameCardHandle handle)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.ChallengeCardExistence(new OutBuffer(responseBuffer), new InBuffer(challengeSeedBuffer),
            new InBuffer(challengeValueBuffer), handle);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result GetGameCardAsicInfo(this FileSystemClient fs, out RmaInformation outRmaInfo,
        ReadOnlySpan<byte> asicFirmwareBuffer)
    {
        UnsafeHelpers.SkipParamInit(out outRmaInfo);

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        Unsafe.SkipInit(out RmaInformation rmaInformation);

        res = deviceOperator.Get.GetGameCardAsicInfo(OutBuffer.FromStruct(ref rmaInformation),
            Unsafe.SizeOf<RmaInformation>(), new InBuffer(asicFirmwareBuffer), asicFirmwareBuffer.Length);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        outRmaInfo = rmaInformation;

        return Result.Success;
    }

    public static Result GetGameCardIdSet(this FileSystemClient fs, out GameCardIdSet outGcIdSet)
    {
        UnsafeHelpers.SkipParamInit(out outGcIdSet);

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        Unsafe.SkipInit(out GameCardIdSet gcIdSet);

        res = deviceOperator.Get.GetGameCardIdSet(OutBuffer.FromStruct(ref gcIdSet), Unsafe.SizeOf<GameCardIdSet>());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        outGcIdSet = gcIdSet;

        return Result.Success;
    }

    public static Result GetGameCardCid(this FileSystemClient fs, Span<byte> outCidBuffer)
    {
        Result res;

        if (outCidBuffer.Length < Unsafe.SizeOf<GameCardIdSet>())
        {
            res = ResultFs.InvalidSize.Value;
            fs.Impl.AbortIfNeeded(res);
            return res.Log();
        }

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        Unsafe.SkipInit(out GameCardIdSet gcIdSet);

        res = deviceOperator.Get.GetGameCardIdSet(OutBuffer.FromStruct(ref gcIdSet), Unsafe.SizeOf<GameCardIdSet>());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        SpanHelpers.AsByteSpan(ref gcIdSet).CopyTo(outCidBuffer);

        return Result.Success;
    }

    public static Result WriteToGameCard(this FileSystemClient fs, long offset, Span<byte> buffer)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.WriteToGameCardDirectly(offset, new OutBuffer(buffer), buffer.Length);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result SetVerifyWriteEnableFlag(this FileSystemClient fs, bool isEnabled)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.SetVerifyWriteEnableFlag(isEnabled);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result GetGameCardImageHash(this FileSystemClient fs, Span<byte> outBuffer, GameCardHandle handle)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.GetGameCardImageHash(new OutBuffer(outBuffer), outBuffer.Length, handle);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result GetGameCardDeviceIdForProdCard(this FileSystemClient fs, Span<byte> outIdBuffer,
        ReadOnlySpan<byte> devHeaderBuffer)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.GetGameCardDeviceIdForProdCard(new OutBuffer(outIdBuffer), outIdBuffer.Length,
            new InBuffer(devHeaderBuffer), devHeaderBuffer.Length);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result EraseAndWriteParamDirectly(this FileSystemClient fs, ReadOnlySpan<byte> devParamBuffer)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.EraseAndWriteParamDirectly(new InBuffer(devParamBuffer), devParamBuffer.Length);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result ReadParamDirectly(this FileSystemClient fs, Span<byte> outBuffer)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.ReadParamDirectly(new OutBuffer(outBuffer), outBuffer.Length);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result ForceEraseGameCard(this FileSystemClient fs)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.ForceEraseGameCard();
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result GetGameCardErrorInfo(this FileSystemClient fs, out GameCardErrorInfo outErrorInfo)
    {
        UnsafeHelpers.SkipParamInit(out outErrorInfo);

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.GetGameCardErrorInfo(out GameCardErrorInfo gameCardErrorInfo);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        outErrorInfo = gameCardErrorInfo;

        return Result.Success;
    }

    public static Result GetGameCardErrorReportInfo(this FileSystemClient fs, out GameCardErrorReportInfo outErrorInfo)
    {
        UnsafeHelpers.SkipParamInit(out outErrorInfo);

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.GetGameCardErrorReportInfo(out GameCardErrorReportInfo gameCardErrorReportInfo);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        outErrorInfo = gameCardErrorReportInfo;

        return Result.Success;
    }

    public static Result CheckGameCardPartitionAvailability(this FileSystemClient fs, GameCardHandle handle,
        GameCardPartition partitionId)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var fileSystem = new SharedRef<IFileSystemSf>();

        Result res = fileSystemProxy.Get.OpenGameCardFileSystem(ref fileSystem.Ref(), handle, partitionId);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result GetGameCardDeviceId(this FileSystemClient fs, Span<byte> outBuffer)
    {
        Result res;

        // Note: Nintendo checks for length 8 here rather than GcCardDeviceIdSize (0x10)
        if (outBuffer.Length < GcCardDeviceIdSize)
        {
            res = ResultFs.InvalidSize.Value;
            fs.Impl.AbortIfNeeded(res);
            return res.Log();
        }

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        Span<byte> buffer = stackalloc byte[GcCardDeviceIdSize];

        res = deviceOperator.Get.GetGameCardDeviceId(new OutBuffer(buffer), GcCardDeviceIdSize);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        buffer.CopyTo(outBuffer);

        return Result.Success;
    }

    private static Result SetGameCardSimulationEventImpl(FileSystemClient fs,
        SimulatingDeviceTargetOperation simulatedOperationType,
        SimulatingDeviceAccessFailureEventType simulatedFailureType, Result failureResult, bool autoClearEvent)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.SetDeviceSimulationEvent((uint)SdmmcPort.GcAsic, (uint)simulatedOperationType,
            (uint)simulatedFailureType, failureResult.Value, autoClearEvent);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result SimulateGameCardDetectionEvent(this FileSystemClient fs, SimulatingDeviceDetectionMode mode,
        bool signalEvent)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result res = fileSystemProxy.Get.SimulateDeviceDetectionEvent(SdmmcPort.GcAsic, mode, signalEvent);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result SetGameCardSimulationEvent(this FileSystemClient fs,
        SimulatingDeviceTargetOperation simulatedOperationType,
        SimulatingDeviceAccessFailureEventType simulatedFailureType)
    {
        Result res = SetGameCardSimulationEventImpl(fs, simulatedOperationType, simulatedFailureType, Result.Success,
            autoClearEvent: false);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result SetGameCardSimulationEvent(this FileSystemClient fs,
        SimulatingDeviceTargetOperation simulatedOperationType,
        SimulatingDeviceAccessFailureEventType simulatedFailureType, bool autoClearEvent)
    {
        Result res = SetGameCardSimulationEventImpl(fs, simulatedOperationType, simulatedFailureType, Result.Success,
            autoClearEvent);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result SetGameCardSimulationEvent(this FileSystemClient fs,
        SimulatingDeviceTargetOperation simulatedOperationType, Result failureResult, bool autoClearEvent)
    {
        Result res = SetGameCardSimulationEventImpl(fs, simulatedOperationType,
            SimulatingDeviceAccessFailureEventType.AccessFailure, failureResult, autoClearEvent);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result ClearGameCardSimulationEvent(this FileSystemClient fs)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.ClearDeviceSimulationEvent((uint)SdmmcPort.GcAsic);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }
}