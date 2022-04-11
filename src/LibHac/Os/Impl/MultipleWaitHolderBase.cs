namespace LibHac.Os.Impl;

public abstract class MultiWaitHolderBase
{
    private MultiWaitImpl _multiWait;

    // LibHac addition because we can't reinterpret_cast a MultiWaitHolderBase
    // to a MultiWaitHolderType like the original does in c++
    public MultiWaitHolderType Holder { get; protected set; }

    public abstract TriBool IsSignaled();
    public abstract TriBool AddToObjectList();
    public abstract void RemoveFromObjectList();
    public abstract bool GetNativeHandle(out OsNativeHandle handle);

    public virtual TimeSpan GetAbsoluteTimeToWakeup()
    {
        return TimeSpan.FromNanoSeconds(long.MaxValue);
    }

    public void SetMultiWait(MultiWaitImpl multiWait)
    {
        _multiWait = multiWait;
    }

    public MultiWaitImpl GetMultiWait()
    {
        return _multiWait;
    }

    public bool IsLinked()
    {
        return _multiWait is not null;
    }

    public bool IsNotLinked()
    {
        return _multiWait is null;
    }
}

public abstract class MultiWaitHolderOfUserWaitObject : MultiWaitHolderBase
{
    public override bool GetNativeHandle(out OsNativeHandle handle)
    {
        handle = default;
        return false;
    }
}

public abstract class MultiWaitHolderOfNativeWaitObject : MultiWaitHolderBase
{
    public override TriBool AddToObjectList()
    {
        return TriBool.Undefined;
    }

    public override void RemoveFromObjectList() { /* ... */ }
}