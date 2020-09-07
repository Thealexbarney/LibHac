using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Ncm;

namespace LibHac.FsSrv.Sf
{
    public interface IFileSystemProxyForLoader
    {
        Result OpenCodeFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem,
            out CodeVerificationData verificationData, in FspPath path, ProgramId programId);

        Result IsArchivedProgram(out bool isArchived, ulong processId);
        Result SetCurrentProcess(ulong processId);
    }
}
