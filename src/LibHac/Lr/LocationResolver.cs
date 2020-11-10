using System;
using LibHac.Ncm;
using LibHac.Sf;

namespace LibHac.Lr
{
    public class LocationResolver : IDisposable
    {
        private ReferenceCountedDisposable<ILocationResolver> _interface;

        public LocationResolver(ReferenceCountedDisposable<ILocationResolver> baseInterface)
        {
            _interface = baseInterface.AddReference();
        }

        public Result ResolveProgramPath(out Path path, ProgramId id) =>
            _interface.Target.ResolveProgramPath(out path, id);

        public Result RedirectProgramPath(in Path path, ProgramId id) =>
            _interface.Target.RedirectProgramPath(in path, id);

        public Result ResolveApplicationControlPath(out Path path, ProgramId id) =>
            _interface.Target.ResolveApplicationControlPath(out path, id);

        public Result ResolveApplicationHtmlDocumentPath(out Path path, ProgramId id) =>
            _interface.Target.ResolveApplicationHtmlDocumentPath(out path, id);

        public Result ResolveDataPath(out Path path, DataId id) =>
            _interface.Target.ResolveDataPath(out path, id);

        public Result RedirectApplicationControlPath(in Path path, ProgramId id, ProgramId ownerId) =>
            _interface.Target.RedirectApplicationControlPath(in path, id, ownerId);

        public Result RedirectApplicationHtmlDocumentPath(in Path path, ProgramId id, ProgramId ownerId) =>
            _interface.Target.RedirectApplicationHtmlDocumentPath(in path, id, ownerId);

        public Result ResolveApplicationLegalInformationPath(out Path path, ProgramId id) =>
            _interface.Target.ResolveApplicationLegalInformationPath(out path, id);

        public Result RedirectApplicationLegalInformationPath(in Path path, ProgramId id, ProgramId ownerId) =>
            _interface.Target.RedirectApplicationLegalInformationPath(in path, id, ownerId);

        public Result Refresh() =>
            _interface.Target.Refresh();

        public Result RedirectApplicationProgramPath(in Path path, ProgramId id, ProgramId ownerId) =>
            _interface.Target.RedirectApplicationProgramPath(in path, id, ownerId);

        public Result ClearApplicationRedirection(InArray<ProgramId> excludingIds) =>
            _interface.Target.ClearApplicationRedirection(excludingIds);

        public Result EraseProgramRedirection(ProgramId id) =>
            _interface.Target.EraseProgramRedirection(id);

        public Result EraseApplicationControlRedirection(ProgramId id) =>
            _interface.Target.EraseApplicationControlRedirection(id);

        public Result EraseApplicationHtmlDocumentRedirection(ProgramId id) =>
            _interface.Target.EraseApplicationHtmlDocumentRedirection(id);

        public Result EraseApplicationLegalInformationRedirection(ProgramId id) =>
            _interface.Target.EraseApplicationLegalInformationRedirection(id);

        public Result ResolveProgramPathForDebug(out Path path, ProgramId id) =>
            _interface.Target.ResolveProgramPathForDebug(out path, id);

        public Result RedirectProgramPathForDebug(in Path path, ProgramId id) =>
            _interface.Target.RedirectProgramPathForDebug(in path, id);

        public Result RedirectApplicationProgramPathForDebug(in Path path, ProgramId id, ProgramId ownerId) =>
            _interface.Target.RedirectApplicationProgramPathForDebug(in path, id, ownerId);

        public Result EraseProgramRedirectionForDebug(ProgramId id) =>
            _interface.Target.EraseProgramRedirectionForDebug(id);

        public void Dispose()
        {
            _interface?.Dispose();
        }
    }
}
