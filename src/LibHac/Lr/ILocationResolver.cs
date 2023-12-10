using System;
using LibHac.Ncm;
using LibHac.Sf;

namespace LibHac.Lr;

public interface ILocationResolver : IDisposable
{
    Result ResolveProgramPath(out Path path, ProgramId id);
    Result RedirectProgramPath(ref readonly Path path, ProgramId id);
    Result ResolveApplicationControlPath(out Path path, ProgramId id);
    Result ResolveApplicationHtmlDocumentPath(out Path path, ProgramId id);
    Result ResolveDataPath(out Path path, DataId id);
    Result RedirectApplicationControlPath(ref readonly Path path, ProgramId id, ProgramId ownerId);
    Result RedirectApplicationHtmlDocumentPath(ref readonly Path path, ProgramId id, ProgramId ownerId);
    Result ResolveApplicationLegalInformationPath(out Path path, ProgramId id);
    Result RedirectApplicationLegalInformationPath(ref readonly Path path, ProgramId id, ProgramId ownerId);
    Result Refresh();
    Result RedirectApplicationProgramPath(ref readonly Path path, ProgramId id, ProgramId ownerId);
    Result ClearApplicationRedirection(InArray<ProgramId> excludingIds);
    Result EraseProgramRedirection(ProgramId id);
    Result EraseApplicationControlRedirection(ProgramId id);
    Result EraseApplicationHtmlDocumentRedirection(ProgramId id);
    Result EraseApplicationLegalInformationRedirection(ProgramId id);
    Result ResolveProgramPathForDebug(out Path path, ProgramId id);
    Result RedirectProgramPathForDebug(ref readonly Path path, ProgramId id);
    Result RedirectApplicationProgramPathForDebug(ref readonly Path path, ProgramId id, ProgramId ownerId);
    Result EraseProgramRedirectionForDebug(ProgramId id);
}