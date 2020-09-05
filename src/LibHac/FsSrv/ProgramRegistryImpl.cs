using System;
using LibHac.Fs;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Ncm;

namespace LibHac.FsSrv
{
    /// <summary>
    /// Used to add, remove or access the Program Registry.
    /// </summary>
    /// <remarks>Every process that is launched has information registered with FS. This information
    /// is stored in a <see cref="ProgramInfo"/> and includes the process' process ID, program ID,
    /// storage location and file system permissions. This allows FS to resolve the program ID and
    /// verify the permissions of any process calling it. 
    /// <br/>Based on FS 10.0.0 (nnSdk 10.4.0)</remarks>
    public class ProgramRegistryImpl : IProgramRegistry
    {
        private ulong _processId;

        // Note: FS keeps this object as a global variable
        private readonly ProgramRegistryServiceImpl _registryService;

        public ProgramRegistryImpl(ProgramRegistryServiceImpl registryService)
        {
            _processId = ulong.MaxValue;
            _registryService = registryService;
        }

        public ProgramRegistryImpl(ProgramRegistryServiceImpl registryService, ulong processId)
        {
            _processId = processId;
            _registryService = registryService;
        }

        /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
        /// <see cref="ResultFs.InvalidArgument"/>: The process ID is already registered.<br/>
        /// <see cref="ResultFs.PermissionDenied"/>: Insufficient permissions.</returns>
        /// <inheritdoc cref="ProgramRegistryManager.RegisterProgram"/>
        public Result RegisterProgram(ulong processId, ProgramId programId, StorageId storageId,
            ReadOnlySpan<byte> accessControlData, ReadOnlySpan<byte> accessControlDescriptor)
        {
            if (!ProgramInfo.IsInitialProgram(_processId))
                return ResultFs.PermissionDenied.Log();

            return _registryService.RegisterProgram(processId, programId, storageId, accessControlData,
                accessControlDescriptor);
        }

        /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
        /// <see cref="ResultFs.InvalidArgument"/>: The process ID is not registered.<br/>
        /// <see cref="ResultFs.PermissionDenied"/>: Insufficient permissions.</returns>
        /// <inheritdoc cref="ProgramRegistryManager.UnregisterProgram" />
        public Result UnregisterProgram(ulong processId)
        {
            if (!ProgramInfo.IsInitialProgram(_processId))
                return ResultFs.PermissionDenied.Log();

            return _registryService.UnregisterProgram(processId);
        }

        /// <inheritdoc cref="ProgramRegistryManager.GetProgramInfo"/>
        public Result GetProgramInfo(out ProgramInfo programInfo, ulong processId)
        {
            return _registryService.GetProgramInfo(out programInfo, processId);
        }

        /// <inheritdoc cref="ProgramRegistryManager.GetProgramInfoByProgramId"/>
        public Result GetProgramInfoByProgramId(out ProgramInfo programInfo, ulong programId)
        {
            return _registryService.GetProgramInfoByProgramId(out programInfo, programId);
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
    }
}
