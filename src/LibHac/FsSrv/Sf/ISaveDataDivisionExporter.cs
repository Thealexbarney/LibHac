﻿using System;
using LibHac.Common;
using LibHac.Fs.Impl;
using LibHac.Sf;

namespace LibHac.FsSrv.Sf;

public interface ISaveDataDivisionExporter : IDisposable
{
    public Result SetDivisionCount(int divisionCount);
    public Result ReadSaveDataExtraData(OutBuffer extraData);
    public Result OpenSaveDataDiffChunkIterator(ref SharedRef<ISaveDataChunkIterator> outIterator);
    public Result OpenSaveDataChunkExporter(ref SharedRef<ISaveDataChunkExporter> outExporter, uint chunkId);
    public Result CancelExport();
    public Result SuspendExport(OutBuffer exportContext);
    public Result GetKeySeed(out KeySeed keySeed);
    public Result GetInitialDataMac(out InitialDataMac initialDataMac);
    public Result FinalizeExport();
    public Result GetInitialDataMacKeyGeneration(out int keyGeneration);
    public Result GetImportInitialDataAad(out InitialDataAad initialDataAad);
    public Result SetExportInitialDataAad(in InitialDataAad initialDataAad);
    public Result GetReportInfo(out ImportReportInfo reportInfo);
}
