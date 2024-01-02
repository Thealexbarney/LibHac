// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;

namespace LibHac.Fs.Dbm;

public class KeyValueListTemplate<TKey, TValue> where TKey : unmanaged where TValue : unmanaged
{
    private BufferedAllocationTableStorage _keyValueStorage;
    private uint _maxCount;

    public struct FormatInformation
    {
        public uint IndexLimitation;
        public uint IndexNotFormatted;

        public static ref FormatInformation GetFromStorageElement(ref StorageElement element)
        {
            throw new NotImplementedException();
        }
    }

    public struct StorageElement
    {
        public TKey Key;
        public TValue Value;
        public uint NextIndex;
    }

    public KeyValueListTemplate()
    {
        throw new NotImplementedException();
    }

    public static uint QueryEntryCount(long storageSize)
    {
        throw new NotImplementedException();
    }

    public static uint QueryStorageSize(uint entryCount)
    {
        throw new NotImplementedException();
    }

    public static Result Format(AllocationTableStorage storage)
    {
        throw new NotImplementedException();
    }

    public Result Initialize(BufferedAllocationTableStorage keyValueStorage)
    {
        throw new NotImplementedException();
    }

    public void FinalizeObject()
    {
        throw new NotImplementedException();
    }

    public Result GetMaxCount(out uint outCount)
    {
        throw new NotImplementedException();
    }

    public Result Remove(in TKey key)
    {
        throw new NotImplementedException();
    }

    public Result ModifyKey(in TKey newKey, in TKey oldKey)
    {
        throw new NotImplementedException();
    }

    public void EnterHoldingCacheSection()
    {
        throw new NotImplementedException();
    }

    public void LeaveHoldingCacheSection()
    {
        throw new NotImplementedException();
    }

    protected Result AddInternal(out uint outIndex, in TKey key, in TValue value)
    {
        throw new NotImplementedException();
    }

    protected Result GetInternal(out uint outIndex, out TValue outValue, in TKey key)
    {
        throw new NotImplementedException();
    }

    protected Result GetByIndex(out TKey outKey, out TValue outValue, uint index)
    {
        throw new NotImplementedException();
    }

    protected Result SetByIndex(uint index, in TValue value)
    {
        throw new NotImplementedException();
    }

    private Result ReadKeyValue(out StorageElement outElement, uint index)
    {
        throw new NotImplementedException();
    }

    private Result ReadKeyValue(Span<StorageElement> outElements, uint index, uint count)
    {
        throw new NotImplementedException();
    }

    private Result ReadKeyValueImpl(ref StorageElement outElement, uint index)
    {
        throw new NotImplementedException();
    }

    private Result WriteKeyValue(in StorageElement element, uint index)
    {
        throw new NotImplementedException();
    }

    private Result FindInternal(out uint outIndex, out uint outPreviousIndex, out StorageElement outElement, in TKey key)
    {
        throw new NotImplementedException();
    }

    private Result AllocateEntry(out uint outEntryIndex)
    {
        throw new NotImplementedException();
    }

    private Result LinkEntry(out uint outOldNextIndex, uint index, uint nextIndex)
    {
        throw new NotImplementedException();
    }

    private Result FreeEntry(uint index)
    {
        throw new NotImplementedException();
    }
}