using System;
using LibHac.Ncm;
using LibHac.Sf;

namespace LibHac.Lr
{
    public interface ILocationResolver : IDisposable
    {
        Result ResolveProgramPath(out Path path, ProgramId id);
        Result RedirectProgramPath(in Path path, ProgramId id);
        Result ResolveApplicationControlPath(out Path path, ProgramId id);
        Result ResolveApplicationHtmlDocumentPath(out Path path, ProgramId id);
        Result ResolveDataPath(out Path path, DataId id);
        Result RedirectApplicationControlPath(in Path path, ProgramId id, ProgramId ownerId);
        Result RedirectApplicationHtmlDocumentPath(in Path path, ProgramId id, ProgramId ownerId);
        Result ResolveApplicationLegalInformationPath(out Path path, ProgramId id);
        Result RedirectApplicationLegalInformationPath(in Path path, ProgramId id, ProgramId ownerId);
        Result Refresh();
        Result RedirectApplicationProgramPath(in Path path, ProgramId id, ProgramId ownerId);
        Result ClearApplicationRedirection(InArray<ProgramId> excludingIds);
        Result EraseProgramRedirection(ProgramId id);
        Result EraseApplicationControlRedirection(ProgramId id);
        Result EraseApplicationHtmlDocumentRedirection(ProgramId id);
        Result EraseApplicationLegalInformationRedirection(ProgramId id);
        Result ResolveProgramPathForDebug(out Path path, ProgramId id);
        Result RedirectProgramPathForDebug(in Path path, ProgramId id);
        Result RedirectApplicationProgramPathForDebug(in Path path, ProgramId id, ProgramId ownerId);
        Result EraseProgramRedirectionForDebug(ProgramId id);
    }
}
