using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Sf;

namespace LibHac.FsSrv.Sf
{
    public interface ISaveDataTransferManagerForRepair : IDisposable
    {
        public Result OpenSaveDataExporter(ref SharedRef<ISaveDataDivisionExporter> outExporter, SaveDataSpaceId spaceId, ulong saveDataId);
        public Result OpenSaveDataImporter(ref SharedRef<ISaveDataDivisionImporter> outImporter, InBuffer initialData, SaveDataSpaceId spaceId);
    }
}