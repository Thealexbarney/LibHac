using System;
using LibHac.Common;

namespace LibHac.Sm;

// This interface is being used as a stop-gap solution so we can
// have at least some sort of service system for now
public interface IServiceObject : IDisposable
{
    Result GetServiceObject(ref SharedRef<IDisposable> outServiceObject);
}