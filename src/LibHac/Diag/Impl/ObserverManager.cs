using System.Collections.Generic;
using LibHac.Os;

namespace LibHac.Diag.Impl
{
    internal interface IObserverHolder
    {
        bool IsRegistered { get; set; }
    }

    internal class ObserverManager<TObserver, TItem> where TObserver : IObserverHolder
    {
        private LinkedList<TObserver> _observers;
        private ReaderWriterLock _rwLock;

        public delegate void Function(ref TObserver observer, in TItem item);

        public ObserverManager(HorizonClient hos)
        {
            _observers = new LinkedList<TObserver>();
            _rwLock = new ReaderWriterLock(hos.Os);
        }

        public void RegisterObserver(TObserver observerHolder)
        {
            Assert.SdkRequires(!observerHolder.IsRegistered);

            using ScopedLock<ReaderWriterLock> lk = ScopedLock.Lock(ref _rwLock);

            _observers.AddFirst(observerHolder);
            observerHolder.IsRegistered = true;
        }

        public void UnregisterObserver(TObserver observerHolder)
        {
            Assert.SdkRequires(observerHolder.IsRegistered);

            using ScopedLock<ReaderWriterLock> lk = ScopedLock.Lock(ref _rwLock);

            LinkedListNode<TObserver> foundObserver = _observers.Find(observerHolder);
            if (foundObserver is not null)
            {
                _observers.Remove(foundObserver);
            }

            observerHolder.IsRegistered = false;
        }

        public void UnregisterAllObservers()
        {
            using ScopedLock<ReaderWriterLock> lk = ScopedLock.Lock(ref _rwLock);

            LinkedListNode<TObserver> curNode = _observers.First;

            while (curNode is not null)
            {
                curNode.ValueRef.IsRegistered = false;
                curNode = curNode.Next;
            }

            _observers.Clear();
        }

        public void InvokeAllObserver(in TItem item, Function function)
        {
            using ScopedLock<ReaderWriterLock> lk = ScopedLock.Lock(ref _rwLock);

            LinkedListNode<TObserver> curNode = _observers.First;

            while (curNode is not null)
            {
                function(ref curNode.ValueRef, in item);
                curNode = curNode.Next;
            }
        }
    }

    // Todo: Use generic class when ref structs can be used as generics
    internal class LogObserverManager
    {
        private readonly LinkedList<LogObserverHolder> _observers;
        private ReaderWriterLock _rwLock;

        public delegate void Function(ref LogObserverHolder observer, in LogObserverContext item);

        public LogObserverManager(HorizonClient hos)
        {
            _observers = new LinkedList<LogObserverHolder>();
            _rwLock = new ReaderWriterLock(hos.Os);
        }

        public void RegisterObserver(LogObserverHolder observerHolder)
        {
            Assert.SdkRequires(!observerHolder.IsRegistered);

            using ScopedLock<ReaderWriterLock> lk = ScopedLock.Lock(ref _rwLock);

            _observers.AddFirst(observerHolder);
            observerHolder.IsRegistered = true;
        }

        public void UnregisterObserver(LogObserverHolder observerHolder)
        {
            Assert.SdkRequires(observerHolder.IsRegistered);

            using ScopedLock<ReaderWriterLock> lk = ScopedLock.Lock(ref _rwLock);

            LinkedListNode<LogObserverHolder> foundObserver = _observers.Find(observerHolder);
            if (foundObserver is not null)
            {
                _observers.Remove(foundObserver);
            }

            observerHolder.IsRegistered = false;
        }

        public void UnregisterAllObservers()
        {
            using ScopedLock<ReaderWriterLock> lk = ScopedLock.Lock(ref _rwLock);

            LinkedListNode<LogObserverHolder> curNode = _observers.First;

            while (curNode is not null)
            {
                curNode.ValueRef.IsRegistered = false;
                curNode = curNode.Next;
            }

            _observers.Clear();
        }

        public void InvokeAllObserver(in LogObserverContext item, Function function)
        {
            using ScopedLock<ReaderWriterLock> lk = ScopedLock.Lock(ref _rwLock);

            LinkedListNode<LogObserverHolder> curNode = _observers.First;

            while (curNode is not null)
            {
                function(ref curNode.ValueRef, in item);
                curNode = curNode.Next;
            }
        }
    }
}
