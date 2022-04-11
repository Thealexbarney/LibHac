using LibHac.Diag;
using LibHac.Os.Impl;

namespace LibHac.Os;

// Todo: Handling waiting in .NET in an OS-agnostic way requires using WaitHandles. 
// I'm not sure if this might cause issues in the future if we need to wait on objects other than
// those supported by WaitHandle.
public static class MultipleWait
{
    private static MultiWaitImpl GetMultiWaitImpl(MultiWaitType multiWait)
    {
        return multiWait.Impl;
    }

    private static MultiWaitHolderType CastToMultiWaitHolder(MultiWaitHolderBase holderBase)
    {
        return holderBase.Holder;
    }

    // Note: The "IsWaiting" field is only used in develop builds
    public static void InitializeMultiWait(this OsState os, MultiWaitType multiWait)
    {
        multiWait.Impl = new MultiWaitImpl(os, multiWait);

        multiWait.IsWaiting = false;
        MemoryFenceApi.FenceMemoryStoreStore();

        multiWait.CurrentState = MultiWaitType.State.Initialized;
    }

    public static void FinalizeMultiWait(this OsState os, MultiWaitType multiWait)
    {
        MultiWaitImpl impl = GetMultiWaitImpl(multiWait);

        Assert.SdkRequires(multiWait.CurrentState == MultiWaitType.State.Initialized);
        Assert.SdkRequires(impl.IsListEmpty());

        multiWait.CurrentState = MultiWaitType.State.NotInitialized;
        impl.Dispose();
    }

    public static MultiWaitHolderType WaitAny(this OsState os, MultiWaitType multiWait)
    {
        MultiWaitImpl impl = GetMultiWaitImpl(multiWait);

        Assert.SdkRequires(multiWait.CurrentState == MultiWaitType.State.Initialized);
        Assert.SdkRequires(impl.IsListNotEmpty());

        multiWait.IsWaiting = true;
        MemoryFenceApi.FenceMemoryStoreAny();

        MultiWaitHolderType holder = CastToMultiWaitHolder(impl.WaitAny());

        MemoryFenceApi.FenceMemoryAnyStore();
        multiWait.IsWaiting = false;

        Assert.SdkAssert(holder is not null);
        return holder;
    }

    public static MultiWaitHolderType TryWaitAny(this OsState os, MultiWaitType multiWait)
    {
        MultiWaitImpl impl = GetMultiWaitImpl(multiWait);

        Assert.SdkRequires(multiWait.CurrentState == MultiWaitType.State.Initialized);
        Assert.SdkRequires(impl.IsListNotEmpty());

        multiWait.IsWaiting = true;
        MemoryFenceApi.FenceMemoryStoreAny();

        MultiWaitHolderType holder = CastToMultiWaitHolder(impl.TryWaitAny());

        MemoryFenceApi.FenceMemoryAnyStore();
        multiWait.IsWaiting = false;

        return holder;
    }

    public static MultiWaitHolderType TimedWaitAny(this OsState os, MultiWaitType multiWait, TimeSpan timeout)
    {
        MultiWaitImpl impl = GetMultiWaitImpl(multiWait);

        Assert.SdkRequires(multiWait.CurrentState == MultiWaitType.State.Initialized);
        Assert.SdkRequires(impl.IsListNotEmpty());
        Assert.SdkRequires(timeout.GetNanoSeconds() >= 0);

        multiWait.IsWaiting = true;
        MemoryFenceApi.FenceMemoryStoreAny();

        MultiWaitHolderType holder = CastToMultiWaitHolder(impl.TimedWaitAny(timeout));

        MemoryFenceApi.FenceMemoryAnyStore();
        multiWait.IsWaiting = false;

        return holder;
    }

    public static void FinalizeMultiWaitHolder(this OsState os, MultiWaitHolderType holder)
    {
        MultiWaitHolderBase holderBase = holder.Impl.HolderBase;
        Assert.SdkRequires(holderBase.IsNotLinked());
    }

    public static void LinkMultiWaitHolder(this OsState os, MultiWaitType multiWait, MultiWaitHolderType holder)
    {
        MultiWaitImpl impl = GetMultiWaitImpl(multiWait);
        MultiWaitHolderBase holderBase = holder.Impl.HolderBase;

        Assert.SdkRequires(multiWait.CurrentState == MultiWaitType.State.Initialized);
        Assert.SdkRequires(holderBase.IsNotLinked());

        Assert.SdkEqual(false, multiWait.IsWaiting);
        MemoryFenceApi.FenceMemoryLoadAny();

        impl.PushBackToList(holderBase);
        holderBase.SetMultiWait(impl);
    }

    public static void UnlinkMultiWaitHolder(this OsState os, MultiWaitHolderType holder)
    {
        MultiWaitHolderBase holderBase = holder.Impl.HolderBase;

        Assert.SdkRequires(holderBase.IsLinked());

        Assert.SdkEqual(false, holderBase.GetMultiWait().GetMultiWaitType().IsWaiting);
        MemoryFenceApi.FenceMemoryLoadAny();

        holderBase.GetMultiWait().EraseFromList(holderBase);
        holderBase.SetMultiWait(null);
    }

    public static void UnlinkAllMultiWaitHolder(this OsState os, MultiWaitType multiWait)
    {
        MultiWaitImpl impl = GetMultiWaitImpl(multiWait);

        Assert.SdkRequires(multiWait.CurrentState == MultiWaitType.State.Initialized);

        Assert.SdkEqual(false, multiWait.IsWaiting);
        MemoryFenceApi.FenceMemoryLoadAny();

        impl.EraseAllFromList();
    }

    public static void MoveAllMultiWaitHolder(this OsState os, MultiWaitType dest, MultiWaitType source)
    {
        MultiWaitImpl dstImpl = GetMultiWaitImpl(dest);
        MultiWaitImpl srcImpl = GetMultiWaitImpl(source);

        Assert.SdkRequires(dest.CurrentState == MultiWaitType.State.Initialized);
        Assert.SdkRequires(source.CurrentState == MultiWaitType.State.Initialized);

        Assert.SdkEqual(false, dest.IsWaiting);
        MemoryFenceApi.FenceMemoryLoadAny();

        Assert.SdkEqual(false, source.IsWaiting);
        MemoryFenceApi.FenceMemoryLoadAny();

        dstImpl.MoveAllFromOther(srcImpl);
    }

    public static void SetMultiWaitHolderUserData(this OsState os, MultiWaitHolderType holder, object userData)
    {
        holder.UserData = userData;
    }

    public static object GetMultiWaitHolderUserData(this OsState os, MultiWaitHolderType holder)
    {
        return holder.UserData;
    }

    public static void InitializeMultiWaitHolder(this OsState os, MultiWaitHolderType holder, OsNativeHandle handle)
    {
        Assert.SdkRequires(handle != OsTypes.InvalidNativeHandle);

        holder.Impl = new MultiWaitHolderImpl(new MultiWaitHolderOfNativeHandle(handle));
        holder.UserData = null;
    }
}