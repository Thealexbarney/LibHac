using System;
using LibHac.Common;
using LibHac.Ncm;

namespace LibHac.Lr;

public class RegisteredLocationResolver : IDisposable
{
    private SharedRef<IRegisteredLocationResolver> _interface;

    public RegisteredLocationResolver(ref readonly SharedRef<IRegisteredLocationResolver> baseInterface)
    {
        _interface = SharedRef<IRegisteredLocationResolver>.CreateCopy(in baseInterface);
    }

    public void Dispose()
    {
        _interface.Destroy();
    }

    public Result ResolveProgramPath(out Path path, ProgramId id) =>
        _interface.Get.ResolveProgramPath(out path, id);

    public Result RegisterProgramPath(ref readonly Path path, ProgramId id, ProgramId ownerId) =>
        _interface.Get.RegisterProgramPath(in path, id, ownerId);

    public Result UnregisterProgramPath(ProgramId id) =>
        _interface.Get.UnregisterProgramPath(id);

    public Result RedirectProgramPath(ref readonly Path path, ProgramId id, ProgramId ownerId) =>
        _interface.Get.RedirectProgramPath(in path, id, ownerId);

    public Result ResolveHtmlDocumentPath(out Path path, ProgramId id) =>
        _interface.Get.ResolveHtmlDocumentPath(out path, id);

    public Result RegisterHtmlDocumentPath(ref readonly Path path, ProgramId id, ProgramId ownerId) =>
        _interface.Get.RegisterHtmlDocumentPath(in path, id, ownerId);

    public Result UnregisterHtmlDocumentPath(ProgramId id) =>
        _interface.Get.UnregisterHtmlDocumentPath(id);

    public Result RedirectHtmlDocumentPath(ref readonly Path path, ProgramId id) =>
        _interface.Get.RedirectHtmlDocumentPath(in path, id);

    public Result Refresh() =>
        _interface.Get.Refresh();

    public Result RefreshExcluding(ReadOnlySpan<ProgramId> ids) =>
        _interface.Get.RefreshExcluding(ids);
}