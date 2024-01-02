using System;
using LibHac.Common;
using LibHac.Fs.Impl;
using LibHac.Sf;

namespace LibHac.FsSrv.Sf;

public interface ISaveDataDivisionImporter : IDisposable
{
    public Result ReadSaveDataExtraData(OutBuffer outExtraData);
    public Result OpenSaveDataDiffChunkIterator(ref SharedRef<ISaveDataChunkIterator> outIterator);
    public Result InitializeImport(out long outRemaining, long sizeToProcess);
    public Result FinalizeImport();
    public Result CancelImport();
    public Result GetImportContext(OutBuffer outImportContext);
    public Result SuspendImport();
    public Result FinalizeImportWithoutSwap();
    public Result OpenSaveDataChunkImporter(ref SharedRef<ISaveDataChunkImporter> outImporter, uint chunkId);
    public Result GetImportInitialDataAad(out InitialDataAad outInitialDataAad);
    public Result GetReportInfo(out ImportReportInfo outReportInfo);
}