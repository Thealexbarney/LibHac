using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Ncm;
using LibHac.Sf;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.FsSrv.Sf;

public interface IFileSystemProxyForLoader : IDisposable
{
    Result OpenCodeFileSystem(ref SharedRef<IFileSystemSf> fileSystem, OutBuffer outVerificationData,
        ref readonly FspPath path, ContentAttributes attributes, ProgramId programId);

    Result IsArchivedProgram(out bool isArchived, ulong processId);
    Result SetCurrentProcess(ulong processId);
}