using System;
using System.Collections.Generic;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Fsa;
using LibHac.Os;

namespace LibHac.FsSrv.Impl;

/// <summary>
/// Caches opened FatFileSystems.
/// </summary>
/// <remarks>Based on nnSdk 18.3.0 (FS 18.0.0)</remarks>
public class FatFileSystemCacheManager : IDisposable
{
    public readonly struct CacheId
    {
        internal readonly int Value;

        internal CacheId(int value) => Value = value;
    }

    private struct CacheNode : IDisposable
    {
        public int Id;
        public SharedRef<IFileSystem> FileSystem;

        public CacheNode(int id, ref readonly SharedRef<IFileSystem> fileSystem)
        {
            Id = id;
            FileSystem = SharedRef<IFileSystem>.CreateCopy(in fileSystem);
        }

        public void Dispose()
        {
            FileSystem.Destroy();
        }
    }

    public struct Iterator
    {
        private LinkedListNode<CacheNode> _current;

        internal Iterator(FatFileSystemCacheManager manager)
        {
            _current = manager._cache.First;
        }

        public readonly bool IsEnd() => _current is not null;
        public void Next() => _current = _current.Next;
        public readonly SharedRef<IFileSystem> Get() => SharedRef<IFileSystem>.CreateCopy(in _current.ValueRef.FileSystem);
    }

    private SdkRecursiveMutex _mutex;
    private int _nextCacheId;
    private LinkedList<CacheNode> _cache;

    public FatFileSystemCacheManager()
    {
        _mutex = new SdkRecursiveMutex();
        _nextCacheId = 0;
        _cache = new LinkedList<CacheNode>();
    }

    public void Dispose()
    {
        while (_cache.First is not null)
        {
            LinkedListNode<CacheNode> currentNode = _cache.First;
            _cache.Remove(currentNode);
            currentNode.ValueRef.Dispose();
        }
    }

    public UniqueLock<SdkRecursiveMutex> GetScopedLock()
    {
        return new UniqueLock<SdkRecursiveMutex>(_mutex);
    }

    private SharedRef<IFileSystem> GetCache(int cacheId)
    {
        LinkedListNode<CacheNode> currentNode = _cache.First;

        while (currentNode is not null)
        {
            if (currentNode.ValueRef.Id == cacheId)
            {
                return SharedRef<IFileSystem>.CreateCopy(in currentNode.ValueRef.FileSystem);
            }

            currentNode = currentNode.Next;
        }

        return new SharedRef<IFileSystem>();
    }

    public SharedRef<IFileSystem> GetCache(CacheId cacheId)
    {
        return GetCache(cacheId.Value);
    }

    public Result SetCache(out CacheId outCacheId, ref readonly SharedRef<IFileSystem> fileSystem)
    {
        int originalId = _nextCacheId;

        while (true)
        {
            bool cacheIdInUse;
            using (SharedRef<IFileSystem> fs = GetCache(_nextCacheId))
            {
                cacheIdInUse = fs.HasValue;
            }

            if (!cacheIdInUse)
                break;

            _nextCacheId++;
            if (_nextCacheId == originalId)
                Abort.DoAbort();
        }

        int id = _nextCacheId;
        var node = new LinkedListNode<CacheNode>(new CacheNode(_nextCacheId, in fileSystem));
        _cache.AddLast(node);

        _nextCacheId++;
        outCacheId = new CacheId(id);
        return Result.Success;
    }

    public void UnsetCache(CacheId cacheId)
    {
        LinkedListNode<CacheNode> currentNode = _cache.First;

        while (currentNode is not null)
        {
            if (currentNode.ValueRef.Id == cacheId.Value)
            {
                _cache.Remove(currentNode);
                currentNode.ValueRef.Dispose();
                return;
            }

            currentNode = currentNode.Next;
        }
    }

    public Iterator GetIterator()
    {
        return new Iterator(this);
    }
}