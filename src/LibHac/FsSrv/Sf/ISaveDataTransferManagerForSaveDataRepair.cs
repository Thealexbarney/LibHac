using System;
using LibHac.Fs;
using LibHac.Sf;

namespace LibHac.FsSrv.Sf
{
    public interface ISaveDataTransferManagerForSaveDataRepair : IDisposable
    {
        public Result GetChallenge(OutBuffer challenge);
        public Result SetKeyPackage(InBuffer keyPackage);
        public Result OpenSaveDataExporterAndGetEncryptedKey(out ReferenceCountedDisposable<ISaveDataDivisionExporter> exporter, out RsaEncryptedKey key, SaveDataSpaceId spaceId, ulong saveDataId);
        public Result PrepareOpenSaveDataImporter(out RsaEncryptedKey key);
        public Result OpenSaveDataImporterForSaveDataAfterRepair(out ReferenceCountedDisposable<ISaveDataDivisionImporter> importer, InBuffer initialDataBeforeRepair, InBuffer initialDataAfterRepair, UserId userId, SaveDataSpaceId spaceId);
        public Result OpenSaveDataImporterForSaveDataBeforeRepair(out ReferenceCountedDisposable<ISaveDataDivisionImporter> importer, InBuffer initialData, UserId userId, SaveDataSpaceId spaceId);
        public Result OpenSaveDataExporterWithKey(out ReferenceCountedDisposable<ISaveDataDivisionExporter> exporter, in AesKey key, SaveDataSpaceId spaceId, ulong saveDataId);
        public Result OpenSaveDataImporterWithKey(out ReferenceCountedDisposable<ISaveDataDivisionImporter> importer, in AesKey key, InBuffer initialData, UserId userId, ulong saveDataSpaceId);
    }
}
