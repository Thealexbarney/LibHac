using LibHac.Fs;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Ncm;
using LibHac.Sf;

namespace LibHac.FsSrv
{
    public static class ProgramRegistryImplGlobalMethods
    {
        public static void InitializeProgramRegistryImpl(this FileSystemServer fsSrv,
            ProgramRegistryServiceImpl serviceImpl)
        {
            fsSrv.Globals.ProgramRegistryImpl.ServiceImpl = serviceImpl;
        }
    }

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
    /// <br/>Based on FS 10.0.0 (nnSdk 10.4.0)</remarks>
    public class ProgramRegistryImpl : IProgramRegistry
    {
        private FileSystemServer _fsServer;
        private ulong _processId;

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
        public Result RegisterProgram(ulong processId, ProgramId programId, StorageId storageId,
            InBuffer accessControlData, InBuffer accessControlDescriptor)
        {
            if (!ProgramInfo.IsInitialProgram(_processId))
                return ResultFs.PermissionDenied.Log();

            return Globals.ServiceImpl.RegisterProgramInfo(processId, programId, storageId, accessControlData.Buffer,
                accessControlDescriptor.Buffer);
        }

        /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
        /// <see cref="ResultFs.InvalidArgument"/>: The process ID is not registered.<br/>
        /// <see cref="ResultFs.PermissionDenied"/>: Insufficient permissions.</returns>
        /// <inheritdoc cref="ProgramRegistryManager.UnregisterProgram" />
        public Result UnregisterProgram(ulong processId)
        {
            if (!ProgramInfo.IsInitialProgram(_processId))
                return ResultFs.PermissionDenied.Log();

            return Globals.ServiceImpl.UnregisterProgramInfo(processId);
        }

        /// <inheritdoc cref="ProgramRegistryManager.GetProgramInfo"/>
        public Result GetProgramInfo(out ProgramInfo programInfo, ulong processId)
        {
            return Globals.ServiceImpl.GetProgramInfo(out programInfo, processId);
        }

        /// <inheritdoc cref="ProgramRegistryManager.GetProgramInfoByProgramId"/>
        public Result GetProgramInfoByProgramId(out ProgramInfo programInfo, ulong programId)
        {
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
    }
}
