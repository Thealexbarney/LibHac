using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Ncm;
using LibHac.Sf;

namespace LibHac.FsSrv;

internal struct ProgramRegistryImplGlobals
{
    public ProgramRegistryServiceImpl ServiceImpl;
}

/// <summary>
/// Used to add, remove or access the Program Registry.
/// </summary>
/// <remarks>Every process that is launched has information registered with FS. This information
/// is stored in a <see cref="ProgramInfo"/> and includes the process' process ID, program ID,
/// storage location and file system permissions. This allows FS to resolve the program ID and
/// verify the permissions of any process calling it. 
/// <para>Based on nnSdk 13.4.0 (FS 13.1.0)</para></remarks>
public class ProgramRegistryImpl : IProgramRegistry
{
    private ulong _processId;

    // LibHac addition
    private FileSystemServer _fsServer;

    private ref ProgramRegistryImplGlobals Globals => ref _fsServer.Globals.ProgramRegistryImpl;

    public ProgramRegistryImpl(FileSystemServer server)
    {
        _fsServer = server;
        _processId = ulong.MaxValue;
    }

    public void Dispose() { }

    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.InvalidArgument"/>: The process ID is already registered.<br/>
    /// <see cref="ResultFs.PermissionDenied"/>: Insufficient permissions.</returns>
    /// <inheritdoc cref="ProgramRegistryManager.RegisterProgram"/>
    public Result RegisterProgram(ulong processId, ProgramId programId, StorageId storageId, InBuffer accessControlData,
        long accessControlDataSize, InBuffer accessControlDescriptor, long accessControlDescriptorSize)
    {
        Assert.SdkRequiresNotNull(Globals.ServiceImpl);

        if (!_fsServer.IsInitialProgram(_processId))
            return ResultFs.PermissionDenied.Log();

        if (accessControlDataSize > accessControlData.Size)
            return ResultFs.InvalidSize.Log();

        if (accessControlDescriptorSize > accessControlDescriptor.Size)
            return ResultFs.InvalidSize.Log();

        return Globals.ServiceImpl.RegisterProgramInfo(processId, programId, storageId, accessControlData.Buffer,
            accessControlDescriptor.Buffer);
    }

    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.InvalidArgument"/>: The process ID is not registered.<br/>
    /// <see cref="ResultFs.PermissionDenied"/>: Insufficient permissions.</returns>
    /// <inheritdoc cref="ProgramRegistryManager.UnregisterProgram" />
    public Result UnregisterProgram(ulong processId)
    {
        Assert.SdkRequiresNotNull(Globals.ServiceImpl);

        if (!_fsServer.IsInitialProgram(_processId))
            return ResultFs.PermissionDenied.Log();

        return Globals.ServiceImpl.UnregisterProgramInfo(processId);
    }

    /// <inheritdoc cref="ProgramRegistryManager.GetProgramInfo"/>
    public Result GetProgramInfo(out ProgramInfo programInfo, ulong processId)
    {
        Assert.SdkRequiresNotNull(Globals.ServiceImpl);

        return Globals.ServiceImpl.GetProgramInfo(out programInfo, processId);
    }

    /// <inheritdoc cref="ProgramRegistryManager.GetProgramInfoByProgramId"/>
    public Result GetProgramInfoByProgramId(out ProgramInfo programInfo, ulong programId)
    {
        Assert.SdkRequiresNotNull(Globals.ServiceImpl);

        return Globals.ServiceImpl.GetProgramInfoByProgramId(out programInfo, programId);
    }

    /// <summary>
    /// Sets the process ID of the process that will use this service via IPC.
    /// </summary>
    /// <param name="processId">The process ID to set.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.</returns>
    public Result SetCurrentProcess(ulong processId)
    {
        _processId = processId;
        return Result.Success;
    }

    /// <summary>
    /// Sets the <see cref="ProgramRegistryServiceImpl"/> used by the provided <see cref="FileSystemServer"/>.
    /// This function must be called before calling functions on a <see cref="ProgramRegistryImpl"/>.
    /// This function must not be called more than once.
    /// </summary>
    /// <param name="fsServer">The <see cref="FileSystemServer"/> to initialize.</param>
    /// <param name="serviceImpl">The <see cref="ProgramRegistryServiceImpl"/>
    /// that <paramref name="fsServer"/> will use.</param>
    public static void Initialize(FileSystemServer fsServer, ProgramRegistryServiceImpl serviceImpl)
    {
        ref ProgramRegistryImplGlobals globals = ref fsServer.Globals.ProgramRegistryImpl;

        Assert.SdkRequiresNotNull(serviceImpl);
        Assert.SdkRequiresNull(globals.ServiceImpl);

        globals.ServiceImpl = serviceImpl;
    }
}