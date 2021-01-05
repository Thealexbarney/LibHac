using System;
using LibHac.Fs.Impl;
using LibHac.Sf;

namespace LibHac.FsSrv.Sf
{
    public interface ISaveDataDivisionImporter : IDisposable
    {
        public Result ReadSaveDataExtraData(OutBuffer extraData);
        public Result OpenSaveDataDiffChunkIterator(out ReferenceCountedDisposable<ISaveDataChunkIterator> iterator);
        public Result InitializeImport(out long remaining, long sizeToProcess);
        public Result FinalizeImport();
        public Result CancelImport();
        public Result GetImportContext(OutBuffer context);
        public Result SuspendImport();
        public Result FinalizeImportWithoutSwap();
        public Result OpenSaveDataChunkImporter(out ReferenceCountedDisposable<ISaveDataChunkImporter> importer, uint chunkId);
        public Result GetImportInitialDataAad(out InitialDataAad initialDataAad);
        public Result GetReportInfo(out ImportReportInfo reportInfo);
    }
}