using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Sf;
using LibHac.Time;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs.Impl;

/// <summary>
/// An adapter for interacting with an <see cref="FsSrv.Sf.ISaveDataChunkIterator"/>
/// IPC service object via the non-IPC <see cref="ISaveDataChunkIterator"/> interface.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0</remarks>
public class SaveDataChunkIterator : ISaveDataChunkIterator
{
    private SharedRef<FsSrv.Sf.ISaveDataChunkIterator> _baseInterface;

    // LibHac addition
    private FileSystemClient _fsClient;

    public SaveDataChunkIterator(FileSystemClient fs, ref SharedRef<FsSrv.Sf.ISaveDataChunkIterator> baseInterface)
    {
        _baseInterface = SharedRef<FsSrv.Sf.ISaveDataChunkIterator>.CreateMove(ref baseInterface);
        _fsClient = fs;
    }

    public void Dispose()
    {
        _baseInterface.Destroy();
    }

    public ushort GetId()
    {
        Result rc = _baseInterface.Get.GetId(out uint id);
        _fsClient.Impl.LogResultErrorMessage(rc);
        Abort.DoAbortUnless(rc.IsSuccess());

        return (ushort)id;
    }

    public void Next()
    {
        Result rc = _baseInterface.Get.Next();
        _fsClient.Impl.LogResultErrorMessage(rc);
        Abort.DoAbortUnless(rc.IsSuccess());
    }

    public bool IsEnd()
    {
        Result rc = _baseInterface.Get.IsEnd(out bool isEnd);
        _fsClient.Impl.LogResultErrorMessage(rc);
        Abort.DoAbortUnless(rc.IsSuccess());

        return isEnd;
    }
}

/// <summary>
/// An adapter for interacting with an <see cref="FsSrv.Sf.ISaveDataChunkExporter"/>
/// IPC service object via the non-IPC <see cref="ISaveDataChunkExporter"/> interface.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0</remarks>
public class SaveDataChunkExporter : ISaveDataChunkExporter
{
    private SharedRef<FsSrv.Sf.ISaveDataChunkExporter> _baseInterface;

    // LibHac addition
    private FileSystemClient _fsClient;

    public SaveDataChunkExporter(FileSystemClient fs, ref SharedRef<FsSrv.Sf.ISaveDataChunkExporter> baseInterface)
    {
        _baseInterface = SharedRef<FsSrv.Sf.ISaveDataChunkExporter>.CreateMove(ref baseInterface);
        _fsClient = fs;
    }

    public void Dispose()
    {
        _baseInterface.Destroy();
    }

    public Result Pull(out ulong outPulledSize, Span<byte> destination)
    {
        UnsafeHelpers.SkipParamInit(out outPulledSize);

        Result rc = _baseInterface.Get.Pull(out ulong pulledSize, new OutBuffer(destination),
            (ulong)destination.Length);

        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        outPulledSize = pulledSize;
        return Result.Success;
    }

    public long GetRestRawDataSize()
    {
        Result rc = _baseInterface.Get.GetRestRawDataSize(out long restSize);
        _fsClient.Impl.LogResultErrorMessage(rc);
        Abort.DoAbortUnless(rc.IsSuccess());

        return restSize;
    }
}

/// <summary>
/// An adapter for interacting with an <see cref="FsSrv.Sf.ISaveDataChunkImporter"/>
/// IPC service object via the non-IPC <see cref="ISaveDataChunkImporter"/> interface.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0</remarks>
public class SaveDataChunkImporter : ISaveDataChunkImporter
{
    private SharedRef<FsSrv.Sf.ISaveDataChunkImporter> _baseInterface;

    // LibHac addition
    private FileSystemClient _fsClient;

    public SaveDataChunkImporter(FileSystemClient fs, ref SharedRef<FsSrv.Sf.ISaveDataChunkImporter> baseInterface)
    {
        _baseInterface = SharedRef<FsSrv.Sf.ISaveDataChunkImporter>.CreateMove(ref baseInterface);
        _fsClient = fs;
    }

    public void Dispose()
    {
        _baseInterface.Destroy();
    }

    public Result Push(ReadOnlySpan<byte> source)
    {
        Result rc = _baseInterface.Get.Push(new InBuffer(source), (ulong)source.Length);
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }
}

/// <summary>
/// An adapter for interacting with an <see cref="FsSrv.Sf.ISaveDataDivisionExporter"/>
/// IPC service object via the non-IPC <see cref="ISaveDataDivisionExporter"/> interface.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0</remarks>
public class SaveDataExporterVersion2 : ISaveDataDivisionExporter
{
    private SharedRef<FsSrv.Sf.ISaveDataDivisionExporter> _baseInterface;

    // LibHac addition
    private FileSystemClient _fsClient;

    public SaveDataExporterVersion2(FileSystemClient fs,
        ref SharedRef<FsSrv.Sf.ISaveDataDivisionExporter> baseInterface)
    {
        _baseInterface = SharedRef<FsSrv.Sf.ISaveDataDivisionExporter>.CreateMove(ref baseInterface);
        _fsClient = fs;
    }

    public void Dispose()
    {
        _baseInterface.Destroy();
    }

    public Result SetDivisionCount(int divisionCount)
    {
        Result rc = _baseInterface.Get.SetDivisionCount(divisionCount);
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public Result OpenSaveDataDiffChunkIterator(ref UniqueRef<ISaveDataChunkIterator> outIterator)
    {
        using var iteratorObject = new SharedRef<FsSrv.Sf.ISaveDataChunkIterator>();

        Result rc = _baseInterface.Get.OpenSaveDataDiffChunkIterator(ref iteratorObject.Ref());
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        outIterator.Reset(new SaveDataChunkIterator(_fsClient, ref iteratorObject.Ref()));
        return Result.Success;
    }

    public Result OpenSaveDataChunkExporter(ref UniqueRef<ISaveDataChunkExporter> outExporter, ushort chunkId)
    {
        using var exporterObject = new SharedRef<FsSrv.Sf.ISaveDataChunkExporter>();

        Result rc = _baseInterface.Get.OpenSaveDataChunkExporter(ref exporterObject.Ref(), chunkId);
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        outExporter.Reset(new SaveDataChunkExporter(_fsClient, ref exporterObject.Ref()));
        return Result.Success;
    }

    public Result GetKeySeed(out KeySeed outKeySeed)
    {
        UnsafeHelpers.SkipParamInit(out outKeySeed);

        Result rc = _baseInterface.Get.GetKeySeed(out KeySeed keySeed);
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        outKeySeed = keySeed;
        return Result.Success;
    }

    public Result GetInitialDataMac(out InitialDataMac outInitialDataMac)
    {
        UnsafeHelpers.SkipParamInit(out outInitialDataMac);

        Result rc = _baseInterface.Get.GetInitialDataMac(out InitialDataMac initialDataMac);
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        outInitialDataMac = initialDataMac;
        return Result.Success;
    }

    public Result GetInitialDataMacKeyGeneration(out int outKeyGeneration)
    {
        UnsafeHelpers.SkipParamInit(out outKeyGeneration);

        Result rc = _baseInterface.Get.GetInitialDataMacKeyGeneration(out int initialDataMacKeyGeneration);
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        outKeyGeneration = initialDataMacKeyGeneration;
        return Result.Success;
    }

    public Result FinalizeExport()
    {
        Result rc = _baseInterface.Get.FinalizeExport();
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public Result CancelExport()
    {
        Result rc = _baseInterface.Get.CancelExport();
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public Result SuspendExport(out ISaveDataDivisionExporter.ExportContext outContext)
    {
        UnsafeHelpers.SkipParamInit(out outContext);

        Result rc = _baseInterface.Get.SuspendExport(OutBuffer.FromStruct(ref outContext));
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public Result GetImportInitialDataAad(out InitialDataAad outInitialDataAad)
    {
        UnsafeHelpers.SkipParamInit(out outInitialDataAad);

        Result rc = _baseInterface.Get.GetImportInitialDataAad(out InitialDataAad initialDataAad);
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        outInitialDataAad = initialDataAad;
        return Result.Success;
    }

    public Result SetExportInitialDataAad(in InitialDataAad initialDataAad)
    {
        Result rc = _baseInterface.Get.SetExportInitialDataAad(in initialDataAad);
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public Result GetSaveDataCommitId(out long outCommitId)
    {
        UnsafeHelpers.SkipParamInit(out outCommitId);
        Unsafe.SkipInit(out SaveDataExtraData extraData);

        Result rc = _baseInterface.Get.ReadSaveDataExtraData(OutBuffer.FromStruct(ref extraData));
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        outCommitId = extraData.CommitId;
        return Result.Success;
    }

    public Result GetSaveDataTimeStamp(out PosixTime outTimeStamp)
    {
        UnsafeHelpers.SkipParamInit(out outTimeStamp);
        Unsafe.SkipInit(out SaveDataExtraData extraData);

        Result rc = _baseInterface.Get.ReadSaveDataExtraData(OutBuffer.FromStruct(ref extraData));
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        outTimeStamp = new PosixTime(extraData.TimeStamp);
        return Result.Success;
    }

    public Result GetReportInfo(out ExportReportInfo outReportInfo)
    {
        Result rc = _baseInterface.Get.GetReportInfo(out outReportInfo);
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }
}

/// <summary>
/// An adapter for interacting with an <see cref="FsSrv.Sf.ISaveDataDivisionImporter"/>
/// IPC service object via the non-IPC <see cref="ISaveDataDivisionImporter"/> interface.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0</remarks>
public class SaveDataImporterVersion2 : ISaveDataDivisionImporter
{
    private SharedRef<FsSrv.Sf.ISaveDataDivisionImporter> _baseInterface;

    // LibHac addition
    private FileSystemClient _fsClient;

    public SaveDataImporterVersion2(FileSystemClient fs,
        ref SharedRef<FsSrv.Sf.ISaveDataDivisionImporter> baseInterface)
    {
        _baseInterface = SharedRef<FsSrv.Sf.ISaveDataDivisionImporter>.CreateMove(ref baseInterface);
        _fsClient = fs;
    }

    public void Dispose()
    {
        _baseInterface.Destroy();
    }

    public Result InitializeImport(out long remaining, long sizeToProcess)
    {
        Result rc = _baseInterface.Get.InitializeImport(out remaining, sizeToProcess);
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public Result FinalizeImport()
    {
        Result rc = _baseInterface.Get.FinalizeImport();
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public Result FinalizeImportWithoutSwap()
    {
        Result rc = _baseInterface.Get.FinalizeImportWithoutSwap();
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public Result CancelImport()
    {
        Result rc = _baseInterface.Get.CancelImport();
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public Result GetImportContext(out ISaveDataDivisionImporter.ImportContext outContext)
    {
        UnsafeHelpers.SkipParamInit(out outContext);

        Result rc = _baseInterface.Get.GetImportContext(OutBuffer.FromStruct(ref outContext));
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public Result SuspendImport()
    {
        Result rc = _baseInterface.Get.SuspendImport();
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public Result OpenSaveDataDiffChunkIterator(ref UniqueRef<ISaveDataChunkIterator> outIterator)
    {
        using var iteratorObject = new SharedRef<FsSrv.Sf.ISaveDataChunkIterator>();

        Result rc = _baseInterface.Get.OpenSaveDataDiffChunkIterator(ref iteratorObject.Ref());
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        outIterator.Reset(new SaveDataChunkIterator(_fsClient, ref iteratorObject.Ref()));
        return Result.Success;
    }

    public Result OpenSaveDataChunkImporter(ref UniqueRef<ISaveDataChunkImporter> outImporter, ushort chunkId)
    {
        using var importerObject = new SharedRef<FsSrv.Sf.ISaveDataChunkImporter>();

        Result rc = _baseInterface.Get.OpenSaveDataChunkImporter(ref importerObject.Ref(), chunkId);
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        outImporter.Reset(new SaveDataChunkImporter(_fsClient, ref importerObject.Ref()));
        return Result.Success;
    }

    public Result GetImportInitialDataAad(out InitialDataAad outInitialDataAad)
    {
        UnsafeHelpers.SkipParamInit(out outInitialDataAad);

        Result rc = _baseInterface.Get.GetImportInitialDataAad(out InitialDataAad initialDataAad);
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        outInitialDataAad = initialDataAad;
        return Result.Success;
    }

    public Result GetSaveDataCommitId(out long outCommitId)
    {
        UnsafeHelpers.SkipParamInit(out outCommitId);
        Unsafe.SkipInit(out SaveDataExtraData extraData);

        Result rc = _baseInterface.Get.ReadSaveDataExtraData(OutBuffer.FromStruct(ref extraData));
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        outCommitId = extraData.CommitId;
        return Result.Success;
    }

    public Result GetSaveDataTimeStamp(out PosixTime outTimeStamp)
    {
        UnsafeHelpers.SkipParamInit(out outTimeStamp);
        Unsafe.SkipInit(out SaveDataExtraData extraData);

        Result rc = _baseInterface.Get.ReadSaveDataExtraData(OutBuffer.FromStruct(ref extraData));
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        outTimeStamp = new PosixTime(extraData.TimeStamp);
        return Result.Success;
    }

    public Result GetReportInfo(out ImportReportInfo outReportInfo)
    {
        Result rc = _baseInterface.Get.GetReportInfo(out outReportInfo);
        _fsClient.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }
}