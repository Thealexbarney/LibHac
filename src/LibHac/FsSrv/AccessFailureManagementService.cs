using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Sf;

namespace LibHac.FsSrv
{
    public readonly struct AccessFailureManagementService
    {
        private readonly AccessFailureManagementServiceImpl _serviceImpl;
        private readonly ulong _processId;

        public AccessFailureManagementService(AccessFailureManagementServiceImpl serviceImpl, ulong processId)
        {
            _serviceImpl = serviceImpl;
            _processId = processId;
        }

        internal Result GetProgramInfo(out ProgramInfo programInfo)
        {
            return _serviceImpl.GetProgramInfo(out programInfo, _processId);
        }

        public Result OpenAccessFailureDetectionEventNotifier(out ReferenceCountedDisposable<IEventNotifier> notifier,
            ulong processId, bool notifyOnDeepRetry)
        {
            UnsafeHelpers.SkipParamInit(out notifier);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.OpenAccessFailureDetectionEventNotifier))
                return ResultFs.PermissionDenied.Log();

            rc = _serviceImpl.CreateNotifier(out IEventNotifier tempNotifier, processId, notifyOnDeepRetry);
            if (rc.IsFailure()) return rc;

            notifier = new ReferenceCountedDisposable<IEventNotifier>(tempNotifier);
            return Result.Success;
        }

        public Result GetAccessFailureDetectionEvent(out NativeHandle eventHandle)
        {
            UnsafeHelpers.SkipParamInit(out eventHandle);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.GetAccessFailureDetectionEvent))
                return ResultFs.PermissionDenied.Log();

            Svc.Handle handle = _serviceImpl.GetEvent();
            eventHandle = new NativeHandle(_serviceImpl.HorizonClient.Os, handle, false);

            return Result.Success;
        }

        public Result IsAccessFailureDetected(out bool isDetected, ulong processId)
        {
            UnsafeHelpers.SkipParamInit(out isDetected);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.IsAccessFailureDetected))
                return ResultFs.PermissionDenied.Log();

            isDetected = _serviceImpl.IsAccessFailureDetectionNotified(processId);
            return Result.Success;
        }

        public Result ResolveAccessFailure(ulong processId)
        {
            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.ResolveAccessFailure))
                return ResultFs.PermissionDenied.Log();

            _serviceImpl.ResetAccessFailureDetection(processId);

            // Todo: Modify ServiceContext

            return Result.Success;
        }

        public Result AbandonAccessFailure(ulong processId)
        {
            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.AbandonAccessFailure))
                return ResultFs.PermissionDenied.Log();

            _serviceImpl.DisableAccessFailureDetection(processId);

            // Todo: Modify ServiceContext

            return Result.Success;
        }
    }
}
