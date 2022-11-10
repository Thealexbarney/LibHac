using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Sf;

namespace LibHac.FsSrv.Sf;

public interface ISaveDataTransferManager : IDisposable
{
    public Result GetChallenge(OutBuffer challenge);
    public Result SetToken(InBuffer token);
    public Result OpenSaveDataExporter(ref SharedRef<ISaveDataExporter> outExporter, SaveDataSpaceId spaceId, ulong saveDataId);
    public Result OpenSaveDataImporter(ref SharedRef<ISaveDataImporter> outImporter, out long requiredSize, InBuffer initialData, in UserId userId, SaveDataSpaceId spaceId);
}