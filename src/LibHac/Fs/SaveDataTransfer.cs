using System;
using LibHac.Account;
using LibHac.Common;

namespace LibHac.Fs;

public partial class SaveDataTransferManager
{
    public Result OpenSaveDataImporter(ref UniqueRef<SaveDataImporter> outImporter, out long outRequiredSize,
        ReadOnlySpan<byte> initialData, in Uid user, SaveDataSpaceId spaceId)
    {
        throw new NotImplementedException();
    }
}