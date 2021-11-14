using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Sf;

namespace LibHac.FsSrv.Sf;

public interface ISaveDataTransferManagerForSaveDataRepair : IDisposable
{
    public Result GetChallenge(OutBuffer challenge);
    public Result SetKeyPackage(InBuffer keyPackage);
    public Result OpenSaveDataExporterAndGetEncryptedKey(ref SharedRef<ISaveDataDivisionExporter> outExporter, out RsaEncryptedKey key, SaveDataSpaceId spaceId, ulong saveDataId);
    public Result PrepareOpenSaveDataImporter(out RsaEncryptedKey key);
    public Result OpenSaveDataImporterForSaveDataAfterRepair(ref SharedRef<ISaveDataDivisionImporter> outImporter, InBuffer initialDataBeforeRepair, InBuffer initialDataAfterRepair, UserId userId, SaveDataSpaceId spaceId);
    public Result OpenSaveDataImporterForSaveDataBeforeRepair(ref SharedRef<ISaveDataDivisionImporter> outImporter, InBuffer initialData, UserId userId, SaveDataSpaceId spaceId);
    public Result OpenSaveDataExporterWithKey(ref SharedRef<ISaveDataDivisionExporter> outExporter, in AesKey key, SaveDataSpaceId spaceId, ulong saveDataId);
    public Result OpenSaveDataImporterWithKey(ref SharedRef<ISaveDataDivisionImporter> outImporter, in AesKey key, InBuffer initialData, UserId userId, ulong saveDataSpaceId);
}
