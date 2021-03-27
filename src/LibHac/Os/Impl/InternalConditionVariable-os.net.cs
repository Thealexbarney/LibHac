using System.Threading;
using LibHac.Diag;

namespace LibHac.Os.Impl
{
    internal struct InternalConditionVariableImpl
    {
        private object _obj;

        public InternalConditionVariableImpl(nint _ = 0) => _obj = new object();
        public void Initialize() => _obj = new object();

        public void Signal()
        {
            Assert.SdkRequires(!Monitor.IsEntered(_obj));

            Monitor.Enter(_obj);
            Monitor.Pulse(_obj);
            Monitor.Exit(_obj);
        }

        public void Broadcast()
        {
            Assert.SdkRequires(!Monitor.IsEntered(_obj));

            Monitor.Enter(_obj);
            Monitor.PulseAll(_obj);
            Monitor.Exit(_obj);
        }

        public void Wait(ref InternalCriticalSection cs)
        {
            Assert.SdkRequires(!Monitor.IsEntered(_obj));
            Abort.DoAbortUnless(cs.IsLockedByCurrentThread());

            // Monitor.Wait doesn't allow specifying a separate mutex object. Workaround this by manually 
            // unlocking and locking the separate mutex object. Due to this, the order the waiting threads 
            // will resume is not guaranteed, and 5 Monitor calls are required instead of 1.
            cs.Leave();
            Monitor.Enter(_obj);
            Monitor.Wait(_obj);
            Monitor.Exit(_obj);
            cs.Enter();
        }

        public ConditionVariableStatus TimedWait(ref InternalCriticalSection cs, in TimeoutHelper timeoutHelper)
        {
            Assert.SdkRequires(!Monitor.IsEntered(_obj));
            Abort.DoAbortUnless(cs.IsLockedByCurrentThread());

            TimeSpan remainingTime = timeoutHelper.GetTimeLeftOnTarget();

            if (remainingTime <= new TimeSpan(0))
                return ConditionVariableStatus.TimedOut;

            // Casting to an int won't lose any data because the .NET implementation of
            // GetTimeLeftOnTarget always returns a value that fits in an int.
            int remainingTimeMs = (int)remainingTime.GetMilliSeconds();

            cs.Leave();
            Monitor.Enter(_obj);
            bool acquiredBeforeTimeout = Monitor.Wait(_obj, remainingTimeMs);
            Monitor.Exit(_obj);
            cs.Enter();

            // Short code path if we timed out even before waiting on the mutex.
            if (!acquiredBeforeTimeout)
                return ConditionVariableStatus.TimedOut;

            // We may have timed out waiting to Enter the mutex. Check the time left again.
            if (timeoutHelper.GetTimeLeftOnTarget() <= new TimeSpan(0))
                return ConditionVariableStatus.TimedOut;

            return ConditionVariableStatus.Success;
        }
    }
}
