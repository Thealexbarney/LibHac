using System;
using LibHac.Account;
using LibHac.Common;

namespace LibHac.Fs;

public partial class SaveDataTransferManagerForSaveDataRepair
{
    public Result OpenSaveDataImporter(ref UniqueRef<ISaveDataDivisionImporter> outImporter,
        in InitialDataVersion2 initialDataBeforeRepair, in InitialDataVersion2 initialDataAfterRepair, in Uid user,
        SaveDataSpaceId spaceId)
    {
        throw new NotImplementedException();
    }
    
    public Result OpenSaveDataImporter(ref UniqueRef<ISaveDataDivisionImporter> outImporter,
        in InitialDataVersion2 initialDataBeforeRepair, in Uid user, SaveDataSpaceId spaceId)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataImporterWithKey(ref UniqueRef<ISaveDataDivisionExporter> outExporter, in AesKey key,
        in InitialDataVersion2 initialData, in Uid user, SaveDataSpaceId spaceId)
    {
        throw new NotImplementedException();
    }
}