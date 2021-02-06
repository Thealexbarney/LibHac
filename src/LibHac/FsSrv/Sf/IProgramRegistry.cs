using System;
using LibHac.Ncm;
using LibHac.Sf;

namespace LibHac.FsSrv.Sf
{
    public interface IProgramRegistry : IDisposable
    {
        Result RegisterProgram(ulong processId, ProgramId programId, StorageId storageId,
            InBuffer accessControlData, InBuffer accessControlDescriptor);

        Result UnregisterProgram(ulong processId);
        Result SetCurrentProcess(ulong processId);
    }
}
