namespace LibHac
{
    enum SvcName
    {
        Reserved0,
        SetHeapSize,
        SetMemoryPermission,
        SetMemoryAttribute,
        MapMemory,
        UnmapMemory,
        QueryMemory,
        ExitProcess,
        CreateThread,
        StartThread,
        ExitThread,
        SleepThread,
        GetThreadPriority,
        SetThreadPriority,
        GetThreadCoreMask,
        SetThreadCoreMask,
        GetCurrentProcessorNumber,
        SignalEvent,
        ClearEvent,
        MapSharedMemory,
        UnmapSharedMemory,
        CreateTransferMemory,
        CloseHandle,
        ResetSignal,
        WaitSynchronization,
        CancelSynchronization,
        ArbitrateLock,
        ArbitrateUnlock,
        WaitProcessWideKeyAtomic,
        SignalProcessWideKey,
        GetSystemTick,
        ConnectToNamedPort,
        SendSyncRequestLight,
        SendSyncRequest,
        SendSyncRequestWithUserBuffer,
        SendAsyncRequestWithUserBuffer,
        GetProcessId,
        GetThreadId,
        Break,
        OutputDebugString,
        ReturnFromException,
        GetInfo,
        FlushEntireDataCache,
        FlushDataCache,
        MapPhysicalMemory,
        UnmapPhysicalMemory,
        GetFutureThreadInfo,
        GetLastThreadInfo,
        GetResourceLimitLimitValue,
        GetResourceLimitCurrentValue,
        SetThreadActivity,
        GetThreadContext3,
        WaitForAddress,
        SignalToAddress,
        Reserved1,
        Reserved2,
        Reserved3,
        Reserved4,
        Reserved5,
        Reserved6,
        DumpInfo,
        DumpInfoNew,
        Reserved7,
        Reserved8,
        CreateSession,
        AcceptSession,
        ReplyAndReceiveLight,
        ReplyAndReceive,
        ReplyAndReceiveWithUserBuffer,
        CreateEvent,
        Reserved9,
        Reserved10,
        MapPhysicalMemoryUnsafe,
        UnmapPhysicalMemoryUnsafe,
        SetUnsafeLimit,
        CreateCodeMemory,
        ControlCodeMemory,
        SleepSystem,
        ReadWriteRegister,
        SetProcessActivity,
        CreateSharedMemory,
        MapTransferMemory,
        UnmapTransferMemory,
        CreateInterruptEvent,
        QueryPhysicalAddress,
        QueryIoMapping,
        CreateDeviceAddressSpace,
        AttachDeviceAddressSpace,
        DetachDeviceAddressSpace,
        MapDeviceAddressSpaceByForce,
        MapDeviceAddressSpaceAligned,
        MapDeviceAddressSpace,
        UnmapDeviceAddressSpace,
        InvalidateProcessDataCache,
        StoreProcessDataCache,
        FlushProcessDataCache,
        DebugActiveProcess,
        BreakDebugProcess,
        TerminateDebugProcess,
        GetDebugEvent,
        ContinueDebugEvent,
        GetProcessList,
        GetThreadList,
        GetDebugThreadContext,
        SetDebugThreadContext,
        QueryDebugProcessMemory,
        ReadDebugProcessMemory,
        WriteDebugProcessMemory,
        SetHardwareBreakPoint,
        GetDebugThreadParam,
        Reserved11,
        GetSystemInfo,
        CreatePort,
        ManageNamedPort,
        ConnectToPort,
        SetProcessMemoryPermission,
        MapProcessMemory,
        UnmapProcessMemory,
        QueryProcessMemory,
        MapProcessCodeMemory,
        UnmapProcessCodeMemory,
        CreateProcess,
        StartProcess,
        TerminateProcess,
        GetProcessInfo,
        CreateResourceLimit,
        SetResourceLimitLimitValue,
        CallSecureMonitor
    }
}
