using System;
using LibHac.Common;

namespace LibHac.Sm
{
    public class ServiceManagerClient
    {
        private ServiceManager Server { get; }

        internal ServiceManagerClient(ServiceManager server)
        {
            Server = server;
        }

        public Result GetService<T>(out T serviceObject, ReadOnlySpan<char> name)
        {
            Result rc = Server.GetService(out object service, ServiceName.Encode(name));
            if (rc.IsFailure())
            {
                UnsafeHelpers.SkipParamInit(out serviceObject);
                return rc;
            }

            if (service is T typedService)
            {
                serviceObject = typedService;
                return Result.Success;
            }

            throw new InvalidCastException("The service object is not of the specified type.");
        }

        public Result RegisterService(IServiceObject serviceObject, ReadOnlySpan<char> name)
        {
            return Server.RegisterService(serviceObject, ServiceName.Encode(name));
        }

        public Result UnregisterService(ReadOnlySpan<char> name)
        {
            return Server.UnregisterService(ServiceName.Encode(name));
        }
    }
}
