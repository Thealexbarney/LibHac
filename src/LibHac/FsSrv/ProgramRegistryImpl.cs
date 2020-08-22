using System;
using LibHac.Fs;
using LibHac.FsSrv.Impl;
using LibHac.Ncm;

namespace LibHac.FsSrv
{
    internal class ProgramRegistryImpl
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
