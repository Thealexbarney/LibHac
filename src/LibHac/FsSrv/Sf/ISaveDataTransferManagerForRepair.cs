using System;
using LibHac.Fs;
using LibHac.Sf;

namespace LibHac.FsSrv.Sf
{
    public interface ISaveDataTransferManagerForRepair : IDisposable
    {
        public Result OpenSaveDataExporter(out ReferenceCountedDisposable<ISaveDataDivisionExporter> exporter, SaveDataSpaceId spaceId, ulong saveDataId);
        public Result OpenSaveDataImporter(out ReferenceCountedDisposable<ISaveDataDivisionImporter> importer, InBuffer initialData, SaveDataSpaceId spaceId);
    }
}