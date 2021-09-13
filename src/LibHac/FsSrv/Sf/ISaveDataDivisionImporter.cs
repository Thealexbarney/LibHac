﻿using System;
using LibHac.Common;
using LibHac.Fs.Impl;
using LibHac.Sf;

namespace LibHac.FsSrv.Sf
{
    public interface ISaveDataDivisionImporter : IDisposable
    {
        public Result ReadSaveDataExtraData(OutBuffer extraData);
        public Result OpenSaveDataDiffChunkIterator(ref SharedRef<ISaveDataChunkIterator> outIterator);
        public Result InitializeImport(out long remaining, long sizeToProcess);
        public Result FinalizeImport();
        public Result CancelImport();
        public Result GetImportContext(OutBuffer context);
        public Result SuspendImport();
        public Result FinalizeImportWithoutSwap();
        public Result OpenSaveDataChunkImporter(ref SharedRef<ISaveDataChunkImporter> outImporter, uint chunkId);
        public Result GetImportInitialDataAad(out InitialDataAad initialDataAad);
        public Result GetReportInfo(out ImportReportInfo reportInfo);
    }
}