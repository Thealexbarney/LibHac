using System;
using LibHac.Ncm;

namespace LibHac.FsSrv.Sf
{
    public interface IProgramRegistry
    {
        Result RegisterProgram(ulong processId, ProgramId programId, StorageId storageId,
            ReadOnlySpan<byte> accessControlData, ReadOnlySpan<byte> accessControlDescriptor);

        Result UnregisterProgram(ulong processId);
        Result SetCurrentProcess(ulong processId);
    }
}
