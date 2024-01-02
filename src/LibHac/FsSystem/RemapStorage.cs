// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Fs;
using LibHac.Os;

namespace LibHac.FsSystem;

public class RemapStorage : IStorage
{
    private struct ControlArea
    {
        public uint Magic;
        public int Version;
        public int CountMapEntries;
        public int SegmentCount;
        public int SegmentBits;
        public Array44<byte> Reserved;
    }

    private ControlArea _controlArea;
    private ValueSubStorage _controlAreaStorage;
    private ValueSubStorage _mapEntryStorage;
    private MapEntryCache _mapEntryCache;
    private Array4<StorageEntry> _storageEntries;
    private bool _isInitialized;
    private bool _isDirty;

    public RemapStorage()
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    public static long QueryMetaSize()
    {
        throw new NotImplementedException();
    }

    public static long QueryEntryTableSize(int countMapUpdate)
    {
        throw new NotImplementedException();
    }

    public static long GetMapUpdateCountLowerBound(long sizeEntry)
    {
        throw new NotImplementedException();
    }

    public static long GetMapUpdateCountUpperBound(long sizeEntry)
    {
        throw new NotImplementedException();
    }

    public static Result Format(ref ValueSubStorage storageMeta, int countMaxMaps)
    {
        throw new NotImplementedException();
    }

    public Result Initialize(in ValueSubStorage storageMeta, in ValueSubStorage storageEntry, long alignmentSmall,
        long alignmentLarge)
    {
        throw new NotImplementedException();
    }

    public void FinalizeObject()
    {
        throw new NotImplementedException();
    }

    public Result RegisterMap(out long outOffsetVirtual, long size, long alignment, int storageType)
    {
        throw new NotImplementedException();
    }

    public long GetEntryTableSize()
    {
        throw new NotImplementedException();
    }

    public long GetMapUpdateCount()
    {
        throw new NotImplementedException();
    }

    public long GetMapSizeMax()
    {
        throw new NotImplementedException();
    }

    public long GetAlignmentSmall()
    {
        throw new NotImplementedException();
    }

    public long GetAlignmentLarge()
    {
        throw new NotImplementedException();
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        throw new NotImplementedException();
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        throw new NotImplementedException();
    }

    public override Result Flush()
    {
        throw new NotImplementedException();
    }

    public override Result SetSize(long size)
    {
        throw new NotImplementedException();
    }

    public override Result GetSize(out long size)
    {
        throw new NotImplementedException();
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }

    [NonCopyableDisposable]
    private struct StorageEntry
    {
        private long _offsetPhysicalNextSmall;
        private long _offsetPhysicalNextLarge;
        private ValueSubStorage _storage;
        private bool _isRegistered;

        public StorageEntry(in StorageEntry other)
        {
            throw new NotImplementedException();
        }

        public StorageEntry()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void Initialize(long offsetPhysicalNextSmall, long offsetPhysicalNextLarge, long alignmentSmall,
            long alignmentLarge)
        {
            throw new NotImplementedException();
        }

        public void Set(in StorageEntry other)
        {
            throw new NotImplementedException();
        }

        public readonly bool IsRegistered()
        {
            throw new NotImplementedException();
        }

        public long GetOffsetPhysicalNextLarge()
        {
            throw new NotImplementedException();
        }

        public long GetOffsetPhysicalNextSmall()
        {
            throw new NotImplementedException();
        }

        public void SetOffsetPhysicalNextLarge(long offset)
        {
            throw new NotImplementedException();
        }

        public void SetOffsetPhysicalNextSmall(long offset)
        {
            throw new NotImplementedException();
        }

        public ref ValueSubStorage GetStorage()
        {
            throw new NotImplementedException();
        }

        public void SetStorage(in ValueSubStorage storage)
        {
            throw new NotImplementedException();
        }
    }

    private Result InitializeStorageEntries()
    {
        throw new NotImplementedException();
    }

    private Result ReadMapEntry(out MapEntry outEntry, int index)
    {
        throw new NotImplementedException();
    }

    private Result WriteMapEntry(in MapEntry entry, int index)
    {
        throw new NotImplementedException();
    }

    private long MakeVirtualOffset(long higherBits, long lowerBits)
    {
        throw new NotImplementedException();
    }

    private long GetVirtualOffsetLowerBitsMask()
    {
        throw new NotImplementedException();
    }

    private long GetVirtualOffsetHigherBitsMask()
    {
        throw new NotImplementedException();
    }

    private long GetMapEntriesCountMax()
    {
        throw new NotImplementedException();
    }

    private Result RegisterMapCore(long offset, long sizeNew, long sizeOld, long alignment, int storageType)
    {
        throw new NotImplementedException();
    }

    private Result MakeMapEntry(out MapEntry outMapEntry, ref StorageEntry storageEntry, ref ControlArea controlArea,
        long offset, long size, long alignment, int storageType)
    {
        throw new NotImplementedException();
    }

    private Result AddMapEntries(ReadOnlySpan<MapEntry> mapEntries)
    {
        throw new NotImplementedException();
    }

    private delegate Result IterateMappingEntriesFunc(IStorage storage, long offset, long sizeAccess, long sizeAccessed,
        ref IterateMappingEntriesClosure closure);

    private ref struct IterateMappingEntriesClosure
    {
        public ReadOnlySpan<byte> InBuffer;
        public Span<byte> OutBuffer;
        public OperationId OperationId;
    }

    private Result IterateMappingEntries(long offset, long size, IterateMappingEntriesFunc func,
        ref IterateMappingEntriesClosure closure)
    {
        throw new NotImplementedException();
    }

    private ref MapEntryCache GetCache(long offset)
    {
        throw new NotImplementedException();
    }

    private void ClearAllCaches()
    {
        throw new NotImplementedException();
    }

    private struct MapEntry
    {
        public long OffsetVirtual;
        public long OffsetPhysical;
        public long Size;
        public int Alignment;
        public int StorageType;
    }

    private struct MapEntryHolder
    {
        public MapEntry Entry;
        public int Index;
    }

    private struct MapIterator
    {
        private RemapStorage _remapStorage;
        private MapEntry _entry;
        private int _indexMapEntryCache;
        private int _storageType;
        private Result _lastResult;

        public MapIterator(RemapStorage remapStorage, long offset)
        {
            throw new NotImplementedException();
        }

        public bool MoveNext(long maxOffset)
        {
            throw new NotImplementedException();
        }

        public readonly ref readonly MapEntry GetMapEntry()
        {
            throw new NotImplementedException();
        }

        public readonly IStorage GetStorage()
        {
            throw new NotImplementedException();
        }

        public readonly Result GetLastResult()
        {
            throw new NotImplementedException();
        }
    }

    private class MapEntryCache
    {
        private Array20<MapEntryHolder> _entries;
        private int _startIndex;
        private SdkMutex _mutex;

        public MapEntryCache()
        {
            throw new NotImplementedException();
        }

        public bool SetMapEntry(in MapEntry entry, int index)
        {
            throw new NotImplementedException();
        }

        public bool GetMapEntry(out MapEntry outMapEntry, out int outIndexMapEntry, long offset)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }
    }
}