using System.Threading;

namespace LibHac.Os.Impl
{
    internal struct InternalCriticalSectionImpl
    {
        private object _obj;

        public void Initialize()
        {
            _obj = new object();
        }

        public void FinalizeObject() { }

        public void Enter()
        {
            Monitor.Enter(_obj);
        }

        public bool TryEnter()
        {
            return Monitor.TryEnter(_obj);
        }

        public void Leave()
        {
            Monitor.Exit(_obj);
        }

        public bool IsLockedByCurrentThread()
        {
            return Monitor.IsEntered(_obj);
        }
    }
}
