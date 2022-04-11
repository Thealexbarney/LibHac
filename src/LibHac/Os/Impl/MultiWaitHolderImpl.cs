namespace LibHac.Os.Impl;

public class MultiWaitHolderImpl
{
    private MultiWaitHolderBase _holder;

    public MultiWaitHolderBase HolderBase => _holder;
    public MultiWaitHolderOfNativeHandle HolderOfNativeHandle => (MultiWaitHolderOfNativeHandle)_holder;
    public MultiWaitHolderOfSemaphore HolderOfSemaphore => (MultiWaitHolderOfSemaphore)_holder;

    public MultiWaitHolderImpl(MultiWaitHolderOfNativeHandle holder) => _holder = holder;
    public MultiWaitHolderImpl(MultiWaitHolderOfSemaphore holder) => _holder = holder;
}