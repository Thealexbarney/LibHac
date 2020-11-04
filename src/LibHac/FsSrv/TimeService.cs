using System;
using LibHac.Fs;
using LibHac.FsSrv.Impl;

namespace LibHac.FsSrv
{
    public readonly struct TimeService
    {
        private readonly TimeServiceImpl _serviceImpl;
        private readonly ulong _processId;

        public TimeService(TimeServiceImpl serviceImpl, ulong processId)
        {
            _serviceImpl = serviceImpl;
            _processId = processId;
        }

        public Result SetCurrentPosixTimeWithTimeDifference(long currentTime, int timeDifference)
        {
            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.SetCurrentPosixTime))
                return ResultFs.PermissionDenied.Log();

            return _serviceImpl.SetCurrentPosixTimeWithTimeDifference(currentTime, timeDifference);
        }

        private Result GetProgramInfo(out ProgramInfo programInfo)
        {
            return _serviceImpl.GetProgramInfo(out programInfo, _processId);
        }
    }

    public class TimeServiceImpl
    {
        private Configuration _config;
        private long _baseTime;
        private int _timeDifference;
        private object _lockObject;

        public TimeServiceImpl(in Configuration configuration)
        {
            _config = configuration;
            _baseTime = 0;
            _timeDifference = 0;
            _lockObject = new object();
        }

        // The entire Configuration struct is a LibHac addition to avoid using global state
        public struct Configuration
        {
            public HorizonClient HorizonClient;
            public ProgramRegistryImpl ProgramRegistry;
        }

        public Result GetCurrentPosixTime(out long time)
        {
            throw new NotImplementedException();
        }

        public Result GetCurrentPosixTimeWithTimeDifference(out long currentTime, out int timeDifference)
        {
            throw new NotImplementedException();
        }

        public Result SetCurrentPosixTimeWithTimeDifference(long currentTime, int timeDifference)
        {
            throw new NotImplementedException();
        }

        internal Result GetProgramInfo(out ProgramInfo programInfo, ulong processId)
        {
            return _config.ProgramRegistry.GetProgramInfo(out programInfo, processId);
        }
    }
}
