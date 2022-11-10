using System;
using LibHac.Common;

namespace LibHac.Sm;

public class ServiceManagerClient
{
    private ServiceManager Server { get; }

    internal ServiceManagerClient(ServiceManager server)
    {
        Server = server;
    }

    public Result GetService<T>(ref SharedRef<T> serviceObject, ReadOnlySpan<char> name) where T : class, IDisposable
    {
        using var service = new SharedRef<IDisposable>();

        Result res = Server.GetService(ref service.Ref(), ServiceName.Encode(name));
        if (res.IsFailure()) return res.Miss();

        if (serviceObject.TryCastSet(ref service.Ref()))
        {
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
