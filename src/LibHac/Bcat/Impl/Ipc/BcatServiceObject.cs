using LibHac.Sm;

namespace LibHac.Bcat.Impl.Ipc
{
    internal class BcatServiceObject : IServiceObject
    {
        private IServiceCreator _serviceCreator;

        public BcatServiceObject(IServiceCreator serviceCreator)
        {
            _serviceCreator = serviceCreator;
        }

        public Result GetServiceObject(out object serviceObject)
        {
            serviceObject = _serviceCreator;
            return Result.Success;
        }
    }
}
