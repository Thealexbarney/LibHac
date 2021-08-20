using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Sf;

namespace LibHac.FsSrv.Sf
{
    public interface ISaveDataTransferManagerWithDivision : IDisposable
    {
        public Result GetChallenge(OutBuffer challenge);
        public Result SetKeySeedPackage(InBuffer keySeedPackage);
        public Result OpenSaveDataExporter(ref SharedRef<ISaveDataDivisionExporter> outExporter, SaveDataSpaceId spaceId, ulong saveDataId);
        public Result OpenSaveDataExporterForDiffExport(ref SharedRef<ISaveDataDivisionExporter> outExporter, InBuffer initialData, SaveDataSpaceId spaceId, ulong saveDataId);
        public Result OpenSaveDataExporterByContext(ref SharedRef<ISaveDataDivisionExporter> outExporter, InBuffer exportContext);
        public Result OpenSaveDataImporterDeprecated(ref SharedRef<ISaveDataDivisionImporter> outImporter, InBuffer initialData, in UserId userId, SaveDataSpaceId spaceId);
        public Result OpenSaveDataImporterForDiffImport(ref SharedRef<ISaveDataDivisionImporter> outImporter, InBuffer initialData, SaveDataSpaceId spaceId, ulong saveDataId);
        public Result OpenSaveDataImporterForDuplicateDiffImport(ref SharedRef<ISaveDataDivisionImporter> outImporter, InBuffer initialData, SaveDataSpaceId spaceId, ulong saveDataId);
        public Result OpenSaveDataImporter(ref SharedRef<ISaveDataDivisionImporter> outImporter, InBuffer initialData, in UserId userId, SaveDataSpaceId spaceId, bool useSwap);
        public Result OpenSaveDataImporterByContext(ref SharedRef<ISaveDataDivisionImporter> outImporter, InBuffer importContext);
        public Result CancelSuspendingImport(Ncm.ApplicationId applicationId, in UserId userId);
        public Result CancelSuspendingImportByAttribute(in SaveDataAttribute attribute);
        public Result SwapSecondary(in SaveDataAttribute attribute, bool doSwap, long primaryCommitId);
    }
}