using System.Collections.Generic;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Os;
using LibHac.Util;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs.Impl
{
    internal class MountTable
    {
        private LinkedList<FileSystemAccessor> _fileSystemList;
        private SdkMutexType _mutex;

        public MountTable(FileSystemClient fsClient)
        {
            _fileSystemList = new LinkedList<FileSystemAccessor>();
            _mutex = new SdkMutexType();
            _mutex.Initialize();
        }

        public Result Mount(FileSystemAccessor fileSystem)
        {
            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            if (!CanAcceptMountName(fileSystem.GetName()))
                return ResultFs.MountNameAlreadyExists.Log();

            _fileSystemList.AddLast(fileSystem);
            return Result.Success;
        }

        public Result Find(out FileSystemAccessor accessor, U8Span name)
        {
            UnsafeHelpers.SkipParamInit(out accessor);
            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            for (LinkedListNode<FileSystemAccessor> currentNode = _fileSystemList.First;
                currentNode is not null;
                currentNode = currentNode.Next)
            {
                if (!Matches(currentNode.Value, name)) continue;
                accessor = currentNode.Value;
                return Result.Success;
            }

            return ResultFs.NotMounted.Log();
        }

        public void Unmount(U8Span name)
        {
            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            LinkedListNode<FileSystemAccessor> currentNode;
            for (currentNode = _fileSystemList.First; currentNode is not null; currentNode = currentNode.Next)
            {
                if (Matches(currentNode.Value, name))
                    break;
            }

            if (currentNode is null)
                Abort.DoAbort(ResultFs.NotMounted.Log(), $"{name.ToString()} is not mounted.");

            _fileSystemList.Remove(currentNode);
            currentNode.Value.Dispose();
        }

        public bool CanAcceptMountName(U8Span name)
        {
            Assert.SdkAssert(_mutex.IsLockedByCurrentThread());

            for (LinkedListNode<FileSystemAccessor> currentNode = _fileSystemList.First;
                currentNode is not null;
                currentNode = currentNode.Next)
            {
                if (Matches(currentNode.Value, name))
                    return false;
            }

            return true;
        }

        private static bool Matches(FileSystemAccessor accessor, U8Span name)
        {
            return StringUtils.Compare(accessor.GetName(), name, Unsafe.SizeOf<MountName>()) == 0;
        }
    }
}
