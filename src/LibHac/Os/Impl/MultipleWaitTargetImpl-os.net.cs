using System;
using System.Threading;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;

namespace LibHac.Os.Impl;

public class MultiWaitTargetImpl : IDisposable
{
    private EventWaitHandle _cancelEvent;

    // LibHac addition
    private OsState _os;

    public MultiWaitTargetImpl(OsState os)
    {
        _cancelEvent = new EventWaitHandle(false, EventResetMode.AutoReset);
        _os = os;
    }

    public void Dispose()
    {
        _cancelEvent.Dispose();
    }

    public void CancelWait()
    {
        _cancelEvent.Set();
    }

    public Result WaitAny(out int index, Span<WaitHandle> handles, int num)
    {
        return WaitForAnyObjects(out index, num, handles, int.MaxValue);
    }

    public Result TimedWaitAny(out int outIndex, Span<WaitHandle> handles, int num, TimeSpan timeout)
    {
        UnsafeHelpers.SkipParamInit(out outIndex);

        var timeoutHelper = new TimeoutHelper(_os, timeout);

        do
        {
            Result rc = WaitForAnyObjects(out int index, num, handles,
                (int)timeoutHelper.GetTimeLeftOnTarget().GetMilliSeconds());
            if (rc.IsFailure()) return rc.Miss();

            if (index == MultiWaitImpl.WaitTimedOut)
            {
                outIndex = index;
                return Result.Success;
            }
        } while (!timeoutHelper.TimedOut());

        outIndex = MultiWaitImpl.WaitTimedOut;
        return Result.Success;
    }

    public Result ReplyAndReceive(out int index, Span<WaitHandle> handles, int num, WaitHandle replyTarget)
    {
        return ReplyAndReceiveImpl(out index, handles, num, replyTarget, TimeSpan.FromNanoSeconds(long.MaxValue));
    }

    public Result TimedReplyAndReceive(out int index, Span<WaitHandle> handles, int num, WaitHandle replyTarget,
        TimeSpan timeout)
    {
        return ReplyAndReceiveImpl(out index, handles, num, replyTarget, timeout);
    }

    public void SetCurrentThreadHandleForCancelWait()
    {
        /* ... */
    }

    public void ClearCurrentThreadHandleForCancelWait()
    {
        /* ... */
    }

    private Result WaitForAnyObjects(out int outIndex, int num, Span<WaitHandle> handles, int timeoutMs)
    {
        // Check that we can add our cancel handle to the wait.
        Abort.DoAbortUnless(num + 1 < handles.Length);

        handles[num] = _cancelEvent;
        int index = WaitHandle.WaitAny(handles.Slice(0, num + 1).ToArray(), timeoutMs);

        if (index == WaitHandle.WaitTimeout)
        {
            outIndex = MultiWaitImpl.WaitTimedOut;
            return Result.Success;
        }

        Assert.SdkAssert(index >= 0 && index <= num);

        if (index == num)
        {
            outIndex = MultiWaitImpl.WaitCancelled;
            return Result.Success;
        }

        outIndex = index;
        return Result.Success;
    }

    private Result ReplyAndReceiveImpl(out int outIndex, Span<WaitHandle> handles, int num, WaitHandle replyTarget,
        TimeSpan timeout)
    {
        UnsafeHelpers.SkipParamInit(out outIndex);

        Abort.DoAbortUnlessSuccess(ResultFs.NotImplemented.Value);

        return ResultFs.NotImplemented.Log();
    }
}