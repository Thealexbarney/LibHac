using LibHac.Common;
using LibHac.Fs;
using LibHac.Os;

namespace LibHac.FsSrv.Impl
{
    internal struct DeviceEventSimulatorGlobals
    {
        public GameCardEventSimulator GameCardEventSimulator;
        public SdCardEventSimulator SdCardEventSimulator;
        public nint GameCardEventSimulatorInit;
        public nint SdCardEventSimulatorInit;
    }

    internal static class DeviceEventSimulatorGlobalMethods
    {
        public static SdCardEventSimulator GetSdCardEventSimulator(this FileSystemServerImpl fs)
        {
            ref DeviceEventSimulatorGlobals g = ref fs.Globals.DeviceEventSimulator;
            using var guard = new InitializationGuard(ref g.SdCardEventSimulatorInit, fs.Globals.InitMutex);

            if (guard.IsInitialized)
                return g.SdCardEventSimulator;

            g.SdCardEventSimulator = new SdCardEventSimulator(fs.Hos.Os);
            return g.SdCardEventSimulator;
        }

        public static GameCardEventSimulator GetGameCardEventSimulator(this FileSystemServerImpl fs)
        {
            ref DeviceEventSimulatorGlobals g = ref fs.Globals.DeviceEventSimulator;
            using var guard = new InitializationGuard(ref g.GameCardEventSimulatorInit, fs.Globals.InitMutex);

            if (guard.IsInitialized)
                return g.GameCardEventSimulator;

            g.GameCardEventSimulator = new GameCardEventSimulator(fs.Hos.Os);
            return g.GameCardEventSimulator;
        }
    }

    // ReSharper disable once InconsistentNaming
    public abstract class IDeviceEventSimulator
    {
        private bool _isEventSet;
        private bool _isDetectionSimulationEnabled;
        private SdkRecursiveMutex _mutex;
        private SimulatingDeviceDetectionMode _detectionSimulationMode;
        private SimulatingDeviceAccessFailureEventType _simulatedFailureType;
        private SimulatingDeviceTargetOperation _simulatedOperation;
        private Result _failureResult;
        private bool _isRecurringEvent;
        private int _timeoutLengthMs;

        private OsState _os;

        public IDeviceEventSimulator(OsState os, int timeoutMs)
        {
            _os = os;
            _timeoutLengthMs = timeoutMs;
            _mutex = new SdkRecursiveMutex();
        }

        public virtual Result GetCorrespondingResult(SimulatingDeviceAccessFailureEventType eventType)
        {
            return Result.Success;
        }

        public void SetDeviceEvent(SimulatingDeviceTargetOperation operation,
            SimulatingDeviceAccessFailureEventType failureType, Result failureResult, bool isRecurringEvent)
        {
            using ScopedLock<SdkRecursiveMutex> lk = ScopedLock.Lock(ref _mutex);

            if (failureResult.IsFailure())
                _failureResult = failureResult;

            _isEventSet = true;
            _simulatedFailureType = failureType;
            _simulatedOperation = operation;
            _isRecurringEvent = isRecurringEvent;
        }

        public void ClearDeviceEvent()
        {
            using ScopedLock<SdkRecursiveMutex> lk = ScopedLock.Lock(ref _mutex);

            _isEventSet = false;
            _simulatedFailureType = SimulatingDeviceAccessFailureEventType.None;
            _simulatedOperation = SimulatingDeviceTargetOperation.None;
            _failureResult = Result.Success;
            _isRecurringEvent = false;
        }

        public void SetDetectionSimulationMode(SimulatingDeviceDetectionMode mode)
        {
            using ScopedLock<SdkRecursiveMutex> lk = ScopedLock.Lock(ref _mutex);

            _isDetectionSimulationEnabled = mode != SimulatingDeviceDetectionMode.NoSimulation;
            _detectionSimulationMode = mode;
        }

        public void ClearDetectionSimulationMode()
        {
            SetDetectionSimulationMode(SimulatingDeviceDetectionMode.NoSimulation);
        }

        public Result CheckSimulatedAccessFailureEvent(SimulatingDeviceTargetOperation operation)
        {
            if (_isEventSet)
                return Result.Success;

            using ScopedLock<SdkRecursiveMutex> lk = ScopedLock.Lock(ref _mutex);

            if ((_simulatedOperation & operation) == 0)
                return Result.Success;

            Result result = GetCorrespondingResult(_simulatedFailureType);

            if (result.IsFailure() && _failureResult.IsFailure())
                result = _failureResult;

            if (_simulatedFailureType == SimulatingDeviceAccessFailureEventType.AccessTimeoutFailure)
                SimulateTimeout();

            if (!_isRecurringEvent)
                ClearDeviceEvent();

            return result;
        }

        public bool FilterDetectionState(bool actualState)
        {
            if (!_isDetectionSimulationEnabled)
                return actualState;

            bool simulatedState = _detectionSimulationMode switch
            {
                SimulatingDeviceDetectionMode.NoSimulation => actualState,
                SimulatingDeviceDetectionMode.DeviceAttached => true,
                SimulatingDeviceDetectionMode.DeviceRemoved => false,
                _ => actualState
            };

            return simulatedState;
        }

        protected virtual void SimulateTimeout()
        {
            _os.SleepThread(TimeSpan.FromMilliSeconds(_timeoutLengthMs));
        }
    }

    public class GameCardEventSimulator : IDeviceEventSimulator
    {
        public GameCardEventSimulator(OsState os) : base(os, 2000) { }

        public override Result GetCorrespondingResult(SimulatingDeviceAccessFailureEventType eventType)
        {
            return eventType switch
            {
                SimulatingDeviceAccessFailureEventType.None => Result.Success,
                SimulatingDeviceAccessFailureEventType.AccessTimeoutFailure => ResultFs.GameCardCardAccessTimeout.Log(),
                SimulatingDeviceAccessFailureEventType.AccessFailure => ResultFs.GameCardAccessFailed.Log(),
                SimulatingDeviceAccessFailureEventType.DataCorruption => ResultFs.SimulatedDeviceDataCorrupted.Log(),
                _ => ResultFs.InvalidArgument.Log()
            };
        }
    }

    public class SdCardEventSimulator : IDeviceEventSimulator
    {
        public SdCardEventSimulator(OsState os) : base(os, 2000) { }

        public override Result GetCorrespondingResult(SimulatingDeviceAccessFailureEventType eventType)
        {
            return eventType switch
            {
                SimulatingDeviceAccessFailureEventType.None => Result.Success,
                SimulatingDeviceAccessFailureEventType.AccessTimeoutFailure => ResultFs.PortSdCardResponseTimeoutError.Log(),
                SimulatingDeviceAccessFailureEventType.AccessFailure => ResultFs.SdCardAccessFailed.Log(),
                SimulatingDeviceAccessFailureEventType.DataCorruption => ResultFs.SimulatedDeviceDataCorrupted.Log(),
                _ => ResultFs.InvalidArgument.Log()
            };
        }
    }
}