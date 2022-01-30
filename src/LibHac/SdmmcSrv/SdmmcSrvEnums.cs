namespace LibHac.SdmmcSrv;

public enum SdCardManagerOperationIdValue
{
    GetAndClearErrorInfo = 1,
    SuspendControl = 2,
    ResumeControl = 3,
    SimulateDetectionEventSignaled = 4
}

public enum SdCardOperationIdValue
{
    GetSpeedMode = 1,
    GetCid = 2,
    GetUserAreaNumSectors = 3,
    GetUserAreaSize = 4,
    GetProtectedAreaNumSectors = 5,
    GetProtectedAreaSize = 6
}

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

public enum MmcOperationIdValue
{
    GetSpeedMode = 1,
    GetCid = 2,
    GetPartitionSize = 3,
    GetExtendedCsd = 4,
    Erase = 5
}