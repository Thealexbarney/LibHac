using System;
using LibHac.Fs;
using LibHac.FsSrv.Impl;
using LibHac.Ncm;

namespace LibHac.FsSrv
{
    /// <summary>
    /// Manages the main program registry and the multi-program registry.
    /// </summary>
    public class ProgramRegistryServiceImpl
    {
        private ProgramRegistryManager RegistryManager { get; }
        private ProgramIndexMapInfoManager ProgramIndexManager { get; }

        public ProgramRegistryServiceImpl(FileSystemServer fsServer)
        {
            RegistryManager = new ProgramRegistryManager(fsServer);
            ProgramIndexManager = new ProgramIndexMapInfoManager();
        }

        /// <inheritdoc cref="ProgramRegistryManager.RegisterProgram"/>
        public Result RegisterProgram(ulong processId, ProgramId programId, StorageId storageId,
            ReadOnlySpan<byte> accessControlData, ReadOnlySpan<byte> accessControlDescriptor)
        {
            return RegistryManager.RegisterProgram(processId, programId, storageId, accessControlData,
                accessControlDescriptor);
        }

        /// <inheritdoc cref="ProgramRegistryManager.UnregisterProgram" />
        public Result UnregisterProgram(ulong processId)
        {
            return RegistryManager.UnregisterProgram(processId);
        }

        /// <inheritdoc cref="ProgramRegistryManager.GetProgramInfo"/>
        public Result GetProgramInfo(out ProgramInfo programInfo, ulong processId)
        {
            return RegistryManager.GetProgramInfo(out programInfo, processId);
        }

        /// <inheritdoc cref="ProgramRegistryManager.GetProgramInfoByProgramId"/>
        public Result GetProgramInfoByProgramId(out ProgramInfo programInfo, ulong programId)
        {
            return RegistryManager.GetProgramInfoByProgramId(out programInfo, programId);
        }

        /// <inheritdoc cref="ProgramIndexMapInfoManager.GetProgramId"/>
        public ProgramId GetProgramId(ProgramId programId, byte programIndex)
        {
            return ProgramIndexManager.GetProgramId(programId, programIndex);
        }

        /// <inheritdoc cref="ProgramIndexMapInfoManager.Get"/>
        public ProgramIndexMapInfo? GetProgramIndexMapInfo(ProgramId programId)
        {
            return ProgramIndexManager.Get(programId);
        }

        /// <summary>
        /// Gets the number of programs in the currently registered application.
        /// </summary>
        /// <returns>The number of programs.</returns>
        public int GetProgramIndexMapInfoCount()
        {
            return ProgramIndexManager.GetProgramCount();
        }

        /// <inheritdoc cref="ProgramIndexMapInfoManager.Reset"/>
        public Result RegisterProgramIndexMapInfo(ReadOnlySpan<ProgramIndexMapInfo> programIndexMapInfo)
        {
            return ProgramIndexManager.Reset(programIndexMapInfo);
        }
    }
}
