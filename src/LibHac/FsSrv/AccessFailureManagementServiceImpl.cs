using System;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Svc;

namespace LibHac.FsSrv
{
    public class AccessFailureManagementServiceImpl
    {
        private ProgramRegistryImpl _programRegistry;
        internal HorizonClient HorizonClient { get; }
        private AccessFailureDetectionEventManager _eventManager;

        public AccessFailureManagementServiceImpl(ProgramRegistryImpl programRegistry, HorizonClient horizonClient)
        {
            _programRegistry = programRegistry;
            HorizonClient = horizonClient;
            _eventManager = new AccessFailureDetectionEventManager();
        }

        internal Result GetProgramInfo(out ProgramInfo programInfo, ulong processId)
        {
            return _programRegistry.GetProgramInfo(out programInfo, processId);
        }

        public Result CreateNotifier(out IEventNotifier notifier, ulong processId, bool notifyOnDeepRetry)
        {
            return _eventManager.CreateNotifier(out notifier, processId, notifyOnDeepRetry);
        }

        public void ResetAccessFailureDetection(ulong processId)
        {
            _eventManager.ResetAccessFailureDetection(processId);
        }

        public void DisableAccessFailureDetection(ulong processId)
        {
            _eventManager.DisableAccessFailureDetection(processId);
        }

        public void NotifyAccessFailureDetection(ulong processId)
        {
            _eventManager.NotifyAccessFailureDetection(processId);
        }

        public bool IsAccessFailureDetectionNotified(ulong processId)
        {
            return _eventManager.IsAccessFailureDetectionNotified(processId);
        }

        public Handle GetEvent()
        {
            return _eventManager.GetEvent();
        }

        public Result HandleResolubleAccessFailure(out bool wasDeferred, Result nonDeferredResult, ulong processId)
        {
            throw new NotImplementedException();
        }
    }
}
