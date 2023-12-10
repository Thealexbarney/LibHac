using System;
using LibHac.Ncm;

namespace LibHac.Lr;

public interface IRegisteredLocationResolver : IDisposable
{
    Result ResolveProgramPath(out Path path, ProgramId id);
    Result RegisterProgramPath(ref readonly Path path, ProgramId id, ProgramId ownerId);
    Result UnregisterProgramPath(ProgramId id);
    Result RedirectProgramPath(ref readonly Path path, ProgramId id, ProgramId ownerId);
    Result ResolveHtmlDocumentPath(out Path path, ProgramId id);
    Result RegisterHtmlDocumentPath(ref readonly Path path, ProgramId id, ProgramId ownerId);
    Result UnregisterHtmlDocumentPath(ProgramId id);
    Result RedirectHtmlDocumentPath(ref readonly Path path, ProgramId id);
    Result Refresh();
    Result RefreshExcluding(ReadOnlySpan<ProgramId> ids);
}