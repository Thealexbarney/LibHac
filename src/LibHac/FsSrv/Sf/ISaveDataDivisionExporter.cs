using System;
using LibHac.Common;
using LibHac.Fs.Impl;
using LibHac.Sf;

namespace LibHac.FsSrv.Sf;

public interface ISaveDataDivisionExporter : IDisposable
{
    public Result SetDivisionCount(int divisionCount);
    public Result ReadSaveDataExtraData(OutBuffer outExtraData);
    public Result OpenSaveDataDiffChunkIterator(ref SharedRef<ISaveDataChunkIterator> outIterator);
    public Result OpenSaveDataChunkExporter(ref SharedRef<ISaveDataChunkExporter> outExporter, uint chunkId);
    public Result CancelExport();
    public Result SuspendExport(OutBuffer outExportContext);
    public Result GetKeySeed(out KeySeed outKeySeed);
    public Result GetInitialDataMac(out InitialDataMac outInitialDataMac);
    public Result FinalizeExport();
    public Result GetInitialDataMacKeyGeneration(out int outKeyGeneration);
    public Result GetImportInitialDataAad(out InitialDataAad outInitialDataAad);
    public Result SetExportInitialDataAad(in InitialDataAad initialDataAad);
    public Result GetReportInfo(out ExportReportInfo outReportInfo);
}