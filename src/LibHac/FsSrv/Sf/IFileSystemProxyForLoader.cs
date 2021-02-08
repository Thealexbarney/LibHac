using System;
using LibHac.Fs;
using LibHac.Ncm;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.FsSrv.Sf
{
    public interface IFileSystemProxyForLoader : IDisposable
    {
        Result OpenCodeFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            out CodeVerificationData verificationData, in FspPath path, ProgramId programId);

        Result IsArchivedProgram(out bool isArchived, ulong processId);
        Result SetCurrentProcess(ulong processId);
    }
}
