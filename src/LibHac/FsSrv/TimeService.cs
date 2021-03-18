using System.Runtime.CompilerServices;
using LibHac.Common;
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
        private long _basePosixTime;
        private int _timeDifference;
        private SdkMutexType _mutex;

        private FileSystemServer _fsServer;

        public TimeServiceImpl(FileSystemServer fsServer)
        {
            _fsServer = fsServer;
            _basePosixTime = 0;
            _timeDifference = 0;
            _mutex.Initialize();
        }

        private long GetSystemSeconds()
        {
            OsState os = _fsServer.Hos.Os;

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
            UnsafeHelpers.SkipParamInit(out currentTime, out timeDifference);

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
            var registry = new ProgramRegistryImpl(_fsServer);
            return registry.GetProgramInfo(out programInfo, processId);
        }
    }
}
