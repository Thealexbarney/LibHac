using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.FsSrv;
using LibHac.FsSrv.Sf;
using LibHac.Ncm;
using LibHac.Sf;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions for registering and unregistering currently running
/// processes and their permissions in the FS program registry.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
public static class ProgramRegistry
{
    /// <inheritdoc cref="ProgramRegistryImpl.RegisterProgram"/>
    public static Result RegisterProgram(this FileSystemClient fs, ulong processId, ProgramId programId,
        StorageId storageId, ReadOnlySpan<byte> accessControlData, ReadOnlySpan<byte> accessControlDescriptor)
    {
        using SharedRef<IProgramRegistry> programRegistry = fs.Impl.GetProgramRegistryServiceObject();

        Result res = programRegistry.Get.SetCurrentProcess(fs.Hos.Os.GetCurrentProcessId().Value);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = programRegistry.Get.RegisterProgram(processId, programId, storageId, new InBuffer(accessControlData),
            accessControlData.Length, new InBuffer(accessControlDescriptor), accessControlDescriptor.Length);

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    /// <inheritdoc cref="ProgramRegistryImpl.UnregisterProgram"/>
    public static Result UnregisterProgram(this FileSystemClient fs, ulong processId)
    {
        using SharedRef<IProgramRegistry> programRegistry = fs.Impl.GetProgramRegistryServiceObject();

        Abort.DoAbortUnlessSuccess(programRegistry.Get.SetCurrentProcess(fs.Hos.Os.GetCurrentProcessId().Value));
        Abort.DoAbortUnlessSuccess(programRegistry.Get.UnregisterProgram(processId));

        return Result.Success;
    }
}