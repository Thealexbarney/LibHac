using System.Collections.Generic;
using LibHac.Diag;

namespace LibHac.Os.Impl;

public class MultiWaitObjectList
{
    private LinkedList<MultiWaitHolderBase> _objectList;

    public MultiWaitObjectList()
    {
        _objectList = new LinkedList<MultiWaitHolderBase>();
    }

    public void WakeupAllMultiWaitThreadsUnsafe()
    {
        foreach (MultiWaitHolderBase holderBase in _objectList)
        {
            holderBase.GetMultiWait().NotifyAndWakeupThread(holderBase);
        }
    }

    public void BroadcastToUpdateObjectStateUnsafe()
    {
        foreach (MultiWaitHolderBase holderBase in _objectList)
        {
            holderBase.GetMultiWait().NotifyAndWakeupThread(null);
        }
    }

    public bool IsEmpty()
    {
        return _objectList.Count == 0;
    }

    public void PushBackToList(MultiWaitHolderBase holderBase)
    {
        _objectList.AddLast(holderBase);
    }

    public void EraseFromList(MultiWaitHolderBase holderBase)
    {
        Assert.SdkRequires(_objectList.Contains(holderBase));

        _objectList.Remove(holderBase);
    }
}