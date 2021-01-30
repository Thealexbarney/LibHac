using System.Runtime.CompilerServices;
using LibHac.Fs;
using LibHac.FsSrv.Impl;
using LibHac.Os;

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

            _serviceImpl.SetCurrentPosixTimeWithTimeDifference(currentTime, timeDifference);
            return Result.Success;
        }

        private Result GetProgramInfo(out ProgramInfo programInfo)
        {
            return _serviceImpl.GetProgramInfo(out programInfo, _processId);
        }
    }

    public class TimeServiceImpl
    {
        private Configuration _config;
        private long _basePosixTime;
        private int _timeDifference;
        private SdkMutexType _mutex;

        public TimeServiceImpl(in Configuration configuration)
        {
            _config = configuration;
            _basePosixTime = 0;
            _timeDifference = 0;
            _mutex.Initialize();
        }

        // The entire Configuration struct is a LibHac addition to avoid using global state
        public struct Configuration
        {
            public HorizonClient HorizonClient;
            public ProgramRegistryImpl ProgramRegistry;
        }

        private long GetSystemSeconds()
        {
            OsState os = _config.HorizonClient.Os;

            Tick tick = os.GetSystemTick();
            TimeSpan timeSpan = os.ConvertToTimeSpan(tick);
            return timeSpan.GetSeconds();
        }

        public Result GetCurrentPosixTime(out long currentTime)
        {
            return GetCurrentPosixTimeWithTimeDifference(out currentTime, out int _);
        }

        public Result GetCurrentPosixTimeWithTimeDifference(out long currentTime, out int timeDifference)
        {
            Unsafe.SkipInit(out currentTime);
            Unsafe.SkipInit(out timeDifference);

            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            if (_basePosixTime == 0)
                return ResultFs.NotInitialized.Log();

            if (!Unsafe.IsNullRef(ref currentTime))
            {
                currentTime = _basePosixTime + GetSystemSeconds();
            }

            if (!Unsafe.IsNullRef(ref timeDifference))
            {
                timeDifference = _timeDifference;
            }

            return Result.Success;
        }

        public void SetCurrentPosixTimeWithTimeDifference(long currentTime, int timeDifference)
        {
            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            _basePosixTime = currentTime - GetSystemSeconds();
            _timeDifference = timeDifference;
        }

        internal Result GetProgramInfo(out ProgramInfo programInfo, ulong processId)
        {
            return _config.ProgramRegistry.GetProgramInfo(out programInfo, processId);
        }
    }
}
