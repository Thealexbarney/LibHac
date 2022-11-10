using System;
using System.Collections.Generic;
using LibHac.Diag;

namespace LibHac.FsSystem;

/// <summary>
/// Represents a list of key/value pairs that are ordered by when they were last accessed.
/// </summary>
/// <typeparam name="TKey">The type of the keys in the list.</typeparam>
/// <typeparam name="TValue">The type of the values in the list.</typeparam>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public class LruListCache<TKey, TValue> where TKey : IEquatable<TKey>
{
    public struct Node
    {
        public TKey Key;
        public TValue Value;

        public Node(TValue value)
        {
            Key = default;
            Value = value;
        }
    }

    private LinkedList<Node> _list;

    public LruListCache()
    {
        _list = new LinkedList<Node>();
    }

    public bool FindValueAndUpdateMru(out TValue value, TKey key)
    {
        LinkedListNode<Node> currentNode = _list.First;

        while (currentNode is not null)
        {
            if (currentNode.ValueRef.Key.Equals(key))
            {
                value = currentNode.ValueRef.Value;

                _list.Remove(currentNode);
                _list.AddFirst(currentNode);

                return true;
            }

            currentNode = currentNode.Next;
        }

        value = default;
        return false;
    }

    public LinkedListNode<Node> PopLruNode()
    {
        Abort.DoAbortUnless(_list.Count != 0);

        LinkedListNode<Node> lru = _list.Last;
        _list.RemoveLast();

        return lru;
    }

    public void PushMruNode(LinkedListNode<Node> node, TKey key)
    {
        node.ValueRef.Key = key;
        _list.AddFirst(node);
    }

    public void DeleteAllNodes()
    {
        _list.Clear();
    }

    public int GetSize()
    {
        return _list.Count;
    }

    public bool IsEmpty()
    {
        return _list.Count == 0;
    }
}