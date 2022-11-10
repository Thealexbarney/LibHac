using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Ncm;
using LibHac.Os;

namespace LibHac.FsSrv.Impl;

public static class ProgramInfoGlobalMethods
{
    public static bool IsInitialProgram(this FileSystemServer fsSrv, ulong processId)
    {
        ref ProgramInfoGlobals g = ref fsSrv.Globals.ProgramInfo;
        using (var guard = new InitializationGuard(ref g.InitialProcessIdRangeInitGuard, fsSrv.Globals.InitMutex))
        {
            if (!guard.IsInitialized)
            {
                // Todo: We have no kernel to call into, so use hardcoded values for now
                g.InitialProcessIdMin = OsState.InitialProcessCountMin;
                g.InitialProcessIdMax = OsState.InitialProcessCountMax;

                Abort.DoAbortUnless(0 < g.InitialProcessIdMin && g.InitialProcessIdMin <= g.InitialProcessIdMax,
                    "Invalid initial process ID range");
            }
        }

        Abort.DoAbortUnless(g.InitialProcessIdMin != 0);

        return g.InitialProcessIdMin <= processId && processId <= g.InitialProcessIdMax;
    }

    public static bool IsCurrentProcess(this FileSystemServer fsSrv, ulong processId)
    {
        ref ProgramInfoGlobals g = ref fsSrv.Globals.ProgramInfo;
        using (var guard = new InitializationGuard(ref g.CurrentProcessIdInitGuard, fsSrv.Globals.InitMutex))
        {
            if (!guard.IsInitialized)
            {
                g.CurrentProcessId = fsSrv.Hos.Os.GetCurrentProcessId().Value;
            }
        }

        return g.CurrentProcessId == processId;
    }
}

internal struct ProgramInfoGlobals
{
    public nint CurrentProcessIdInitGuard;
    public ulong CurrentProcessId;

    public nint InitialProcessIdRangeInitGuard;
    public ulong InitialProcessIdMin;
    public ulong InitialProcessIdMax;

    public nint ProgramInfoForInitialProcessInitGuard;
    public ProgramInfo ProgramInfoForInitialProcess;
}

/// <summary>
/// Contains the program ID, storage location and FS permissions of a running process.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public class ProgramInfo
{
    private readonly ulong _processId;
    public ProgramId ProgramId { get; }
    public StorageId StorageId { get; }
    public AccessControl AccessControl { get; }

    public ulong ProgramIdValue => ProgramId.Value;

    private ProgramInfo(FileSystemServer fsServer, ReadOnlySpan<byte> accessControlData,
        ReadOnlySpan<byte> accessControlDescriptor)
    {
        _processId = 0;
        AccessControl = new AccessControl(fsServer, accessControlData, accessControlDescriptor, ulong.MaxValue);
        ProgramId = default;
        StorageId = 0;
    }

    public ProgramInfo(FileSystemServer fsServer, ulong processId, ProgramId programId, StorageId storageId,
        ReadOnlySpan<byte> accessControlData, ReadOnlySpan<byte> accessControlDescriptor)
    {
        _processId = processId;
        AccessControl = new AccessControl(fsServer, accessControlData, accessControlDescriptor);
        ProgramId = programId;
        StorageId = storageId;
    }

    public bool Contains(ulong processId) => _processId == processId;

    public static ProgramInfo GetProgramInfoForInitialProcess(FileSystemServer fsSrv)
    {
        ref ProgramInfoGlobals g = ref fsSrv.Globals.ProgramInfo;
        using (var guard = new InitializationGuard(ref g.ProgramInfoForInitialProcessInitGuard, fsSrv.Globals.InitMutex))
        {
            if (!guard.IsInitialized)
            {
                g.ProgramInfoForInitialProcess = new ProgramInfo(fsSrv, InitialProcessAccessControlDataHeader,
                    InitialProcessAccessControlDescriptor);
            }
        }

        return g.ProgramInfoForInitialProcess;
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