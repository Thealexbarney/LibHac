namespace LibHac.SdmmcSrv;

/// <summary>
/// The operations that <see cref="SdCardManager"/> can perform on the SD card device.
/// </summary>
/// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
public enum SdCardManagerOperationIdValue
{
    GetAndClearErrorInfo = 1,
    SuspendControl = 2,
    ResumeControl = 3,
    SimulateDetectionEventSignaled = 4
}

/// <summary>
/// The operations that <see cref="SdCardDeviceOperator"/> can perform on the inserted SD card.
/// </summary>
/// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
public enum SdCardOperationIdValue
{
    GetSpeedMode = 1,
    GetCid = 2,
    GetUserAreaNumSectors = 3,
    GetUserAreaSize = 4,
    GetProtectedAreaNumSectors = 5,
    GetProtectedAreaSize = 6
}

/// <summary>
/// The operations that <see cref="MmcManager"/> can perform on the internal MMC device.
/// </summary>
/// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
public enum MmcManagerOperationIdValue
{
    GetAndClearErrorInfo = 1,
    SuspendControl = 2,
    ResumeControl = 3,
    GetAndClearPatrolReadAllocateBufferCount = 4,
    GetPatrolCount = 5,
    SuspendPatrol = 6,
    ResumePatrol = 7
}

/// <summary>
/// The operations that <see cref="MmcDeviceOperator"/> can perform on the internal MMC storage.
/// </summary>
/// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
public enum MmcOperationIdValue
{
    GetSpeedMode = 1,
    GetCid = 2,
    GetPartitionSize = 3,
    GetExtendedCsd = 4,
    Erase = 5
}