using System;
using LibHac.Fs;
using LibHac.Sf;

namespace LibHac.FsSrv.Sf
{
    public interface ISaveDataTransferManager : IDisposable
    {
        public Result GetChallenge(OutBuffer challenge);
        public Result SetToken(InBuffer token);
        public Result OpenSaveDataExporter(out ReferenceCountedDisposable<ISaveDataExporter> exporter, SaveDataSpaceId spaceId, ulong saveDataId);
        public Result OpenSaveDataImporter(out ReferenceCountedDisposable<ISaveDataImporter> importer, out long requiredSize, InBuffer initialData, in UserId userId, SaveDataSpaceId spaceId);
    }
}