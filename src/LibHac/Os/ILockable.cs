namespace LibHac.Os
{
    public interface IBasicLockable
    {
        void Lock();
        void Unlock();
    }

    public interface ILockable : IBasicLockable
    {
        bool TryLock();
    }

    public interface ISharedMutex : ILockable
    {
        void LockShared();
        bool TryLockShared();
        void UnlockShared();
    }
}
