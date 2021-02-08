using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace LibHac.Common
{
    internal readonly ref struct InitializationGuard
    {
        private readonly Ref<nint> _guard;
        private readonly object _mutex;
        public bool IsInitialized => _mutex == null;

        private const byte GuardBitComplete = 1 << 0;

        [Flags]
        private enum InitStatus : byte
        {
            Complete = 1 << 0,
            Pending = 1 << 1,
            Waiting = 1 << 2
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InitializationGuard(ref nint guard, object mutex)
        {
            if (IsGuardInitialized(guard) || !AcquireGuard(ref guard, mutex))
            {
                this = default;
                return;
            }

            _guard = new Ref<nint>(ref guard);
            _mutex = mutex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (!IsInitialized)
            {
                ReleaseGuard(ref _guard.Value, _mutex);
            }
        }

        public static bool AcquireGuard(ref nint guard, object mutex)
        {
            if (SpanHelpers.AsByteSpan(ref guard)[0] == GuardBitComplete)
                return false;

            return AcquireInitByte(ref Unsafe.As<byte, InitStatus>(ref SpanHelpers.AsByteSpan(ref guard)[1]), mutex);
        }

        public static void ReleaseGuard(ref nint guard, object mutex)
        {
            SpanHelpers.AsByteSpan(ref guard)[0] = GuardBitComplete;

            ReleaseInitByte(ref Unsafe.As<byte, InitStatus>(ref SpanHelpers.AsByteSpan(ref guard)[1]), mutex);
        }

        private static bool AcquireInitByte(ref InitStatus initByte, object mutex)
        {
            lock (mutex)
            {
                while (initByte.HasFlag(InitStatus.Pending))
                {
                    initByte |= InitStatus.Waiting;
                    Monitor.Wait(mutex);
                }

                if (initByte == InitStatus.Complete)
                    return false;

                initByte = InitStatus.Pending;
                return true;
            }
        }

        private static void ReleaseInitByte(ref InitStatus initByte, object mutex)
        {
            lock (mutex)
            {
                bool hasWaiting = initByte.HasFlag(InitStatus.Waiting);
                initByte = InitStatus.Complete;

                if (hasWaiting)
                    Monitor.PulseAll(mutex);
            }
        }

        private static bool IsGuardInitialized(nint guard)
        {
            return (guard & 1) != 0;
        }
    }
}
