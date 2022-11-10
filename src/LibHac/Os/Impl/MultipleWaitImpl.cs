using System;
using System.Collections.Generic;
using LibHac.Diag;

namespace LibHac.Os.Impl;

public class MultiWaitImpl : IDisposable
{
    public const int MaximumHandleCount = 64;
    public const int WaitInvalid = -3;
    public const int WaitCancelled = -2;
    public const int WaitTimedOut = -1;

    private LinkedList<MultiWaitHolderBase> _multiWaitList;
    private MultiWaitHolderBase _signaledHolder;
    private TimeSpan _currentTime;
    private InternalCriticalSection _csWait;
    private MultiWaitTargetImpl _targetImpl;

    // LibHac additions
    private OsState _os;
    private MultiWaitType _parent;
    public MultiWaitType GetMultiWaitType() => _parent;

    public MultiWaitImpl(OsState os, MultiWaitType parent)
    {
        _multiWaitList = new LinkedList<MultiWaitHolderBase>();
        _currentTime = new TimeSpan(0);
        _csWait = new InternalCriticalSection();
        _targetImpl = new MultiWaitTargetImpl(os);

        _os = os;
        _parent = parent;
    }

    public void Dispose()
    {
        _csWait.Dispose();
        _targetImpl.Dispose();
    }

    public MultiWaitHolderBase WaitAny()
    {
        return WaitAnyImpl(infinite: true, TimeSpan.FromNanoSeconds(long.MaxValue));
    }

    public MultiWaitHolderBase TryWaitAny()
    {
        return WaitAnyImpl(infinite: false, new TimeSpan(0));
    }

    public MultiWaitHolderBase TimedWaitAny(TimeSpan timeout)
    {
        return WaitAnyImpl(infinite: false, timeout);
    }

    public Result ReplyAndReceive(out MultiWaitHolderBase outHolder, OsNativeHandle replyTarget)
    {
        return WaitAnyImpl(out outHolder, infinite: true, TimeSpan.FromNanoSeconds(long.MaxValue), reply: true,
            replyTarget);
    }

    public bool IsListEmpty()
    {
        return _multiWaitList.Count == 0;
    }

    public bool IsListNotEmpty()
    {
        return _multiWaitList.Count != 0;
    }

    public void PushBackToList(MultiWaitHolderBase holder)
    {
        _multiWaitList.AddLast(holder);
    }

    public void EraseFromList(MultiWaitHolderBase holder)
    {
        bool wasInList = _multiWaitList.Remove(holder);
        Assert.SdkAssert(wasInList);
    }

    public void EraseAllFromList()
    {
        _multiWaitList.Clear();
    }

    public void MoveAllFromOther(MultiWaitImpl other)
    {
        // Set ourselves as multi wait for all of the other's holders.
        foreach (MultiWaitHolderBase holder in other._multiWaitList)
        {
            holder.SetMultiWait(this);
        }

        LinkedListNode<MultiWaitHolderBase> node = other._multiWaitList.First;

        while (node is not null)
        {
            other._multiWaitList.Remove(node);
            _multiWaitList.AddLast(node);

            node = other._multiWaitList.First;
        }
    }

    public TimeSpan GetCurrentTime()
    {
        return _currentTime;
    }

    public void NotifyAndWakeupThread(MultiWaitHolderBase holder)
    {
        using ScopedLock<InternalCriticalSection> lk = ScopedLock.Lock(ref _csWait);

        if (_signaledHolder is null)
        {
            _signaledHolder = holder;
            _targetImpl.CancelWait();
        }
    }

    private MultiWaitHolderBase WaitAnyImpl(bool infinite, TimeSpan timeout)
    {
        Result waitResult = WaitAnyImpl(out MultiWaitHolderBase holder, infinite, timeout, false, OsTypes.InvalidNativeHandle);

        Assert.SdkAssert(waitResult.IsSuccess());

        return holder;
    }

    private Result WaitAnyImpl(out MultiWaitHolderBase outHolder, bool infinite, TimeSpan timeout, bool reply,
        OsNativeHandle replyTarget)
    {
        // Prepare for processing.
        _signaledHolder = null;
        _targetImpl.SetCurrentThreadHandleForCancelWait();
        MultiWaitHolderBase holder = AddToEachObjectListAndCheckObjectState();

        // Check if we've been signaled.
        using (ScopedLock.Lock(ref _csWait))
        {
            if (_signaledHolder is not null)
                holder = _signaledHolder;
        }

        // Process object array.
        Result waitResult = Result.Success;
        if (holder is null)
        {
            waitResult = InternalWaitAnyImpl(out holder, infinite, timeout, reply, replyTarget);
        }
        else if (reply && replyTarget != OsTypes.InvalidNativeHandle)
        {
            waitResult = _targetImpl.TimedReplyAndReceive(out int _, null, num: 0, replyTarget,
                new TimeSpan(0));

            if (waitResult.IsFailure())
                holder = null;
        }

        // Unlink holders from the current object list.
        RemoveFromEachObjectList();

        _targetImpl.ClearCurrentThreadHandleForCancelWait();

        outHolder = holder;
        return waitResult;
    }

    private Result InternalWaitAnyImpl(out MultiWaitHolderBase outHolder, bool infinite, TimeSpan timeout, bool reply,
        OsNativeHandle replyTarget)
    {
        var objectsArray = new OsNativeHandle[MaximumHandleCount];
        var objectsArrayToHolder = new MultiWaitHolderBase[MaximumHandleCount];

        int objectCount = ConstructObjectsArray(objectsArray, objectsArrayToHolder, MaximumHandleCount);

        TimeSpan absoluteEndTime = infinite
            ? TimeSpan.FromNanoSeconds(long.MaxValue)
            : _os.GetCurrentTick().ToTimeSpan(_os) + timeout;

        while (true)
        {
            _currentTime = _os.GetCurrentTick().ToTimeSpan(_os);

            MultiWaitHolderBase minTimeoutObject = RecalcMultiWaitTimeout(out TimeSpan timeoutMin, absoluteEndTime);

            int index;
            Result waitResult = Result.Success;

            if (reply)
            {
                if (infinite && minTimeoutObject is null)
                {
                    waitResult = _targetImpl.ReplyAndReceive(out index, objectsArray, objectCount, replyTarget);
                }
                else
                {
                    waitResult = _targetImpl.TimedReplyAndReceive(out index, objectsArray, objectCount, replyTarget, timeoutMin);
                }
            }
            else
            {
                if (infinite && minTimeoutObject is null)
                {
                    waitResult = _targetImpl.WaitAny(out index, objectsArray, objectCount);
                }
                else
                {
                    if (objectCount == 0 && timeoutMin == new TimeSpan(0))
                    {
                        index = WaitTimedOut;
                    }
                    else
                    {
                        waitResult = _targetImpl.TimedWaitAny(out index, objectsArray, objectCount, timeoutMin);
                    }
                }
            }

            switch (index)
            {
                case WaitTimedOut:
                    if (minTimeoutObject is not null)
                    {
                        _currentTime = _os.GetCurrentTick().ToTimeSpan(_os);

                        if (minTimeoutObject.IsSignaled() == TriBool.True)
                        {
                            using ScopedLock<InternalCriticalSection> lk = ScopedLock.Lock(ref _csWait);

                            _signaledHolder = minTimeoutObject;
                            outHolder = minTimeoutObject;
                            return waitResult;
                        }
                    }
                    else
                    {
                        outHolder = null;
                        return waitResult;
                    }

                    break;
                case WaitCancelled:
                {
                    using ScopedLock<InternalCriticalSection> lk = ScopedLock.Lock(ref _csWait);

                    if (_signaledHolder is not null)
                    {
                        outHolder = _signaledHolder;
                        return waitResult;
                    }

                    break;
                }
                case WaitInvalid:
                    outHolder = null;
                    return waitResult;
                default:
                {
                    Assert.SdkAssert(index >= 0 && index < objectCount);

                    using ScopedLock<InternalCriticalSection> lk = ScopedLock.Lock(ref _csWait);

                    _signaledHolder = objectsArrayToHolder[index];
                    outHolder = _signaledHolder;
                    return waitResult;
                }
            }

            replyTarget = OsTypes.InvalidNativeHandle;
        }
    }

    public int ConstructObjectsArray(Span<OsNativeHandle> outHandles, Span<MultiWaitHolderBase> outObjects, int num)
    {
        Assert.SdkRequiresGreaterEqual(outHandles.Length, num);
        Assert.SdkRequiresGreaterEqual(outObjects.Length, num);

        int count = 0;

        foreach (MultiWaitHolderBase holderBase in _multiWaitList)
        {
            if (holderBase.GetNativeHandle(out OsNativeHandle handle))
            {
                Abort.DoAbortUnless(count < num);

                outHandles[count] = handle;
                outObjects[count] = holderBase;
                count++;
            }
        }

        return count;
    }

    private MultiWaitHolderBase AddToEachObjectListAndCheckObjectState()
    {
        MultiWaitHolderBase signaledHolder = null;

        foreach (MultiWaitHolderBase holderBase in _multiWaitList)
        {
            TriBool isSignaled = holderBase.AddToObjectList();

            if (signaledHolder is null && isSignaled == TriBool.True)
            {
                signaledHolder = holderBase;
            }
        }

        return signaledHolder;
    }

    private void RemoveFromEachObjectList()
    {
        foreach (MultiWaitHolderBase holderBase in _multiWaitList)
        {
            holderBase.RemoveFromObjectList();
        }
    }

    public MultiWaitHolderBase RecalcMultiWaitTimeout(out TimeSpan outMinTimeout, TimeSpan endTime)
    {
        MultiWaitHolderBase minTimeoutHolder = null;
        TimeSpan endTimeMin = endTime;

        foreach (MultiWaitHolderBase holderBase in _multiWaitList)
        {
            TimeSpan wakeupTime = holderBase.GetAbsoluteTimeToWakeup();
            if (wakeupTime < endTimeMin)
            {
                endTimeMin = wakeupTime;
                minTimeoutHolder = holderBase;
            }
        }

        if (endTimeMin < _currentTime)
        {
            outMinTimeout = new TimeSpan(0);
        }
        else
        {
            outMinTimeout = endTimeMin - _currentTime;
        }

        return minTimeoutHolder;
    }
}