using System;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Impl;
using LibHac.Sf;
using LibHac.Util;

namespace LibHac.FsSrv
{
    /// <summary>
    /// Used to perform operations on the program index registry.
    /// </summary>
    /// <remarks>Appropriate methods calls on IFileSystemProxy are forwarded to this class
    /// which then checks the calling process' permissions and performs the requested operation.
    /// <br/>Based on FS 10.0.0 (nnSdk 10.4.0)</remarks>
    internal readonly struct ProgramIndexRegistryService
    {
        private ProgramRegistryServiceImpl ServiceImpl { get; }
        private ulong ProcessId { get; }

        public ProgramIndexRegistryService(ProgramRegistryServiceImpl serviceImpl, ulong processId)
        {
            ServiceImpl = serviceImpl;
            ProcessId = processId;
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
            Result rc = GetProgramInfo(out ProgramInfo programInfo, ProcessId);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.RegisterProgramIndexMapInfo))
                return ResultFs.PermissionDenied.Log();

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
            return ServiceImpl.RegisterProgramIndexMapInfo(mapInfo.Slice(0, programCount));
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
            Result rc = GetProgramInfo(out ProgramInfo programInfo, ProcessId);
            if (rc.IsFailure()) return rc;

            // Try to get map info for this process
            Optional<ProgramIndexMapInfo> mapInfo = ServiceImpl.GetProgramIndexMapInfo(programInfo.ProgramId);

            // Set the output program index if map info was found
            programIndex = mapInfo.HasValue ? mapInfo.ValueRo.ProgramIndex : 0;

            // Set the number of programs in the current application
            programCount = ServiceImpl.GetProgramIndexMapInfoCount();

            return Result.Success;
        }

        /// <inheritdoc cref="ProgramRegistryServiceImpl.GetProgramInfo"/>
        private Result GetProgramInfo(out ProgramInfo programInfo, ulong processId)
        {
            return ServiceImpl.GetProgramInfo(out programInfo, processId);
        }
    }
}
