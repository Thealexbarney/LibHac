using System;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Os;
using LibHac.Util;
using Buffer = LibHac.Mem.Buffer;

namespace LibHac.FsSystem;

/// <summary>
/// Allows searching and iterating the entries in a bucket tree data structure.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
public partial class BucketTree : IDisposable
{
    private const uint ExpectedMagic = 0x52544B42; // BKTR
    private const int MaxVersion = 1;

    private const int NodeSizeMin = 1024;
    private const int NodeSizeMax = 1024 * 512;

    private static readonly int BufferAlignment = sizeof(long);

    private static int NodeHeaderSize => Unsafe.SizeOf<NodeHeader>();

    private ValueSubStorage _nodeStorage;
    private ValueSubStorage _entryStorage;

    private NodeBuffer _nodeL1;

    private long _nodeSize;
    private long _entrySize;
    private int _entryCount;
    private int _offsetCount;
    private int _entrySetCount;
    private OffsetCache _offsetCache;

    public struct ContinuousReadingInfo
    {
        private long _readSize;
        private int _skipCount;
        private bool _isDone;

        public readonly bool CanDo() => _readSize != 0;
        public bool CheckNeedScan() => --_skipCount <= 0;
        public readonly bool IsDone() => _isDone;

        public void Done()
        {
            _readSize = 0;
            _isDone = true;
        }

        public readonly long GetReadSize() => _readSize;
        public void SetReadSize(long readSize) => _readSize = readSize;

        public void Reset()
        {
            _readSize = 0;
            _skipCount = 0;
            _isDone = false;
        }

        public void SetSkipCount(int count)
        {
            Assert.SdkRequiresGreaterEqual(count, 0);

            _skipCount = count;
        }
    }

    public interface IContinuousReadingEntry
    {
        int FragmentSizeMax { get; }

        long GetVirtualOffset();
        long GetPhysicalOffset();
        bool IsFragment();
    }

    private struct ContinuousReadingParam<TEntry> where TEntry : unmanaged, IContinuousReadingEntry
    {
        public long Offset;
        public long Size;
        public NodeHeader EntrySet;
        public int EntryIndex;
        public Offsets TreeOffsets;
        public TEntry Entry;
    }

    public struct NodeHeader
    {
        public int Index;
        public int EntryCount;
        public long OffsetEnd;

        public Result Verify(int nodeIndex, long nodeSize, long entrySize)
        {
            if (Index != nodeIndex)
                return ResultFs.InvalidBucketTreeNodeIndex.Log();

            if (entrySize == 0 || nodeSize < entrySize + NodeHeaderSize)
                return ResultFs.InvalidSize.Log();

            long maxEntryCount = (nodeSize - NodeHeaderSize) / entrySize;

            if (EntryCount <= 0 || maxEntryCount < EntryCount)
                return ResultFs.InvalidBucketTreeNodeEntryCount.Log();

            if (OffsetEnd < 0)
                return ResultFs.InvalidBucketTreeNodeOffset.Log();

            return Result.Success;
        }
    }

    [NonCopyable]
    private struct NodeBuffer : IDisposable
    {
        private MemoryResource _allocator;
        private Buffer _header;

        public void Dispose()
        {
            Assert.SdkAssert(_header.IsNull);
        }

        public readonly MemoryResource GetAllocator() => _allocator;

        public bool Allocate(MemoryResource allocator, int nodeSize)
        {
            Assert.SdkRequires(_header.IsNull);

            _allocator = allocator;
            _header = allocator.Allocate(nodeSize, BufferAlignment);

            return !_header.IsNull;
        }

        public void Free()
        {
            if (!_header.IsNull)
            {
                _allocator.Deallocate(ref _header, BufferAlignment);
                _header = Buffer.Empty;
            }

            _allocator = null;
        }

        public void FillZero()
        {
            if (!_header.IsNull)
            {
                _header.Span.Clear();
            }
        }

        public readonly ref NodeHeader GetHeader()
        {
            Assert.SdkRequiresGreaterEqual(_header.Length * sizeof(long), Unsafe.SizeOf<NodeHeader>());

            return ref Unsafe.As<byte, NodeHeader>(ref _header.Span[0]);
        }

        public readonly Span<byte> GetBuffer()
        {
            return _header.Span;
        }

        public readonly BucketTreeNode<TEntry> GetNode<TEntry>() where TEntry : unmanaged
        {
            return new BucketTreeNode<TEntry>(GetBuffer());
        }
    }

    private struct StorageNode
    {
        private Offset _start;
        private int _count;
        private int _index;

        public StorageNode(long offset, long size, int count)
        {
            _start = new Offset(offset + NodeHeaderSize, (int)size);
            _count = count;
            _index = -1;
        }

        public StorageNode(long size, int count)
        {
            _start = new Offset(NodeHeaderSize, (int)size);
            _count = count;
            _index = -1;
        }

        public readonly int GetIndex() => _index;

        public void Find(ReadOnlySpan<byte> buffer, long virtualAddress)
        {
            int end = _count;
            Offset pos = _start;

            while (end > 0)
            {
                int half = end / 2;
                Offset mid = pos + half;

                long offset = BinaryPrimitives.ReadInt64LittleEndian(buffer.Slice((int)mid.Get()));

                if (offset <= virtualAddress)
                {
                    pos = mid + 1;
                    end -= half + 1;
                }
                else
                {
                    end = half;
                }
            }

            _index = (int)(pos - _start) - 1;
        }

        public Result Find(in ValueSubStorage storage, long virtualAddress)
        {
            int end = _count;
            Offset pos = _start;

            while (end > 0)
            {
                int half = end / 2;
                Offset mid = pos + half;

                long offset = 0;
                Result res = storage.Read(mid.Get(), SpanHelpers.AsByteSpan(ref offset));
                if (res.IsFailure()) return res.Miss();

                if (offset <= virtualAddress)
                {
                    pos = mid + 1;
                    end -= half + 1;
                }
                else
                {
                    end = half;
                }
            }

            _index = (int)(pos - _start) - 1;
            return Result.Success;
        }

        private readonly struct Offset
        {
            private readonly long _offset;
            private readonly int _stride;

            public Offset(long offset, int stride)
            {
                _offset = offset;
                _stride = stride;
            }

            public long Get() => _offset;

            public static Offset operator ++(Offset left) => left + 1;
            public static Offset operator --(Offset left) => left - 1;

            public static Offset operator +(Offset left, long right) => new Offset(left._offset + right * left._stride, left._stride);
            public static Offset operator -(Offset left, long right) => new Offset(left._offset - right * left._stride, left._stride);

            public static long operator -(Offset left, Offset right) =>
                (left._offset - right._offset) / left._stride;

            public static bool operator ==(Offset left, Offset right) => left._offset == right._offset;
            public static bool operator !=(Offset left, Offset right) => left._offset != right._offset;

            public bool Equals(Offset other) => _offset == other._offset;
            public override bool Equals(object obj) => obj is Offset other && Equals(other);
            public override int GetHashCode() => _offset.GetHashCode();
        }
    }

    private struct OffsetCache
    {
        public OffsetCache()
        {
            Mutex = new SdkMutexType();
            IsInitialized = false;
            Offsets.StartOffset = -1;
            Offsets.EndOffset = -1;
        }

        public bool IsInitialized;
        public Offsets Offsets;
        public SdkMutexType Mutex;
    }

    public struct Offsets
    {
        public long StartOffset;
        public long EndOffset;

        public readonly bool IsInclude(long offset)
        {
            return StartOffset <= offset && offset < EndOffset;
        }

        public readonly bool IsInclude(long offset, long size)
        {
            return size > 0 && StartOffset <= offset && size <= EndOffset - offset;
        }
    }

    public struct Header
    {
        public uint Magic;
        public uint Version;
        public int EntryCount;
#pragma warning disable 414
        private int _reserved;
#pragma warning restore 414

        public void Format(int entryCount)
        {
            Assert.SdkRequiresLessEqual(0, entryCount);

            Magic = ExpectedMagic;
            Version = MaxVersion;
            EntryCount = entryCount;
            _reserved = 0;
        }

        public readonly Result Verify()
        {
            if (Magic != ExpectedMagic)
                return ResultFs.InvalidBucketTreeSignature.Log();

            if (EntryCount < 0)
                return ResultFs.InvalidBucketTreeEntryCount.Log();

            if (Version > MaxVersion)
                return ResultFs.UnsupportedVersion.Log();

            return Result.Success;
        }
    }

    public readonly ref struct BucketTreeNode<TEntry> where TEntry : unmanaged
    {
        private readonly Span<byte> _buffer;

        public BucketTreeNode(Span<byte> buffer)
        {
            _buffer = buffer;

            Assert.SdkRequiresGreaterEqual(_buffer.Length, Unsafe.SizeOf<NodeHeader>());
            Assert.SdkRequiresGreaterEqual(_buffer.Length,
                Unsafe.SizeOf<NodeHeader>() + GetHeader().EntryCount * Unsafe.SizeOf<TEntry>());
        }

        public int GetCount() => GetHeader().EntryCount;

        public ReadOnlySpan<TEntry> GetArray() => GetWritableArray();
        internal Span<TEntry> GetWritableArray() => GetWritableArray<TEntry>();

        public long GetBeginOffset() => GetArray<long>()[0];
        public long GetEndOffset() => GetHeader().OffsetEnd;
        public long GetL2BeginOffset() => GetArray<long>()[GetCount()];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<TElement> GetArray<TElement>() where TElement : unmanaged
        {
            return GetWritableArray<TElement>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Span<TElement> GetWritableArray<TElement>() where TElement : unmanaged
        {
            return MemoryMarshal.Cast<byte, TElement>(_buffer.Slice(Unsafe.SizeOf<NodeHeader>()));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref NodeHeader GetHeader()
        {
            return ref Unsafe.As<byte, NodeHeader>(ref MemoryMarshal.GetReference(_buffer));
        }
    }

    private static int GetEntryCount(long nodeSize, long entrySize)
    {
        return (int)((nodeSize - Unsafe.SizeOf<NodeHeader>()) / entrySize);
    }

    private static int GetOffsetCount(long nodeSize)
    {
        return (int)((nodeSize - Unsafe.SizeOf<NodeHeader>()) / sizeof(long));
    }

    private static int GetEntrySetCount(long nodeSize, long entrySize, int entryCount)
    {
        int entryCountPerNode = GetEntryCount(nodeSize, entrySize);
        return BitUtil.DivideUp(entryCount, entryCountPerNode);
    }

    private static int GetNodeL2Count(long nodeSize, long entrySize, int entryCount)
    {
        int offsetCountPerNode = GetOffsetCount(nodeSize);
        int entrySetCount = GetEntrySetCount(nodeSize, entrySize, entryCount);

        if (entrySetCount <= offsetCountPerNode)
            return 0;

        int nodeL2Count = BitUtil.DivideUp(entrySetCount, offsetCountPerNode);
        Abort.DoAbortUnless(nodeL2Count <= offsetCountPerNode);

        return BitUtil.DivideUp(entrySetCount - (offsetCountPerNode - (nodeL2Count - 1)), offsetCountPerNode);
    }

    public static int QueryHeaderStorageSize() => Unsafe.SizeOf<Header>();

    public static long QueryNodeStorageSize(long nodeSize, long entrySize, int entryCount)
    {
        Assert.SdkRequiresLessEqual(sizeof(long), entrySize);
        Assert.SdkRequiresLessEqual(entrySize + Unsafe.SizeOf<NodeHeader>(), nodeSize);
        Assert.SdkRequiresWithinMinMax(nodeSize, NodeSizeMin, NodeSizeMax);
        Assert.SdkRequires(BitUtil.IsPowerOfTwo(nodeSize));
        Assert.SdkRequiresLessEqual(0, entryCount);

        if (entryCount <= 0)
            return 0;

        return (1 + GetNodeL2Count(nodeSize, entrySize, entryCount)) * nodeSize;
    }

    public static long QueryEntryStorageSize(long nodeSize, long entrySize, int entryCount)
    {
        Assert.SdkRequiresLessEqual(sizeof(long), entrySize);
        Assert.SdkRequiresLessEqual(entrySize + Unsafe.SizeOf<NodeHeader>(), nodeSize);
        Assert.SdkRequiresWithinMinMax(nodeSize, NodeSizeMin, NodeSizeMax);
        Assert.SdkRequires(BitUtil.IsPowerOfTwo(nodeSize));
        Assert.SdkRequiresLessEqual(0, entryCount);

        if (entryCount <= 0)
            return 0;

        return GetEntrySetCount(nodeSize, entrySize, entryCount) * nodeSize;
    }

    private static long GetBucketTreeEntryOffset(long entrySetOffset, long entrySize, int entryIndex)
    {
        return entrySetOffset + Unsafe.SizeOf<NodeHeader>() + entryIndex * entrySize;
    }

    private static long GetBucketTreeEntryOffset(int entrySetIndex, long nodeSize, long entrySize, int entryIndex)
    {
        return GetBucketTreeEntryOffset(entrySetIndex * nodeSize, entrySize, entryIndex);
    }

    public BucketTree()
    {
        _offsetCache = new OffsetCache();
    }

    public void Dispose()
    {
        FinalizeObject();
        _nodeL1.Dispose();
    }

    public MemoryResource GetAllocator() => _nodeL1.GetAllocator();

    public int GetEntryCount() => _entryCount;

    public Result GetOffsets(out Offsets offsets)
    {
        UnsafeHelpers.SkipParamInit(out offsets);

        Result res = EnsureOffsetCache();
        if (res.IsFailure()) return res.Miss();

        offsets = _offsetCache.Offsets;
        return Result.Success;
    }

    public Result Initialize(MemoryResource allocator, in ValueSubStorage nodeStorage, in ValueSubStorage entryStorage,
        int nodeSize, int entrySize, int entryCount)
    {
        Assert.SdkRequiresNotNull(allocator);
        Assert.SdkRequiresLessEqual(sizeof(long), entrySize);
        Assert.SdkRequiresLessEqual(entrySize + Unsafe.SizeOf<NodeHeader>(), nodeSize);
        Assert.SdkRequiresWithinMinMax(nodeSize, NodeSizeMin, NodeSizeMax);
        Assert.SdkRequires(BitUtil.IsPowerOfTwo(nodeSize));
        Assert.SdkRequires(!IsInitialized());

        // Ensure valid entry count.
        if (entryCount <= 0)
            return ResultFs.InvalidArgument.Log();

        // Allocate node.
        if (!_nodeL1.Allocate(allocator, nodeSize))
            return ResultFs.BufferAllocationFailed.Log();

        bool needFree = true;
        try
        {
            // Read node.
            Result res = nodeStorage.Read(0, _nodeL1.GetBuffer());
            if (res.IsFailure()) return res.Miss();

            // Verify node.
            res = _nodeL1.GetHeader().Verify(0, nodeSize, sizeof(long));
            if (res.IsFailure()) return res.Miss();

            // Validate offsets.
            int offsetCount = GetOffsetCount(nodeSize);
            int entrySetCount = GetEntrySetCount(nodeSize, entrySize, entryCount);
            BucketTreeNode<long> node = _nodeL1.GetNode<long>();

            long startOffset;
            if (offsetCount < entrySetCount && node.GetCount() < offsetCount)
            {
                startOffset = node.GetL2BeginOffset();
            }
            else
            {
                startOffset = node.GetBeginOffset();
            }

            long endOffset = node.GetEndOffset();

            if (startOffset < 0 || startOffset > node.GetBeginOffset() || startOffset >= endOffset)
                return ResultFs.InvalidBucketTreeEntryOffset.Log();

            _nodeStorage.Set(in nodeStorage);
            _entryStorage.Set(in entryStorage);
            _nodeSize = nodeSize;
            _entrySize = entrySize;
            _entryCount = entryCount;
            _offsetCount = offsetCount;
            _entrySetCount = entrySetCount;
            _offsetCache.IsInitialized = true;
            _offsetCache.Offsets.StartOffset = startOffset;
            _offsetCache.Offsets.EndOffset = endOffset;

            needFree = false;

            return Result.Success;
        }
        finally
        {
            if (needFree)
                _nodeL1.Free();
        }
    }

    public void Initialize(long nodeSize, long endOffset)
    {
        Assert.SdkRequiresWithinMinMax(nodeSize, NodeSizeMin, NodeSizeMax);
        Assert.SdkRequires(BitUtil.IsPowerOfTwo(nodeSize));
        Assert.SdkRequiresLessEqual(0, endOffset);
        Assert.SdkRequires(!IsInitialized());

        _nodeSize = nodeSize;
        _offsetCache.IsInitialized = true;
        _offsetCache.Offsets.StartOffset = 0;
        _offsetCache.Offsets.EndOffset = endOffset;
    }

    public void FinalizeObject()
    {
        if (IsInitialized())
        {
            _nodeStorage.Dispose();
            _entryStorage.Dispose();

            _nodeL1.Free();

            _nodeSize = 0;
            _entrySize = 0;
            _entryCount = 0;
            _offsetCount = 0;
            _entrySetCount = 0;
            _offsetCache.IsInitialized = false;
            _offsetCache.Offsets = default;
        }
    }

    public bool IsInitialized() => _nodeSize > 0;
    public bool IsEmpty() => _entrySize == 0;

    public Result Find(ref Visitor visitor, long virtualAddress)
    {
        Assert.SdkRequires(IsInitialized());

        if (virtualAddress < 0)
            return ResultFs.InvalidOffset.Log();

        if (IsEmpty())
            return ResultFs.OutOfRange.Log();

        Result res = GetOffsets(out Offsets offsets);
        if (res.IsFailure()) return res.Miss();

        res = visitor.Initialize(this, in offsets);
        if (res.IsFailure()) return res.Miss();

        return visitor.Find(virtualAddress);
    }

    public Result InvalidateCache()
    {
        Result res = _nodeStorage.OperateRange(OperationId.InvalidateCache, 0, long.MaxValue);
        if (res.IsFailure()) return res.Miss();

        res = _entryStorage.OperateRange(OperationId.InvalidateCache, 0, long.MaxValue);
        if (res.IsFailure()) return res.Miss();

        _offsetCache.IsInitialized = false;
        return Result.Success;
    }

    private Result EnsureOffsetCache()
    {
        if (_offsetCache.IsInitialized)
            return Result.Success;

        using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _offsetCache.Mutex);

        if (_offsetCache.IsInitialized)
            return Result.Success;

        Result res = _nodeStorage.Read(0, _nodeL1.GetBuffer());
        if (res.IsFailure()) return res.Miss();

        res = _nodeL1.GetHeader().Verify(0, _nodeSize, sizeof(long));
        if (res.IsFailure()) return res.Miss();

        BucketTreeNode<long> node = _nodeL1.GetNode<long>();

        long startOffset;
        if (_offsetCount < _entrySetCount && node.GetCount() < _offsetCount)
        {
            startOffset = node.GetL2BeginOffset();
        }
        else
        {
            startOffset = node.GetBeginOffset();
        }

        if (startOffset < 0 || startOffset > node.GetBeginOffset())
            return ResultFs.InvalidBucketTreeEntryOffset.Log();

        long endOffset = node.GetEndOffset();

        if (startOffset >= endOffset)
            return ResultFs.InvalidBucketTreeEntryOffset.Log();

        _offsetCache.IsInitialized = true;
        _offsetCache.Offsets.StartOffset = startOffset;
        _offsetCache.Offsets.EndOffset = endOffset;

        return Result.Success;
    }

    private bool IsExistL2() => _offsetCount < _entrySetCount;
    private bool IsExistOffsetL2OnL1() => IsExistL2() && _nodeL1.GetHeader().EntryCount < _offsetCount;

    private int GetEntrySetIndex(int nodeIndex, int offsetIndex)
    {
        return (_offsetCount - _nodeL1.GetHeader().EntryCount) + (_offsetCount * nodeIndex) + offsetIndex;
    }

    private Result ScanContinuousReading<TEntry>(out ContinuousReadingInfo info,
        in ContinuousReadingParam<TEntry> param) where TEntry : unmanaged, IContinuousReadingEntry
    {
        Assert.SdkRequires(IsInitialized());
        Assert.Equal(Unsafe.SizeOf<TEntry>(), _entrySize);

        info = new ContinuousReadingInfo();

        // If there's nothing to read, we're done.
        if (param.Size == 0)
            return Result.Success;

        // If we're reading a fragment, we're done.
        // IsFragment() is a readonly function, but we can't specify that on interfaces
        // so cast the readonly params to non-readonly
        if (Unsafe.AsRef(in param.Entry).IsFragment())
            return Result.Success;

        // Validate the first entry.
        TEntry entry = param.Entry;
        long currentOffset = param.Offset;

        if (entry.GetVirtualOffset() > currentOffset)
            return ResultFs.OutOfRange.Log();

        // Create a pooled buffer for our scan.
        using var pool = new PooledBuffer((int)_nodeSize, 1);
        var buffer = Span<byte>.Empty;

        Result res = _entryStorage.GetSize(out long entryStorageSize);
        if (res.IsFailure()) return res.Miss();

        // Read the node.
        if (_nodeSize <= pool.GetSize())
        {
            buffer = pool.GetBuffer();
            long ofs = param.EntrySet.Index * _nodeSize;

            if (_nodeSize + ofs > entryStorageSize)
                return ResultFs.InvalidBucketTreeNodeEntryCount.Log();

            res = _entryStorage.Read(ofs, buffer.Slice(0, (int)_nodeSize));
            if (res.IsFailure()) return res.Miss();
        }

        // Calculate extents.
        long endOffset = param.Size + currentOffset;
        long physicalOffset = entry.GetPhysicalOffset();

        // Start merge tracking.
        long mergeSize = 0;
        long readableSize = 0;
        bool merged = false;

        // Iterate.
        int entryIndex = param.EntryIndex;
        int entryCount = param.EntrySet.EntryCount;

        while (entryIndex < entryCount)
        {
            // If we're past the end, we're done.
            if (endOffset <= currentOffset)
                break;

            // Validate the entry offset.
            long entryOffset = entry.GetVirtualOffset();
            if (entryOffset > currentOffset)
                return ResultFs.InvalidIndirectEntryOffset.Log();

            // Get the next entry.
            TEntry nextEntry = default;
            long nextEntryOffset;

            if (entryIndex + 1 < entryCount)
            {
                if (buffer.IsEmpty)
                {
                    long ofs = GetBucketTreeEntryOffset(param.EntrySet.Index, _nodeSize, _entrySize, entryIndex + 1);

                    if (_entrySize + ofs > entryStorageSize)
                        return ResultFs.InvalidBucketTreeEntryOffset.Log();

                    res = _entryStorage.Read(ofs, SpanHelpers.AsByteSpan(ref nextEntry));
                    if (res.IsFailure()) return res.Miss();
                }
                else
                {
                    long ofs = GetBucketTreeEntryOffset(0, _entrySize, entryIndex + 1);
                    buffer.Slice((int)ofs, (int)_entrySize).CopyTo(SpanHelpers.AsByteSpan(ref nextEntry));
                }

                nextEntryOffset = nextEntry.GetVirtualOffset();

                if (!param.TreeOffsets.IsInclude(nextEntryOffset))
                    return ResultFs.InvalidIndirectEntryOffset.Log();
            }
            else
            {
                nextEntryOffset = param.EntrySet.OffsetEnd;
            }

            // Validate the next entry offset.
            if (currentOffset >= nextEntryOffset)
                return ResultFs.InvalidIndirectEntryOffset.Log();

            // Determine the much data there is.
            long dataSize = nextEntryOffset - currentOffset;
            Assert.SdkLess(0, dataSize);

            // Determine how much data we should read.
            long remainingSize = endOffset - currentOffset;
            long readSize = Math.Min(remainingSize, dataSize);
            Assert.SdkLessEqual(readSize, param.Size);

            // Update our merge tracking.
            if (entry.IsFragment())
            {
                // If we can't merge, stop looping.
                if (readSize >= entry.FragmentSizeMax || remainingSize <= dataSize)
                    break;

                // Otherwise, add the current size to the merge size.
                mergeSize += readSize;
            }
            else
            {
                // If we can't merge, stop looping.
                if (physicalOffset != entry.GetPhysicalOffset())
                    break;

                // Add the size to the readable amount.
                readableSize += readSize + mergeSize;
                Assert.SdkLessEqual(readableSize, param.Size);

                // Update whether we've merged.
                merged |= mergeSize > 0;
                mergeSize = 0;
            }

            // Advance.
            currentOffset += readSize;
            Assert.SdkLessEqual(currentOffset, endOffset);

            physicalOffset += nextEntryOffset - entryOffset;
            entry = nextEntry;
            entryIndex++;
        }

        // If we merged, set our readable size.
        if (merged)
        {
            info.SetReadSize(readableSize);
        }

        info.SetSkipCount(entryIndex - param.EntryIndex);

        return Result.Success;
    }

    public ref struct Visitor
    {
        private BucketTree _tree;
        private Offsets _treeOffsets;
        private Buffer _entry;
        private int _entryIndex;
        private int _entrySetCount;
        private EntrySetHeader _entrySet;

        [StructLayout(LayoutKind.Explicit)]
        private struct EntrySetHeader
        {
            // ReSharper disable once MemberHidesStaticFromOuterClass
            [FieldOffset(0)] public NodeHeader Header;
            [FieldOffset(0)] public EntrySetInfo Info;

            [StructLayout(LayoutKind.Sequential)]
            public struct EntrySetInfo
            {
                public int Index;
                public int Count;
                public long End;
                public long Start;
            }
        }

        public Visitor()
        {
            _tree = null;
            _entry = Buffer.Empty;
            _treeOffsets = default;
            _entryIndex = -1;
            _entrySetCount = 0;
            _entrySet = new EntrySetHeader();
        }

        public void Dispose()
        {
            if (!_entry.IsNull)
            {
                _tree.GetAllocator().Deallocate(ref _entry);
                _tree = null;
                _entry = Buffer.Empty;
            }
        }

        /// <summary>
        /// Returns a writable reference to this <see cref="Visitor"/>.
        /// </summary>
        /// <remarks><para>This property allows using a <see langword="using"/> expression with <see cref="Visitor"/>s
        /// while still being able to pass it by reference.</para></remarks>
        /// <returns>A reference to this <see cref="Visitor"/>.</returns>
        [UnscopedRef]
        public ref Visitor Ref => ref this;

        internal Result Initialize(BucketTree tree, in Offsets offsets)
        {
            Assert.SdkRequiresNotNull(tree);
            Assert.SdkRequires(_tree == null || tree == _tree);

            if (_entry.IsNull)
            {
                _entry = tree.GetAllocator().Allocate(tree._entrySize, BufferAlignment);
                if (_entry.IsNull)
                    return ResultFs.BufferAllocationFailed.Log();

                _tree = tree;
                _treeOffsets = offsets;
            }

            return Result.Success;
        }

        public readonly bool IsValid() => _entryIndex >= 0;

        public readonly Offsets GetTreeOffsets() => _treeOffsets;

        public readonly bool CanMoveNext()
        {
            return IsValid() && (_entryIndex + 1 < _entrySet.Info.Count || _entrySet.Info.Index + 1 < _entrySetCount);
        }

        public readonly bool CanMovePrevious()
        {
            return IsValid() && (_entryIndex > 0 || _entrySet.Info.Index > 0);
        }

        public readonly ref readonly T Get<T>() where T : unmanaged
        {
            Assert.SdkRequires(IsValid());

            return ref MemoryMarshal.Cast<byte, T>(_entry.Span)[0];
        }

        internal Result Find(long virtualAddress)
        {
            Assert.SdkRequiresNotNull(_tree);

            Result res;

            // Get the L1 node.
            BucketTreeNode<long> nodeL1 = _tree._nodeL1.GetNode<long>();

            if (virtualAddress >= nodeL1.GetEndOffset())
                return ResultFs.OutOfRange.Log();

            int entrySetIndex;

            if (_tree.IsExistOffsetL2OnL1() && virtualAddress < nodeL1.GetBeginOffset())
            {
                // The portion of the L2 offsets containing our target offset is stored in the L1 node
                ReadOnlySpan<long> offsets = nodeL1.GetArray<long>().Slice(nodeL1.GetCount());

                // Find the index of the entry containing the requested offset.
                // If the value is not found, BinarySearch will return the bitwise complement of the
                // index of the first element that is larger than the value.
                // The offsets are the start offsets of each entry, so subtracting 1 from the index of 
                // the next-largest value will get us the index of the entry containing the offset.
                int index = offsets.BinarySearch(virtualAddress);
                if (index < 0) index = (~index) - 1;

                // If the requested offset comes before the first offset in the list, "index" will be -1.
                if (index < 0)
                    return ResultFs.OutOfRange.Log();

                entrySetIndex = index;
            }
            else
            {
                ReadOnlySpan<long> offsets = nodeL1.GetArray<long>().Slice(0, nodeL1.GetCount());
                int index = offsets.BinarySearch(virtualAddress);
                if (index < 0) index = (~index) - 1;

                if (index < 0)
                    return ResultFs.OutOfRange.Log();

                if (_tree.IsExistL2())
                {
                    if (index >= _tree._offsetCount)
                        return ResultFs.InvalidBucketTreeNodeOffset.Log();

                    res = FindEntrySet(out entrySetIndex, virtualAddress, index);
                    if (res.IsFailure()) return res.Miss();
                }
                else
                {
                    entrySetIndex = index;
                }
            }

            // Validate the entry set index.
            if (entrySetIndex < 0 || entrySetIndex >= _tree._entrySetCount)
                return ResultFs.InvalidBucketTreeNodeOffset.Log();

            // Find the entry.
            res = FindEntry(virtualAddress, entrySetIndex);
            if (res.IsFailure()) return res.Miss();

            // Set count.
            _entrySetCount = _tree._entrySetCount;
            return Result.Success;
        }

        private Result FindEntrySet(out int entrySetIndex, long virtualAddress, int nodeIndex)
        {
            long nodeSize = _tree._nodeSize;

            using var pool = new PooledBuffer((int)nodeSize, 1);

            if (nodeSize <= pool.GetSize())
            {
                return FindEntrySetWithBuffer(out entrySetIndex, virtualAddress, nodeIndex, pool.GetBuffer());
            }
            else
            {
                pool.Deallocate();
                return FindEntrySetWithoutBuffer(out entrySetIndex, virtualAddress, nodeIndex);
            }
        }

        private Result FindEntrySetWithBuffer(out int entrySetIndex, long virtualAddress, int nodeIndex,
            Span<byte> buffer)
        {
            UnsafeHelpers.SkipParamInit(out entrySetIndex);

            // Calculate node extents.
            long nodeSize = _tree._nodeSize;
            long nodeOffset = (nodeIndex + 1) * nodeSize;
            ref ValueSubStorage storage = ref _tree._nodeStorage;

            // Read the node.
            Result res = storage.Read(nodeOffset, buffer.Slice(0, (int)nodeSize));
            if (res.IsFailure()) return res.Miss();

            // Validate the header.
            NodeHeader header = MemoryMarshal.Cast<byte, NodeHeader>(buffer)[0];
            res = header.Verify(nodeIndex, nodeSize, sizeof(long));
            if (res.IsFailure()) return res.Miss();

            // Create the node and find.
            var node = new StorageNode(sizeof(long), header.EntryCount);
            node.Find(buffer, virtualAddress);

            if (node.GetIndex() < 0)
                return ResultFs.InvalidBucketTreeVirtualOffset.Log();

            // Return the index.
            entrySetIndex = _tree.GetEntrySetIndex(header.Index, node.GetIndex());
            return Result.Success;
        }

        private Result FindEntrySetWithoutBuffer(out int outIndex, long virtualAddress, int nodeIndex)
        {
            UnsafeHelpers.SkipParamInit(out outIndex);

            // Calculate node extents.
            long nodeSize = _tree._nodeSize;
            long nodeOffset = nodeSize * (nodeIndex + 1);
            ref ValueSubStorage storage = ref _tree._nodeStorage;

            // Read and validate the header.
            Unsafe.SkipInit(out NodeHeader header);
            Result res = storage.Read(nodeOffset, SpanHelpers.AsByteSpan(ref header));
            if (res.IsFailure()) return res.Miss();

            res = header.Verify(nodeIndex, nodeSize, sizeof(long));
            if (res.IsFailure()) return res.Miss();

            // Create the node, and find.
            var node = new StorageNode(nodeOffset, sizeof(long), header.EntryCount);
            res = node.Find(in storage, virtualAddress);
            if (res.IsFailure()) return res.Miss();

            if (node.GetIndex() < 0)
                return ResultFs.InvalidBucketTreeVirtualOffset.Log();

            // Return the index.
            outIndex = _tree.GetEntrySetIndex(header.Index, node.GetIndex());
            return Result.Success;
        }

        private Result FindEntry(long virtualAddress, int entrySetIndex)
        {
            long entrySetSize = _tree._nodeSize;

            using var pool = new PooledBuffer((int)entrySetSize, 1);

            if (entrySetSize <= pool.GetSize())
            {
                return FindEntryWithBuffer(virtualAddress, entrySetIndex, pool.GetBuffer());
            }
            else
            {
                pool.Deallocate();
                return FindEntryWithoutBuffer(virtualAddress, entrySetIndex);
            }
        }

        private Result FindEntryWithBuffer(long virtualAddress, int entrySetIndex, Span<byte> buffer)
        {
            // Calculate entry set extents.
            long entrySize = _tree._entrySize;
            long entrySetSize = _tree._nodeSize;
            long entrySetOffset = entrySetIndex * entrySetSize;
            ref ValueSubStorage storage = ref _tree._entryStorage;

            // Read the entry set.
            Result res = storage.Read(entrySetOffset, buffer.Slice(0, (int)entrySetSize));
            if (res.IsFailure()) return res.Miss();

            // Validate the entry set.
            EntrySetHeader entrySet = MemoryMarshal.Cast<byte, EntrySetHeader>(buffer)[0];
            res = entrySet.Header.Verify(entrySetIndex, entrySetSize, entrySize);
            if (res.IsFailure()) return res.Miss();

            // Create the node, and find.
            var node = new StorageNode(entrySize, entrySet.Info.Count);
            node.Find(buffer, virtualAddress);

            if (node.GetIndex() < 0)
                return ResultFs.InvalidBucketTreeVirtualOffset.Log();

            // Copy the data into entry.
            int entryIndex = node.GetIndex();
            long entryOffset = GetBucketTreeEntryOffset(0, entrySize, entryIndex);
            buffer.Slice((int)entryOffset, (int)entrySize).CopyTo(_entry.Span);

            // Set our entry set/index.
            _entrySet = entrySet;
            _entryIndex = entryIndex;

            return Result.Success;
        }

        private Result FindEntryWithoutBuffer(long virtualAddress, int entrySetIndex)
        {
            // Calculate entry set extents.
            long entrySize = _tree._entrySize;
            long entrySetSize = _tree._nodeSize;
            long entrySetOffset = entrySetSize * entrySetIndex;
            ref ValueSubStorage storage = ref _tree._entryStorage;

            // Read and validate the entry set.
            Unsafe.SkipInit(out EntrySetHeader entrySet);
            Result res = storage.Read(entrySetOffset, SpanHelpers.AsByteSpan(ref entrySet));
            if (res.IsFailure()) return res.Miss();

            res = entrySet.Header.Verify(entrySetIndex, entrySetSize, entrySize);
            if (res.IsFailure()) return res.Miss();

            // Create the node, and find.
            var node = new StorageNode(entrySetOffset, entrySize, entrySet.Info.Count);
            res = node.Find(in storage, virtualAddress);
            if (res.IsFailure()) return res.Miss();

            if (node.GetIndex() < 0)
                return ResultFs.InvalidBucketTreeVirtualOffset.Log();

            // Copy the data into entry.
            _entryIndex = -1;
            int entryIndex = node.GetIndex();
            long entryOffset = GetBucketTreeEntryOffset(entrySetOffset, entrySize, entryIndex);

            res = storage.Read(entryOffset, _entry.Span);
            if (res.IsFailure()) return res.Miss();

            // Set our entry set/index.
            _entrySet = entrySet;
            _entryIndex = entryIndex;

            return Result.Success;
        }

        public Result MoveNext()
        {
            Result res;

            if (!IsValid())
                return ResultFs.OutOfRange.Log();

            int entryIndex = _entryIndex + 1;

            // Invalidate our index, and read the header for the next index.
            if (entryIndex == _entrySet.Info.Count)
            {
                int entrySetIndex = _entrySet.Info.Index + 1;
                if (entrySetIndex >= _entrySetCount)
                    return ResultFs.OutOfRange.Log();

                _entryIndex = -1;

                long end = _entrySet.Info.End;

                long entrySetSize = _tree._nodeSize;
                long entrySetOffset = entrySetIndex * entrySetSize;

                res = _tree._entryStorage.Read(entrySetOffset, SpanHelpers.AsByteSpan(ref _entrySet));
                if (res.IsFailure()) return res.Miss();

                res = _entrySet.Header.Verify(entrySetIndex, entrySetSize, _tree._entrySize);
                if (res.IsFailure()) return res.Miss();

                if (_entrySet.Info.Start != end || _entrySet.Info.Start >= _entrySet.Info.End)
                    return ResultFs.InvalidBucketTreeEntrySetOffset.Log();

                entryIndex = 0;
            }
            else
            {
                _entryIndex = 1;
            }

            // Read the new entry
            long entrySize = _tree._entrySize;
            long entryOffset = GetBucketTreeEntryOffset(_entrySet.Info.Index, _tree._nodeSize, entrySize, entryIndex);

            res = _tree._entryStorage.Read(entryOffset, _entry.Span);
            if (res.IsFailure()) return res.Miss();

            // Note that we changed index.
            _entryIndex = entryIndex;
            return Result.Success;
        }

        public Result MovePrevious()
        {
            Result res;

            if (!IsValid())
                return ResultFs.OutOfRange.Log();

            int entryIndex = _entryIndex;

            if (entryIndex == 0)
            {
                if (_entrySet.Info.Index <= 0)
                    return ResultFs.OutOfRange.Log();

                _entryIndex = -1;

                long start = _entrySet.Info.Start;

                long entrySetSize = _tree._nodeSize;
                int entrySetIndex = _entrySet.Info.Index - 1;
                long entrySetOffset = entrySetIndex * entrySetSize;

                res = _tree._entryStorage.Read(entrySetOffset, SpanHelpers.AsByteSpan(ref _entrySet));
                if (res.IsFailure()) return res.Miss();

                res = _entrySet.Header.Verify(entrySetIndex, entrySetSize, _tree._entrySize);
                if (res.IsFailure()) return res.Miss();

                if (_entrySet.Info.End != start || _entrySet.Info.Start >= _entrySet.Info.End)
                    return ResultFs.InvalidBucketTreeEntrySetOffset.Log();

                entryIndex = _entrySet.Info.Count;
            }
            else
            {
                _entryIndex = -1;
            }

            entryIndex--;

            // Read the new entry
            long entrySize = _tree._entrySize;
            long entryOffset = GetBucketTreeEntryOffset(_entrySet.Info.Index, _tree._nodeSize, entrySize, entryIndex);

            res = _tree._entryStorage.Read(entryOffset, _entry.Span);
            if (res.IsFailure()) return res.Miss();

            // Note that we changed index.
            _entryIndex = entryIndex;
            return Result.Success;
        }

        public readonly Result ScanContinuousReading<TEntry>(out ContinuousReadingInfo info, long offset, long size)
            where TEntry : unmanaged, IContinuousReadingEntry
        {
            var param = new ContinuousReadingParam<TEntry>
            {
                Offset = offset,
                Size = size,
                EntrySet = _entrySet.Header,
                EntryIndex = _entryIndex,
                TreeOffsets = _treeOffsets
            };

            _entry.Span.CopyTo(SpanHelpers.AsByteSpan(ref param.Entry));

            return _tree.ScanContinuousReading(out info, in param);
        }
    }
}