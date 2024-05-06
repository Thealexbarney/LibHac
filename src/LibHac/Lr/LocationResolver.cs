using System;
using LibHac.Common;
using LibHac.Ncm;
using LibHac.Sf;

namespace LibHac.Lr;

public class LocationResolver : IDisposable
{
    private SharedRef<ILocationResolver> _interface;

    public LocationResolver(ref readonly SharedRef<ILocationResolver> baseInterface)
    {
        _interface = SharedRef<ILocationResolver>.CreateCopy(in baseInterface);
    }

    public void Dispose()
    {
        _interface.Destroy();
    }

    public Result ResolveProgramPath(out Path path, ProgramId id) =>
        _interface.Get.ResolveProgramPath(out path, id);

    public Result RedirectProgramPath(ref readonly Path path, ProgramId id) =>
        _interface.Get.RedirectProgramPath(in path, id);

    public Result ResolveApplicationControlPath(out Path path, ProgramId id) =>
        _interface.Get.ResolveApplicationControlPath(out path, id);

    public Result ResolveApplicationHtmlDocumentPath(out Path path, ProgramId id) =>
        _interface.Get.ResolveApplicationHtmlDocumentPath(out path, id);

    public Result ResolveDataPath(out Path path, DataId id) =>
        _interface.Get.ResolveDataPath(out path, id);

    public Result RedirectApplicationControlPath(ref readonly Path path, ProgramId id, ProgramId ownerId) =>
        _interface.Get.RedirectApplicationControlPath(in path, id, ownerId);

    public Result RedirectApplicationHtmlDocumentPath(ref readonly Path path, ProgramId id, ProgramId ownerId) =>
        _interface.Get.RedirectApplicationHtmlDocumentPath(in path, id, ownerId);

    public Result ResolveApplicationLegalInformationPath(out Path path, ProgramId id) =>
        _interface.Get.ResolveApplicationLegalInformationPath(out path, id);

    public Result RedirectApplicationLegalInformationPath(ref readonly Path path, ProgramId id, ProgramId ownerId) =>
        _interface.Get.RedirectApplicationLegalInformationPath(in path, id, ownerId);

    public Result Refresh() =>
        _interface.Get.Refresh();

    public Result RedirectApplicationProgramPath(ref readonly Path path, ProgramId id, ProgramId ownerId) =>
        _interface.Get.RedirectApplicationProgramPath(in path, id, ownerId);

    public Result ClearApplicationRedirection(InArray<ProgramId> excludingIds) =>
        _interface.Get.ClearApplicationRedirection(excludingIds);

    public Result EraseProgramRedirection(ProgramId id) =>
        _interface.Get.EraseProgramRedirection(id);

    public Result EraseApplicationControlRedirection(ProgramId id) =>
        _interface.Get.EraseApplicationControlRedirection(id);

    public Result EraseApplicationHtmlDocumentRedirection(ProgramId id) =>
        _interface.Get.EraseApplicationHtmlDocumentRedirection(id);

    public Result EraseApplicationLegalInformationRedirection(ProgramId id) =>
        _interface.Get.EraseApplicationLegalInformationRedirection(id);

    public Result ResolveProgramPathForDebug(out Path path, ProgramId id) =>
        _interface.Get.ResolveProgramPathForDebug(out path, id);

    public Result RedirectProgramPathForDebug(ref readonly Path path, ProgramId id) =>
        _interface.Get.RedirectProgramPathForDebug(in path, id);

    public Result RedirectApplicationProgramPathForDebug(ref readonly Path path, ProgramId id, ProgramId ownerId) =>
        _interface.Get.RedirectApplicationProgramPathForDebug(in path, id, ownerId);

    public Result EraseProgramRedirectionForDebug(ProgramId id) =>
        _interface.Get.EraseProgramRedirectionForDebug(id);
}