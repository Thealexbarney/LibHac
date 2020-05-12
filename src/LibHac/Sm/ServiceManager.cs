using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Sf;

namespace LibHac.Sm
{
    // This is basically a makeshift service manager that doesn't do anything
    // other than keep service objects for now. It's just here so other stuff
    // isn't blocked waiting for something better.
    internal class ServiceManager
    {
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        private Horizon Horizon { get; }
        private Dictionary<ServiceName, object> Services { get; } = new Dictionary<ServiceName, object>();

        public ServiceManager(Horizon horizon)
        {
            Horizon = horizon;
        }

        internal Result GetService(out object serviceObject, ServiceName serviceName)
        {
            serviceObject = default;

            Result rc = ValidateServiceName(serviceName);
            if (rc.IsFailure()) return rc;

            if (!Services.TryGetValue(serviceName, out serviceObject))
            {
                return ResultSf.RequestDeferredByUser.Log();
            }

            return Result.Success;
        }

        internal Result RegisterService(object serviceObject, ServiceName serviceName)
        {
            Result rc = ValidateServiceName(serviceName);
            if (rc.IsFailure()) return rc;

            if (!Services.TryAdd(serviceName, serviceObject))
            {
                return ResultSm.AlreadyRegistered.Log();
            }

            return Result.Success;
        }

        internal Result UnregisterService(ServiceName serviceName)
        {
            Result rc = ValidateServiceName(serviceName);
            if (rc.IsFailure()) return rc;

            if (!Services.Remove(serviceName, out object service))
            {
                return ResultSm.NotRegistered.Log();
            }

            if (service is IDisposable disposable)
            {
                disposable.Dispose();
            }

            return Result.Success;
        }

        private Result ValidateServiceName(ServiceName name)
        {
            // Service names must be non-empty.
            if (name.Name == 0)
                return ResultSm.InvalidServiceName.Log();

            // Get name length.
            int nameLen;
            for (nameLen = 1; nameLen < Unsafe.SizeOf<ServiceName>(); nameLen++)
            {
                if (SpanHelpers.AsReadOnlyByteSpan(ref name)[nameLen] == 0)
                {
                    break;
                }
            }

            // Names must be all-zero after they end.
            for (; nameLen < Unsafe.SizeOf<ServiceName>(); nameLen++)
            {
                if (SpanHelpers.AsReadOnlyByteSpan(ref name)[nameLen] != 0)
                {
                    return ResultSm.InvalidServiceName.Log();
                }
            }

            return Result.Success;
        }
    }
}
