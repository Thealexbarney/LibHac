using System;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Impl;
using LibHac.Ncm;
using LibHac.Sf;
using LibHac.Util;

namespace LibHac.FsSrv;

/// <summary>
/// Used to perform operations on the program index registry.
/// </summary>
/// <remarks>Appropriate methods calls on IFileSystemProxy are forwarded to this class
/// which then checks the calling process' permissions and performs the requested operation.
/// <para>Based on nnSdk 13.4.0 (FS 13.1.0)</para></remarks>
internal readonly struct ProgramIndexRegistryService
{
    private readonly ProgramRegistryServiceImpl _serviceImpl;
    private readonly ulong _processId;

    // LibHac addition
    private readonly FileSystemServer _fsServer;

    public ProgramIndexRegistryService(FileSystemServer fsServer, ProgramRegistryServiceImpl serviceImpl,
        ulong processId)
    {
        _serviceImpl = serviceImpl;
        _processId = processId;
        _fsServer = fsServer;
    }

    /// <summary>
    /// Unregisters any previously registered program index map info and registers the provided map info.
    /// </summary>
    /// <param name="programIndexMapInfo">A buffer containing the program map info to register.</param>
    /// <param name="programCount">The number of programs to register. The provided buffer must be
    /// large enough to hold this many <see cref="ProgramIndexMapInfo"/> entries.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.PermissionDenied"/>: Insufficient permissions.<br/>
    /// <see cref="ResultFs.InvalidSize"/>: The buffer was too small to hold the specified
    /// number of <see cref="ProgramIndexMapInfo"/> entries.</returns>
    public Result RegisterProgramIndexMapInfo(InBuffer programIndexMapInfo, int programCount)
    {
        // Verify the caller's permissions
        using (var programRegistry = new ProgramRegistryImpl(_fsServer))
        {
            Result res = programRegistry.GetProgramInfo(out ProgramInfo programInfo, _processId);
            if (res.IsFailure()) return res.Miss();

            if (!programInfo.AccessControl.CanCall(OperationType.RegisterProgramIndexMapInfo))
                return ResultFs.PermissionDenied.Log();
        }

        // Return early if the program count is 0 so we leave any previously
        // registered entries as they were
        if (programCount == 0)
            return Result.Success;

        // Verify that the provided buffer is large enough to hold "programCount" entries
        ReadOnlySpan<ProgramIndexMapInfo>
            mapInfo = MemoryMarshal.Cast<byte, ProgramIndexMapInfo>(programIndexMapInfo.Buffer);

        if (mapInfo.Length < programCount)
            return ResultFs.InvalidSize.Log();

        // Register the map info
        return _serviceImpl.ResetProgramIndexMapInfo(mapInfo.Slice(0, programCount));
    }

    /// <summary>
    /// Gets the multi-program index of the calling process and the number of programs
    /// in the current application.
    /// </summary>
    /// <param name="programIndex">When this method returns successfully, contains the
    /// program index of the calling process.</param>
    /// <param name="programCount">When this method returns successfully, contains the
    /// number of programs in the current application.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.ProgramInfoNotFound"/>: The calling program was not found
    /// in the program registry. Something's wrong with Loader if this happens.</returns>
    public Result GetProgramIndex(out int programIndex, out int programCount)
    {
        UnsafeHelpers.SkipParamInit(out programIndex, out programCount);

        // No permissions are needed to call this method
        Result res = GetProgramInfo(out ProgramInfo programInfo, _processId);
        if (res.IsFailure()) return res.Miss();

        // Try to get map info for this process
        Optional<ProgramIndexMapInfo> mapInfo = _serviceImpl.GetProgramIndexMapInfo(programInfo.ProgramId);

        // Set the output program index if map info was found
        programIndex = mapInfo.HasValue ? mapInfo.ValueRo.ProgramIndex : 0;

        // Set the number of programs in the current application
        programCount = _serviceImpl.GetProgramIndexMapInfoCount();

        return Result.Success;
    }

    /// <inheritdoc cref="ProgramRegistryServiceImpl.GetProgramInfo"/>
    private Result GetProgramInfo(out ProgramInfo programInfo, ulong processId)
    {
        using var programRegistry = new ProgramRegistryImpl(_fsServer);
        return programRegistry.GetProgramInfo(out programInfo, processId);
    }
}

/// <summary>
/// Manages the main program registry and the multi-program registry.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public class ProgramRegistryServiceImpl : IDisposable
{
    // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
    private Configuration _config;
    private ProgramRegistryManager _registryManager;
    private ProgramIndexMapInfoManager _programIndexManager;

    public struct Configuration
    {
        // LibHac addition
        public FileSystemServer FsServer;
    }

    public ProgramRegistryServiceImpl(in Configuration config)
    {
        _config = config;
        _registryManager = new ProgramRegistryManager(_config.FsServer);
        _programIndexManager = new ProgramIndexMapInfoManager();
    }

    public void Dispose()
    {
        _registryManager?.Dispose();
        _programIndexManager?.Dispose();
    }

    /// <inheritdoc cref="ProgramRegistryManager.RegisterProgram"/>
    public Result RegisterProgramInfo(ulong processId, ProgramId programId, StorageId storageId,
        ReadOnlySpan<byte> accessControlData, ReadOnlySpan<byte> accessControlDescriptor)
    {
        return _registryManager.RegisterProgram(processId, programId, storageId, accessControlData,
            accessControlDescriptor);
    }

    /// <inheritdoc cref="ProgramRegistryManager.UnregisterProgram" />
    public Result UnregisterProgramInfo(ulong processId)
    {
        return _registryManager.UnregisterProgram(processId);
    }

    /// <inheritdoc cref="ProgramRegistryManager.GetProgramInfo"/>
    public Result GetProgramInfo(out ProgramInfo programInfo, ulong processId)
    {
        return _registryManager.GetProgramInfo(out programInfo, processId);
    }

    /// <inheritdoc cref="ProgramRegistryManager.GetProgramInfoByProgramId"/>
    public Result GetProgramInfoByProgramId(out ProgramInfo programInfo, ulong programId)
    {
        return _registryManager.GetProgramInfoByProgramId(out programInfo, programId);
    }

    /// <inheritdoc cref="ProgramIndexMapInfoManager.Reset"/>
    public Result ResetProgramIndexMapInfo(ReadOnlySpan<ProgramIndexMapInfo> programIndexMapInfo)
    {
        return _programIndexManager.Reset(programIndexMapInfo);
    }

    /// <inheritdoc cref="ProgramIndexMapInfoManager.GetProgramId"/>
    public ProgramId GetProgramIdByIndex(ProgramId programId, byte programIndex)
    {
        return _programIndexManager.GetProgramId(programId, programIndex);
    }

    /// <inheritdoc cref="ProgramIndexMapInfoManager.Get"/>
    public Optional<ProgramIndexMapInfo> GetProgramIndexMapInfo(ProgramId programId)
    {
        return _programIndexManager.Get(programId);
    }

    /// <summary>
    /// Gets the number of programs in the currently registered application.
    /// </summary>
    /// <returns>The number of programs.</returns>
    public int GetProgramIndexMapInfoCount()
    {
        return _programIndexManager.GetProgramCount();
    }
}