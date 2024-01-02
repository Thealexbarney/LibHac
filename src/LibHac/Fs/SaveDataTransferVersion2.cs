using System;
using LibHac.Account;
using LibHac.Common;

namespace LibHac.Fs;

public partial class SaveDataTransferManagerVersion2
{
    public Result OpenSaveDataImporter(ref UniqueRef<ISaveDataDivisionImporter> outImporter,
        in InitialDataVersion2 initialData, in Uid user, SaveDataSpaceId spaceId, bool useSwap)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataImporter(ref UniqueRef<ISaveDataDivisionImporter> outImporter,
        in InitialDataVersion2 initialData, ulong staticSaveDataId, in Uid user, ulong ownerId)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataFullImporter(ref UniqueRef<ISaveDataDivisionImporter> outImporter,
        in InitialDataVersion2 initialData, in Uid user, SaveDataSpaceId spaceId)
    {
        throw new NotImplementedException();
    }

    public static SaveDataTag MakeUserAccountSaveDataTag(Ncm.ApplicationId applicationId, in Uid user)
    {
        throw new NotImplementedException();
    }

    public Result CancelSuspendingImport(Ncm.ApplicationId applicationId, in Uid user)
    {
        throw new NotImplementedException();
    }
}