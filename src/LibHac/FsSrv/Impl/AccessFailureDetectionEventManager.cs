using System;
using LibHac.FsSrv.Sf;
using LibHac.Svc;

namespace LibHac.FsSrv.Impl
{
    public class AccessFailureDetectionEventManager
    {
        public Result CreateNotifier(out IEventNotifier notifier, ulong processId, bool notifyOnDeepRetry)
        {
            throw new NotImplementedException();
        }

        public void NotifyAccessFailureDetection(ulong processId)
        {
            throw new NotImplementedException();
        }

        public void ResetAccessFailureDetection(ulong processId)
        {
            throw new NotImplementedException();
        }

        public void DisableAccessFailureDetection(ulong processId)
        {
            throw new NotImplementedException();
        }

        public bool IsAccessFailureDetectionNotified(ulong processId)
        {
            throw new NotImplementedException();
        }

        public Handle GetEvent()
        {
            throw new NotImplementedException();
        }
    }
}
