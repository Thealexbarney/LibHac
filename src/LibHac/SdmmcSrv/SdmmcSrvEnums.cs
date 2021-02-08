namespace LibHac.SdmmcSrv
{
    public enum SdCardManagerOperationIdValue
    {
        GetAndClearErrorInfo = 1,
        SuspendControl = 2,
        ResumeControl = 3,
        SimulateDetectionEventSignaled = 4
    }

    public enum SdCardOperationIdValue
    {
        GetSpeedMode = 0x1,
        GetCid = 0x2,
        GetUserAreaNumSectors = 0x3,
        GetUserAreaSize = 0x4,
        GetProtectedAreaNumSectors = 0x5,
        GetProtectedAreaSize = 0x6
    }

    public enum MmcManagerOperationIdValue
    {
        GetAndClearErrorInfo = 0x1,
        SuspendControl = 0x2,
        ResumeControl = 0x3,
        GetAndClearPatrolReadAllocateBufferCount = 0x4,
        GetPatrolCount = 0x5,
        SuspendPatrol = 0x6,
        ResumePatrol = 0x7
    }

    public enum MmcOperationIdValue
    {
        GetSpeedMode = 1,
        GetCid = 2,
        GetPartitionSize = 3,
        GetExtendedCsd = 4,
        Erase = 5
    }
}
