using System;
using System.Collections.Generic;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Ncm;
using LibHac.Os;

namespace LibHac.FsSrv.Impl;

/// <summary>
/// Handles adding, removing, and accessing <see cref="ProgramInfo"/> from the <see cref="ProgramRegistryImpl"/>.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
internal class ProgramRegistryManager : IDisposable
{
    // Note: FS keeps each ProgramInfo in a shared_ptr, but there aren't any non-memory resources
    // that need to be freed, so we use plain ProgramInfos
    private LinkedList<ProgramInfo> _programInfoList;
    private SdkMutexType _mutex;

    private FileSystemServer _fsServer;

    public ProgramRegistryManager(FileSystemServer fsServer)
    {
        _programInfoList = new LinkedList<ProgramInfo>();
        _mutex = new SdkMutexType();
        _fsServer = fsServer;
    }

    public void Dispose() { }

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
        var programInfo = new ProgramInfo(_fsServer, processId, programId, storageId, accessControlData,
            accessControlDescriptor);

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        foreach (ProgramInfo info in _programInfoList)
        {
            if (info.Contains(processId))
                return ResultFs.InvalidArgument.Log();
        }

        _programInfoList.AddLast(programInfo);
        return Result.Success;
    }

    /// <summary>
    /// Unregisters the program with the specified process ID.
    /// </summary>
    /// <param name="processId">The process ID of the program to unregister.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.InvalidArgument"/>: The process ID is not registered.</returns>
    public Result UnregisterProgram(ulong processId)
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        for (LinkedListNode<ProgramInfo> node = _programInfoList.First; node != null; node = node.Next)
        {
            if (node.Value.Contains(processId))
            {
                _programInfoList.Remove(node);
                return Result.Success;
            }
        }

        return ResultFs.InvalidArgument.Log();
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
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        if (_fsServer.IsInitialProgram(processId))
        {
            programInfo = ProgramInfo.GetProgramInfoForInitialProcess(_fsServer);
            return Result.Success;
        }

        foreach (ProgramInfo info in _programInfoList)
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
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        foreach (ProgramInfo info in _programInfoList)
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