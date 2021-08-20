using System;
using LibHac.Fs;
using LibHac.FsSrv.Impl;
using LibHac.Ncm;
using LibHac.Util;

namespace LibHac.FsSrv
{
    /// <summary>
    /// Manages the main program registry and the multi-program registry.
    /// </summary>
    /// <remarks>Based on FS 10.0.0 (nnSdk 10.4.0)</remarks>
    public class ProgramRegistryServiceImpl
    {
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        private Configuration _config;
        private ProgramRegistryManager _registryManager;
        private ProgramIndexMapInfoManager _programIndexManager;

        public ProgramRegistryServiceImpl(in Configuration config)
        {
            _config = config;
            _registryManager = new ProgramRegistryManager(_config.FsServer);
            _programIndexManager = new ProgramIndexMapInfoManager();
        }

        public struct Configuration
        {
            // LibHac addition
            public FileSystemServer FsServer;
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

        /// <inheritdoc cref="ProgramIndexMapInfoManager.Reset"/>
        public Result RegisterProgramIndexMapInfo(ReadOnlySpan<ProgramIndexMapInfo> programIndexMapInfo)
        {
            return _programIndexManager.Reset(programIndexMapInfo);
        }
    }
}
