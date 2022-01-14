using System;
using LibHac.Ncm;
using LibHac.Sf;

namespace LibHac.FsSrv.Sf;

public interface IProgramRegistry : IDisposable
{
    Result RegisterProgram(ulong processId, ProgramId programId, StorageId storageId, InBuffer accessControlData,
        long accessControlDataSize, InBuffer accessControlDescriptor, long accessControlDescriptorSize);

    Result UnregisterProgram(ulong processId);
    Result SetCurrentProcess(ulong processId);
}