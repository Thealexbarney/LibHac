using LibHac.Os.Impl;

namespace LibHac.Os;

public class MultiWaitType
{
    public enum State : byte
    {
        NotInitialized,
        Initialized
    }

    public State CurrentState;
    public bool IsWaiting;
    public MultiWaitImpl Impl;
}

public class MultiWaitHolderType
{
    public MultiWaitHolderImpl Impl;
    public object UserData;
}