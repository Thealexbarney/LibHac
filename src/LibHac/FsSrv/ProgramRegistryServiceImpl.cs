using System;
using LibHac.Fs;
using LibHac.FsSrv.Impl;
using LibHac.Ncm;

namespace LibHac.FsSrv
{
    public class ProgramRegistryServiceImpl
    {
        private ProgramRegistryManager RegistryManager { get; }
        private ProgramIndexMapInfoManager ProgramIndexManager { get; }

        public ProgramRegistryServiceImpl(FileSystemServer fsServer)
        {
            RegistryManager = new ProgramRegistryManager(fsServer);
            ProgramIndexManager = new ProgramIndexMapInfoManager();
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
        /// <see cref="ResultFs.InvalidArgument"/>: The process ID is already registered.</returns>
        public Result RegisterProgram(ulong processId, ProgramId programId, StorageId storageId,
            ReadOnlySpan<byte> accessControlData, ReadOnlySpan<byte> accessControlDescriptor)
        {
            return RegistryManager.RegisterProgram(processId, programId, storageId, accessControlData,
                accessControlDescriptor);
        }

        /// <summary>
        /// Unregisters the program with the specified process ID.
        /// </summary>
        /// <param name="processId">The process ID of the program to unregister.</param>
        /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
        /// <see cref="ResultFs.InvalidArgument"/>: The process ID is not registered.</returns>
        public Result UnregisterProgram(ulong processId)
        {
            return RegistryManager.UnregisterProgram(processId);
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
            return RegistryManager.GetProgramInfo(out programInfo, processId);
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
            return RegistryManager.GetProgramInfoByProgramId(out programInfo, programId);
        }

        /// <summary>
        /// Gets the <see cref="ProgramId"/> of the program with index <paramref name="programIndex"/> in the
        /// multi-program app <paramref name="programId"/> is part of.
        /// </summary>
        /// <param name="programId">A program ID in the multi-program app to query.</param>
        /// <param name="programIndex">The index of the program to get.</param>
        /// <returns>If the program exists, the ID of the program with the specified index,
        /// otherwise <see cref="ProgramId.InvalidId"/></returns>
        public ProgramId GetProgramId(ProgramId programId, byte programIndex)
        {
            return ProgramIndexManager.GetProgramId(programId, programIndex);
        }
    }
}
