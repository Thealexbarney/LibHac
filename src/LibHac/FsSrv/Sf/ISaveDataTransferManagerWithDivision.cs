using System;
using LibHac.Fs;
using LibHac.Sf;

namespace LibHac.FsSrv.Sf
{
    public interface ISaveDataTransferManagerWithDivision : IDisposable
    {
        public Result GetChallenge(OutBuffer challenge);
        public Result SetKeySeedPackage(InBuffer keySeedPackage);
        public Result OpenSaveDataExporter(out ReferenceCountedDisposable<ISaveDataDivisionExporter> exporter, SaveDataSpaceId spaceId, ulong saveDataId);
        public Result OpenSaveDataExporterForDiffExport(out ReferenceCountedDisposable<ISaveDataDivisionExporter> exporter, InBuffer initialData, SaveDataSpaceId spaceId, ulong saveDataId);
        public Result OpenSaveDataExporterByContext(out ReferenceCountedDisposable<ISaveDataDivisionExporter> exporter, InBuffer exportContext);
        public Result OpenSaveDataImporterDeprecated(out ReferenceCountedDisposable<ISaveDataDivisionImporter> importer, InBuffer initialData, in UserId userId, SaveDataSpaceId spaceId);
        public Result OpenSaveDataImporterForDiffImport(out ReferenceCountedDisposable<ISaveDataDivisionImporter> importer, InBuffer initialData, SaveDataSpaceId spaceId, ulong saveDataId);
        public Result OpenSaveDataImporterForDuplicateDiffImport(out ReferenceCountedDisposable<ISaveDataDivisionImporter> importer, InBuffer initialData, SaveDataSpaceId spaceId, ulong saveDataId);
        public Result OpenSaveDataImporter(out ReferenceCountedDisposable<ISaveDataDivisionImporter> importer, InBuffer initialData, in UserId userId, SaveDataSpaceId spaceId, bool useSwap);
        public Result OpenSaveDataImporterByContext(out ReferenceCountedDisposable<ISaveDataDivisionImporter> importer, InBuffer importContext);
        public Result CancelSuspendingImport(Ncm.ApplicationId applicationId, in UserId userId);
        public Result CancelSuspendingImportByAttribute(in SaveDataAttribute attribute);
        public Result SwapSecondary(in SaveDataAttribute attribute, bool doSwap, long primaryCommitId);
    }
}