namespace LibHac.FsSrv
{
    internal readonly struct ProgramRegistryService
    {
        private ProgramRegistryServiceImpl ServiceImpl { get; }
        private ulong ProcessId { get; }

        public ProgramRegistryService(ProgramRegistryServiceImpl serviceImpl, ulong processId)
        {
            ServiceImpl = serviceImpl;
            ProcessId = processId;
        }
    }
}
