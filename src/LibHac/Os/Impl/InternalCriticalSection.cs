namespace LibHac.Os.Impl
{
    public struct InternalCriticalSection : ILockable
    {
        private InternalCriticalSectionImpl _impl;

        public void Initialize() => _impl.Initialize();
        public void FinalizeObject() => _impl.FinalizeObject();

        public void Enter() => _impl.Enter();
        public bool TryEnter() => _impl.TryEnter();
        public void Leave() => _impl.Leave();

        public void Lock() => _impl.Enter();
        public bool TryLock() => _impl.TryEnter();
        public void Unlock() => _impl.Leave();

        public bool IsLockedByCurrentThread() => _impl.IsLockedByCurrentThread();
    }
}
