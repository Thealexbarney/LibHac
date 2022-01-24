using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Fs.Impl;
using LibHac.Time;

namespace LibHac.Fs;

public interface ISaveDataChunkIterator : IDisposable
{
    ushort GetId();
    void Next();
    bool IsEnd();
}

public interface ISaveDataChunkExporter : IDisposable
{
    Result Pull(out ulong outPulledSize, Span<byte> destination);
    long GetRestRawDataSize();
}

public interface ISaveDataChunkImporter : IDisposable
{
    Result Push(ReadOnlySpan<byte> source);
}

public interface ISaveDataDivisionExporter : IDisposable
{
    Result SetDivisionCount(int divisionCount);
    Result OpenSaveDataDiffChunkIterator(ref UniqueRef<ISaveDataChunkIterator> outIterator);
    Result OpenSaveDataChunkExporter(ref UniqueRef<ISaveDataChunkExporter> outExporter, ushort chunkId);
    Result GetKeySeed(out KeySeed outKeySeed);
    Result GetInitialDataMac(out InitialDataMac outInitialDataMac);
    Result GetInitialDataMacKeyGeneration(out int outKeyGeneration);
    Result FinalizeExport();
    Result CancelExport();
    Result SuspendExport(out ExportContext outContext);
    Result GetImportInitialDataAad(out InitialDataAad outInitialDataAad);
    Result SetExportInitialDataAad(in InitialDataAad initialDataAad);
    Result GetSaveDataCommitId(out long outCommitId);
    Result GetSaveDataTimeStamp(out PosixTime outTimeStamp);
    Result GetReportInfo(out ExportReportInfo outReportInfo);

    public struct ExportContext
    {
        public Array16384<byte> Value;
    }
}

public interface ISaveDataDivisionImporter : IDisposable
{
    Result OpenSaveDataDiffChunkIterator(ref UniqueRef<ISaveDataChunkIterator> outIterator);
    Result InitializeImport(out long remaining, long sizeToProcess);
    Result FinalizeImport();
    Result FinalizeImportWithoutSwap();
    Result CancelImport();
    Result GetImportContext(out ImportContext outContext);
    Result SuspendImport();
    Result OpenSaveDataChunkImporter(ref UniqueRef<ISaveDataChunkImporter> outImporter, ushort chunkId);
    Result GetImportInitialDataAad(out InitialDataAad outInitialDataAad);
    Result GetSaveDataCommitId(out long outCommitId);
    Result GetSaveDataTimeStamp(out PosixTime outTimeStamp);
    Result GetReportInfo(out ImportReportInfo outReportInfo);

    public struct ImportContext
    {
        public Array16384<byte> Value;
    }
}