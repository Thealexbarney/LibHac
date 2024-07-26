// ReSharper disable UnusedMember.Local NotAccessedField.Local
#pragma warning disable CS0169 // Field is never used
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Util;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs;

using Position = uint;
using StorageSizeType = uint;

public class KeyValueRomStorageTemplate<TKey, TValue> : IDisposable where TKey : unmanaged where TValue : unmanaged
{
    private long _bucketCount;
    private ValueSubStorage _bucketStorage;
    private ValueSubStorage _entryStorage;
    private long _totalEntrySize;
    private uint _entryCount;

    private const Position InvalidPosition = ~default(Position);

    private struct StorageElement
    {
        public TKey Key;
        public TValue Value;
        public Position Next;
        public StorageSizeType Size;
    }

    public KeyValueRomStorageTemplate()
    {
        throw new NotImplementedException();
    }

    public virtual void Dispose()
    {
        throw new NotImplementedException();
    }

    public static uint QueryBucketCount(long bucketStorageSize)
    {
        return (uint)(bucketStorageSize / Unsafe.SizeOf<Position>());
    }

    public static long QueryBucketCount(uint bucketCount)
    {
        return bucketCount * Unsafe.SizeOf<Position>();
    }

    public static long QueryEntrySize(uint extraSize)
    {
        // Todo: Use AlignOf<StorageElement> for the alignment when it's added to the language
        return Alignment.AlignUp(Unsafe.SizeOf<StorageElement>() + extraSize, 4);
    }

    public static Result Format(ref ValueSubStorage bucketStorage, uint bucketCount)
    {
        Position pos = InvalidPosition;

        for (int i = 0; i < bucketCount; i++)
        {
            Result res = bucketStorage.Write(i * Unsafe.SizeOf<Position>(), SpanHelpers.AsByteSpan(ref pos));
            if (res.IsFailure()) return res.Miss();
        }

        return Result.Success;
    }

    public Result Initialize(ref readonly ValueSubStorage bucketStorage, long bucketCount,
        ref readonly ValueSubStorage entryStorage)
    {
        throw new NotImplementedException();
    }

    public void FinalizeObject()
    {
        throw new NotImplementedException();
    }

    public long GetTotalEntrySize() => _totalEntrySize;

    private long HashToBucket(uint hashKey) => hashKey % _bucketCount;

    protected Result AddInternal(out Position outPosition, in TKey key, uint hashKey, ReadOnlySpan<byte> extraKey,
        in TValue value)
    {
        throw new NotImplementedException();
    }

    protected Result GetInternal(out Position outPosition, out TValue outValue, in TKey key, uint hashKey,
        ReadOnlySpan<byte> extraKey)
    {
        throw new NotImplementedException();
    }

    protected Result GetByPosition(out TKey outKey, out TValue outValue, Position position)
    {
        throw new NotImplementedException();
    }

    protected Result GetByPosition(out TKey outKey, out TValue outValue, Span<byte> outExtraKey,
        out int outExtraKeySize, Position position)
    {
        throw new NotImplementedException();
    }

    protected Result SetByPosition(Position position, in TValue value)
    {
        Result res = ReadKeyValue(out StorageElement element, position);
        if (res.IsFailure()) return res.Miss();

        element.Value = value;
        return WriteKeyValue(in element, position, default).Ret();
    }

    private Result FindInternal(out Position outPosition, out Position outPreviousPosition,
        out StorageElement outStorageElement, in TKey key, uint hashKey, ReadOnlySpan<byte> extraKey)
    {
        throw new NotImplementedException();
    }

    private Result AllocateEntry(out Position outPosition, uint extraSize)
    {
        throw new NotImplementedException();
    }

    private Result LinkEntry(out Position outNextPosition, Position position, uint hashKey)
    {
        throw new NotImplementedException();
    }

    private Result ReadBucket(out Position outPosition, long index)
    {
        throw new NotImplementedException();
    }

    private Result WriteBucket(Position position, long index)
    {
        Assert.SdkRequiresLess(index, _bucketCount);

        long offset = index * Unsafe.SizeOf<Position>();
        return _bucketStorage.Write(offset, SpanHelpers.AsReadOnlyByteSpan(in position)).Ret();
    }

    private Result ReadKeyValue(out StorageElement outElement, Position position)
    {
        throw new NotImplementedException();
    }

    private Result ReadKeyValue(out StorageElement outElement, Span<byte> outExtraKey, out int outExtraKeySize,
        Position position)
    {
        throw new NotImplementedException();
    }

    private Result WriteKeyValue(in StorageElement element, Position position, ReadOnlySpan<byte> extraKey)
    {
        throw new NotImplementedException();
    }
}