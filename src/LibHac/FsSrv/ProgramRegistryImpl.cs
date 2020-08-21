using System;
using LibHac.Fs;
using LibHac.FsSrv.Impl;
using LibHac.Ncm;

namespace LibHac.FsSrv
{
    internal class ProgramRegistryImpl
    {
        private ulong _processId;
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

        /// <summary>
        /// Registers a program with information about the program in the program registry.
        /// </summary>
        /// <param name="processId">The process ID of the program.</param>
        /// <param name="programId">The <see cref="ProgramId"/> of the program.</param>
        /// <param name="storageId">The <see cref="StorageId"/> where the program is located.</param>
        /// <param name="accessControlData">The FS access control data header located in the program's ACI.</param>
        /// <param name="accessControlDescriptor">The FS access control descriptor located in the program's ACID.</param>
        /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
        /// <see cref="ResultFs.InvalidArgument"/>: The process ID is already registered.<br/>
        /// <see cref="ResultFs.PermissionDenied"/>: Insufficient permissions.</returns>
        public Result RegisterProgram(ulong processId, ProgramId programId, StorageId storageId,
            ReadOnlySpan<byte> accessControlData, ReadOnlySpan<byte> accessControlDescriptor)
        {
            if (!ProgramInfo.IsInitialProgram(_processId))
                return ResultFs.PermissionDenied.Log();

            return _registryService.RegisterProgram(processId, programId, storageId, accessControlData,
                accessControlDescriptor);
        }

        /// <summary>
        /// Unregisters the program with the specified process ID.
        /// </summary>
        /// <param name="processId">The process ID of the program to unregister.</param>
        /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
        /// <see cref="ResultFs.InvalidArgument"/>: The process ID is not registered.<br/>
        /// <see cref="ResultFs.PermissionDenied"/>: Insufficient permissions.</returns>
        public Result UnregisterProgram(ulong processId)
        {
            if (!ProgramInfo.IsInitialProgram(_processId))
                return ResultFs.PermissionDenied.Log();

            return _registryService.UnregisterProgram(processId);
        }

        /// <summary>
        /// Gets the <see cref="ProgramInfo"/> associated with the specified process ID.
        /// </summary>
        /// <param name="programInfo">If the method returns successfully, contains the <see cref="ProgramInfo"/>
        /// associated with the specified process ID.</param>
        /// <param name="processId">The process ID of the <see cref="ProgramInfo"/> to get.</param>
        /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
        /// <see cref="ResultFs.TargetProgramNotFound"/>: The <see cref="ProgramInfo"/> was not found.</returns>
        public Result GetProgramInfo(out ProgramInfo programInfo, ulong processId)
        {
            return _registryService.GetProgramInfo(out programInfo, processId);
        }

        /// <summary>
        /// Gets the <see cref="ProgramInfo"/> associated with the specified program ID.
        /// </summary>
        /// <param name="programInfo">If the method returns successfully, contains the <see cref="ProgramInfo"/>
        /// associated with the specified program ID.</param>
        /// <param name="programId">The program ID of the <see cref="ProgramInfo"/> to get.</param>
        /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
        /// <see cref="ResultFs.TargetProgramNotFound"/>: The <see cref="ProgramInfo"/> was not found.</returns>
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
