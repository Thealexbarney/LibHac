using System;
using System.Collections.Generic;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Ncm;

namespace LibHac.FsSrv.Impl
{
    /// <summary>
    /// Handles adding, removing, and accessing <see cref="ProgramInfo"/> from the <see cref="ProgramRegistryImpl"/>.
    /// </summary>
    /// <remarks>Based on FS 10.0.0 (nnSdk 10.4.0)</remarks>
    internal class ProgramRegistryManager
    {
        // Note: FS keeps each ProgramInfo in a shared_ptr, but there aren't any non-memory resources
        // that need to be freed, so we use plain ProgramInfos
        private LinkedList<ProgramInfo> ProgramInfoList { get; }
        private FileSystemServer FsServer { get; }

        // Note: This variable is global in FS. It's moved to ProgramRegistryManager here because it
        // relies on some state kept in FileSystemServer, and it's only used by ProgramRegistryManager
        private ProgramInfo _programInfoForInitialProcess;
        private readonly object _programInfoForInitialProcessGuard = new object();

        public ProgramRegistryManager(FileSystemServer fsServer)
        {
            ProgramInfoList = new LinkedList<ProgramInfo>();
            FsServer = fsServer;
        }

        private ProgramInfo GetProgramInfoForInitialProcess()
        {
            if (_programInfoForInitialProcess == null)
            {
                lock (_programInfoForInitialProcessGuard)
                {
                    _programInfoForInitialProcess ??= ProgramInfo.CreateProgramInfoForInitialProcess(FsServer);
                }
            }

            return _programInfoForInitialProcess;
        }

        /// <summary>
        /// Registers a program with information about that program in the program registry.
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
            var programInfo = new ProgramInfo(FsServer, processId, programId, storageId, accessControlData,
                accessControlDescriptor);

            lock (ProgramInfoList)
            {
                foreach (ProgramInfo info in ProgramInfoList)
                {
                    if (info.Contains(processId))
                        return ResultFs.InvalidArgument.Log();
                }

                ProgramInfoList.AddLast(programInfo);
                return Result.Success;
            }
        }

        /// <summary>
        /// Unregisters the program with the specified process ID.
        /// </summary>
        /// <param name="processId">The process ID of the program to unregister.</param>
        /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
        /// <see cref="ResultFs.InvalidArgument"/>: The process ID is not registered.</returns>
        public Result UnregisterProgram(ulong processId)
        {
            lock (ProgramInfoList)
            {
                for (LinkedListNode<ProgramInfo> node = ProgramInfoList.First; node != null; node = node.Next)
                {
                    if (node.Value.Contains(processId))
                    {
                        ProgramInfoList.Remove(node);
                        return Result.Success;
                    }
                }

                return ResultFs.InvalidArgument.Log();
            }
        }

        /// <summary>
        /// Gets the <see cref="ProgramInfo"/> associated with the specified process ID.
        /// </summary>
        /// <param name="programInfo">If the method returns successfully, contains the <see cref="ProgramInfo"/>
        /// associated with the specified process ID.</param>
        /// <param name="processId">The process ID of the <see cref="ProgramInfo"/> to get.</param>
        /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
        /// <see cref="ResultFs.ProgramInfoNotFound"/>: The <see cref="ProgramInfo"/> was not found.</returns>
        public Result GetProgramInfo(out ProgramInfo programInfo, ulong processId)
        {
            lock (ProgramInfoList)
            {
                if (ProgramInfo.IsInitialProgram(processId))
                {
                    programInfo = GetProgramInfoForInitialProcess();
                    return Result.Success;
                }

                foreach (ProgramInfo info in ProgramInfoList)
                {
                    if (info.Contains(processId))
                    {
                        programInfo = info;
                        return Result.Success;
                    }
                }

                UnsafeHelpers.SkipParamInit(out programInfo);
                return ResultFs.ProgramInfoNotFound.Log();
            }
        }

        /// <summary>
        /// Gets the <see cref="ProgramInfo"/> associated with the specified program ID.
        /// </summary>
        /// <param name="programInfo">If the method returns successfully, contains the <see cref="ProgramInfo"/>
        /// associated with the specified program ID.</param>
        /// <param name="programId">The program ID of the <see cref="ProgramInfo"/> to get.</param>
        /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
        /// <see cref="ResultFs.ProgramInfoNotFound"/>: The <see cref="ProgramInfo"/> was not found.</returns>
        public Result GetProgramInfoByProgramId(out ProgramInfo programInfo, ulong programId)
        {
            lock (ProgramInfoList)
            {
                foreach (ProgramInfo info in ProgramInfoList)
                {
                    if (info.ProgramId.Value == programId)
                    {
                        programInfo = info;
                        return Result.Success;
                    }
                }

                UnsafeHelpers.SkipParamInit(out programInfo);
                return ResultFs.ProgramInfoNotFound.Log();
            }
        }
    }

    public class ProgramInfo
    {
        private ulong ProcessId { get; }
        public ProgramId ProgramId { get; }
        public StorageId StorageId { get; }
        public AccessControl AccessControl { get; }

        public ulong ProgramIdValue => ProgramId.Value;

        public ProgramInfo(FileSystemServer fsServer, ulong processId, ProgramId programId, StorageId storageId,
            ReadOnlySpan<byte> accessControlData, ReadOnlySpan<byte> accessControlDescriptor)
        {
            ProcessId = processId;
            AccessControl = new AccessControl(fsServer, accessControlData, accessControlDescriptor);
            ProgramId = programId;
            StorageId = storageId;
        }

        private ProgramInfo(FileSystemServer fsServer, ReadOnlySpan<byte> accessControlData,
            ReadOnlySpan<byte> accessControlDescriptor)
        {
            ProcessId = 0;
            AccessControl = new AccessControl(fsServer, accessControlData, accessControlDescriptor, ulong.MaxValue);
            ProgramId = default;
            StorageId = 0;
        }

        public bool Contains(ulong processId) => ProcessId == processId;

        public static bool IsInitialProgram(ulong processId)
        {
            // Todo: We have no kernel to call into, so use hardcoded values for now
            const int initialProcessIdLowerBound = 1;
            const int initialProcessIdUpperBound = 0x50;

            return initialProcessIdLowerBound <= processId && processId <= initialProcessIdUpperBound;
        }

        internal static ProgramInfo CreateProgramInfoForInitialProcess(FileSystemServer fsServer)
        {
            return new ProgramInfo(fsServer, InitialProcessAccessControlDataHeader,
                InitialProcessAccessControlDescriptor);
        }

        private static ReadOnlySpan<byte> InitialProcessAccessControlDataHeader => new byte[]
        {
            0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80, 0x1C, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x1C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        };

        private static ReadOnlySpan<byte> InitialProcessAccessControlDescriptor => new byte[]
        {
            0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
        };
    }
}
