using System;
using LibHac.Ncm;

namespace LibHac.Lr
{
    public class RegisteredLocationResolver : IDisposable
    {
        private ReferenceCountedDisposable<IRegisteredLocationResolver> _interface;

        public RegisteredLocationResolver(ReferenceCountedDisposable<IRegisteredLocationResolver> baseInterface)
        {
            _interface = baseInterface.AddReference();
        }

        public Result ResolveProgramPath(out Path path, ProgramId id) =>
            _interface.Target.ResolveProgramPath(out path, id);

        public Result RegisterProgramPath(in Path path, ProgramId id, ProgramId ownerId) =>
            _interface.Target.RegisterProgramPath(in path, id, ownerId);

        public Result UnregisterProgramPath(ProgramId id) =>
        _interface.Target.UnregisterProgramPath(id);

        public Result RedirectProgramPath(in Path path, ProgramId id, ProgramId ownerId) =>
            _interface.Target.RedirectProgramPath(in path, id, ownerId);

        public Result ResolveHtmlDocumentPath(out Path path, ProgramId id) =>
            _interface.Target.ResolveHtmlDocumentPath(out path, id);

        public Result RegisterHtmlDocumentPath(in Path path, ProgramId id, ProgramId ownerId) =>
            _interface.Target.RegisterHtmlDocumentPath(in path, id, ownerId);

        public Result UnregisterHtmlDocumentPath(ProgramId id) =>
            _interface.Target.UnregisterHtmlDocumentPath(id);

        public Result RedirectHtmlDocumentPath(in Path path, ProgramId id) =>
            _interface.Target.RedirectHtmlDocumentPath(in path, id);

        public Result Refresh() =>
            _interface.Target.Refresh();

        public Result RefreshExcluding(ReadOnlySpan<ProgramId> ids) =>
            _interface.Target.RefreshExcluding(ids);

        public void Dispose()
        {
            _interface?.Dispose();
        }
    }
}
