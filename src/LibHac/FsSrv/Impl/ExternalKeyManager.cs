using System;
using LibHac.Fs;
using LibHac.Os;
using LibHac.Spl;

namespace LibHac.FsSrv.Impl;

public class ExternalKeyManager
{
    private SdkMutexType _mutex;

    public ExternalKeyManager()
    {
        _mutex = new SdkMutexType();
    }

    public Result Register(in RightsId rightsId, in AccessKey accessKey)
    {
        throw new NotImplementedException();
    }

    public Result Unregister(in RightsId rightsId)
    {
        throw new NotImplementedException();
    }

    public Result UnregisterAll()
    {
        throw new NotImplementedException();
    }

    public bool IsAvailableKeySource(ReadOnlySpan<byte> keySource)
    {
        throw new NotImplementedException();
    }

    public Result Find(out AccessKey outAccessKey, in RightsId rightsId)
    {
        throw new NotImplementedException();
    }

    private Result FindCore(out AccessKey outAccessKey, in RightsId rightsId)
    {
        throw new NotImplementedException();
    }
}