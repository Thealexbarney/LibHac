using LibHac.Ns;

namespace LibHac.Arp.Impl
{
    public interface IReader
    {
        Result GetApplicationLaunchProperty(out ApplicationLaunchProperty launchProperty, ulong processId);
        Result GetApplicationLaunchPropertyWithApplicationId(out ApplicationLaunchProperty launchProperty, ApplicationId applicationId);
        Result GetApplicationControlProperty(out ApplicationControlProperty controlProperty, ulong processId);
        Result GetApplicationControlPropertyWithApplicationId(out ApplicationControlProperty controlProperty, ApplicationId applicationId);
    }
}
