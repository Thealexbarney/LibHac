using System;
using LibHac.Common;
using LibHac.FsSrv;
using LibHac.FsSrv.Sf;
using LibHac.Ncm;
using LibHac.Sf;

namespace LibHac.Fs.Shim;

public static class ProgramRegistry
{
    /// <inheritdoc cref="ProgramRegistryImpl.RegisterProgram"/>
    public static Result RegisterProgram(this FileSystemClient fs, ulong processId, ProgramId programId,
        StorageId storageId, ReadOnlySpan<byte> accessControlData, ReadOnlySpan<byte> accessControlDescriptor)
    {
        using SharedRef<IProgramRegistry> programRegistry = fs.Impl.GetProgramRegistryServiceObject();

        Result rc = programRegistry.Get.SetCurrentProcess(fs.Hos.Os.GetCurrentProcessId().Value);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        rc = programRegistry.Get.RegisterProgram(processId, programId, storageId, new InBuffer(accessControlData),
            new InBuffer(accessControlDescriptor));

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    /// <inheritdoc cref="ProgramRegistryImpl.UnregisterProgram"/>
    public static Result UnregisterProgram(this FileSystemClient fs, ulong processId)
    {
        using SharedRef<IProgramRegistry> programRegistry = fs.Impl.GetProgramRegistryServiceObject();

        Result rc = programRegistry.Get.SetCurrentProcess(fs.Hos.Os.GetCurrentProcessId().Value);
        if (rc.IsFailure()) return rc.Miss();

        rc = programRegistry.Get.UnregisterProgram(processId);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }
}
