using System;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Svc;

namespace LibHac.FsSrv
{
    public class AccessFailureManagementServiceImpl
    {
        private Configuration _config;
        private AccessFailureDetectionEventManager _eventManager;

        internal HorizonClient HorizonClient => _config.FsServer.Hos;

        public AccessFailureManagementServiceImpl(in Configuration configuration)
        {
            _config = configuration;
            _eventManager = new AccessFailureDetectionEventManager();
        }

        // LibHac addition
        public struct Configuration
        {
            public FileSystemServer FsServer;
        }

        internal Result GetProgramInfo(out ProgramInfo programInfo, ulong processId)
        {
            var registry = new ProgramRegistryImpl(_config.FsServer);
            return registry.GetProgramInfo(out programInfo, processId);
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
