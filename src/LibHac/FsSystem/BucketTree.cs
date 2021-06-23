using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Util;

namespace LibHac.FsSystem
{
    public partial class BucketTree
    {
        private const uint ExpectedMagic = 0x52544B42; // BKTR
        private const int MaxVersion = 1;

        private const int NodeSizeMin = 1024;
        private const int NodeSizeMax = 1024 * 512;

        private static int NodeHeaderSize => Unsafe.SizeOf<NodeHeader>();

        private SubStorage NodeStorage { get; set; }
        private SubStorage EntryStorage { get; set; }

        private NodeBuffer _nodeL1;

        private long NodeSize { get; set; }
        private long EntrySize { get; set; }
        private int OffsetCount { get; set; }
        private int EntrySetCount { get; set; }
        private long StartOffset { get; set; }
        private long EndOffset { get; set; }

        public Result Initialize(SubStorage nodeStorage, SubStorage entryStorage, int nodeSize, int entrySize,
            int entryCount)
        {
            Assert.SdkRequiresLessEqual(sizeof(long), entrySize);
            Assert.SdkRequiresLessEqual(entrySize + Unsafe.SizeOf<NodeHeader>(), nodeSize);
            Assert.SdkRequiresWithinMinMax(nodeSize, NodeSizeMin, NodeSizeMax);
            Assert.SdkRequires(BitUtil.IsPowerOfTwo(nodeSize));
            Assert.SdkRequires(!IsInitialized());

            // Ensure valid entry count.
            if (entryCount <= 0)
                return ResultFs.InvalidArgument.Log();

            // Allocate node.
            if (!_nodeL1.Allocate(nodeSize))
                return ResultFs.BufferAllocationFailed.Log();

            bool needFree = true;
            try
            {
                // Read node.
                Result rc = nodeStorage.Read(0, _nodeL1.GetBuffer());
                if (rc.IsFailure()) return rc;

                // Verify node.
                rc = _nodeL1.GetHeader().Verify(0, nodeSize, sizeof(long));
                if (rc.IsFailure()) return rc;

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

                NodeStorage = nodeStorage;
                EntryStorage = entryStorage;
                NodeSize = nodeSize;
                EntrySize = entrySize;
                OffsetCount = offsetCount;
                EntrySetCount = entrySetCount;
                StartOffset = startOffset;
                EndOffset = endOffset;

                needFree = false;

                return Result.Success;
            }
            finally
            {
                if (needFree)
                    _nodeL1.Free();
            }
        }

        public bool IsInitialized() => NodeSize > 0;
        public bool IsEmpty() => EntrySize == 0;

        public long GetStart() => StartOffset;
        public long GetEnd() => EndOffset;
        public long GetSize() => EndOffset - StartOffset;

        public bool Includes(long offset)
        {
            return StartOffset <= offset && offset < EndOffset;
        }

        public bool Includes(long offset, long size)
        {
            return size > 0 && StartOffset <= offset && size <= EndOffset - offset;
        }

        public Result Find(ref Visitor visitor, long virtualAddress)
        {
            Assert.SdkRequires(IsInitialized());

            if (virtualAddress < 0)
                return ResultFs.InvalidOffset.Log();

            if (IsEmpty())
                return ResultFs.OutOfRange.Log();

            Result rc = visitor.Initialize(this);
            if (rc.IsFailure()) return rc;

            return visitor.Find(virtualAddress);
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

        public static int GetNodeL2Count(long nodeSize, long entrySize, int entryCount)
        {
            int offsetCountPerNode = GetOffsetCount(nodeSize);
            int entrySetCount = GetEntrySetCount(nodeSize, entrySize, entryCount);

            if (entrySetCount <= offsetCountPerNode)
                return 0;

            int nodeL2Count = BitUtil.DivideUp(entrySetCount, offsetCountPerNode);
            Abort.DoAbortUnless(nodeL2Count <= offsetCountPerNode);

            return BitUtil.DivideUp(entrySetCount - (offsetCountPerNode - (nodeL2Count - 1)), offsetCountPerNode);
        }

        private static long GetBucketTreeEntryOffset(long entrySetOffset, long entrySize, int entryIndex)
        {
            return entrySetOffset + Unsafe.SizeOf<NodeHeader>() + entryIndex * entrySize;
        }

        private static long GetBucketTreeEntryOffset(int entrySetIndex, long nodeSize, long entrySize, int entryIndex)
        {
            return GetBucketTreeEntryOffset(entrySetIndex * nodeSize, entrySize, entryIndex);
        }

        private bool IsExistL2() => OffsetCount < EntrySetCount;
        private bool IsExistOffsetL2OnL1() => IsExistL2() && _nodeL1.GetHeader().Count < OffsetCount;

        private long GetEntrySetIndex(int nodeIndex, int offsetIndex)
        {
            return (OffsetCount - _nodeL1.GetHeader().Count) + (OffsetCount * nodeIndex) + offsetIndex;
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
                Magic = ExpectedMagic;
                Version = MaxVersion;
                EntryCount = entryCount;
                _reserved = 0;
            }

            public Result Verify()
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

        public struct NodeHeader
        {
            public int Index;
            public int Count;
            public long Offset;

            public Result Verify(int nodeIndex, long nodeSize, long entrySize)
            {
                if (Index != nodeIndex)
                    return ResultFs.InvalidBucketTreeNodeIndex.Log();

                if (entrySize == 0 || nodeSize < entrySize + NodeHeaderSize)
                    return ResultFs.InvalidSize.Log();

                long maxEntryCount = (nodeSize - NodeHeaderSize) / entrySize;

                if (Count <= 0 || maxEntryCount < Count)
                    return ResultFs.InvalidBucketTreeNodeEntryCount.Log();

                if (Offset < 0)
                    return ResultFs.InvalidBucketTreeNodeOffset.Log();

                return Result.Success;
            }
        }

        private struct NodeBuffer
        {
            // Use long to ensure alignment
            private long[] _header;

            public bool Allocate(int nodeSize)
            {
                Assert.SdkRequiresNull(_header);

                _header = new long[nodeSize / sizeof(long)];

                return _header != null;
            }

            public void Free()
            {
                _header = null;
            }

            public void FillZero()
            {
                if (_header != null)
                {
                    Array.Fill(_header, 0);
                }
            }

            public ref NodeHeader GetHeader()
            {
                Assert.SdkRequiresGreaterEqual(_header.Length / sizeof(long), Unsafe.SizeOf<NodeHeader>());

                return ref Unsafe.As<long, NodeHeader>(ref _header[0]);
            }

            public Span<byte> GetBuffer()
            {
                return MemoryMarshal.AsBytes(_header.AsSpan());
            }

            public BucketTreeNode<TEntry> GetNode<TEntry>() where TEntry : unmanaged
            {
                return new BucketTreeNode<TEntry>(GetBuffer());
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
                    Unsafe.SizeOf<NodeHeader>() + GetHeader().Count * Unsafe.SizeOf<TEntry>());
            }

            public int GetCount() => GetHeader().Count;

            public ReadOnlySpan<TEntry> GetArray() => GetWritableArray();
            internal Span<TEntry> GetWritableArray() => GetWritableArray<TEntry>();

            public long GetBeginOffset() => GetArray<long>()[0];
            public long GetEndOffset() => GetHeader().Offset;
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

        public ref struct Visitor
        {
            private BucketTree Tree { get; set; }
            private byte[] Entry { get; set; }
            private int EntryIndex { get; set; }
            private int EntrySetCount { get; set; }
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

            public Result Initialize(BucketTree tree)
            {
                Assert.SdkRequiresNotNull(tree);
                Assert.SdkRequires(Tree == null || tree == Tree);

                if (Entry == null)
                {
                    Entry = ArrayPool<byte>.Shared.Rent((int)tree.EntrySize);
                    Tree = tree;
                    EntryIndex = -1;
                }

                return Result.Success;
            }

            public void Dispose()
            {
                if (Entry != null)
                {
                    ArrayPool<byte>.Shared.Return(Entry);
                    Entry = null;
                }
            }

            public bool IsValid() => EntryIndex >= 0;

            public bool CanMoveNext()
            {
                return IsValid() && (EntryIndex + 1 < _entrySet.Info.Count || _entrySet.Info.Index + 1 < EntrySetCount);
            }

            public bool CanMovePrevious()
            {
                return IsValid() && (EntryIndex > 0 || _entrySet.Info.Index > 0);
            }

            public ref T Get<T>() where T : unmanaged
            {
                return ref MemoryMarshal.Cast<byte, T>(Entry)[0];
            }

            public Result MoveNext()
            {
                Result rc;

                if (!IsValid())
                    return ResultFs.OutOfRange.Log();

                int entryIndex = EntryIndex + 1;

                // Invalidate our index, and read the header for the next index.
                if (entryIndex == _entrySet.Info.Count)
                {
                    int entrySetIndex = _entrySet.Info.Index + 1;
                    if (entrySetIndex >= EntrySetCount)
                        return ResultFs.OutOfRange.Log();

                    EntryIndex = -1;

                    long end = _entrySet.Info.End;

                    long entrySetSize = Tree.NodeSize;
                    long entrySetOffset = entrySetIndex * entrySetSize;

                    rc = Tree.EntryStorage.Read(entrySetOffset, SpanHelpers.AsByteSpan(ref _entrySet));
                    if (rc.IsFailure()) return rc;

                    rc = _entrySet.Header.Verify(entrySetIndex, entrySetSize, Tree.EntrySize);
                    if (rc.IsFailure()) return rc;

                    if (_entrySet.Info.Start != end || _entrySet.Info.Start >= _entrySet.Info.End)
                        return ResultFs.InvalidBucketTreeEntrySetOffset.Log();

                    entryIndex = 0;
                }
                else
                {
                    EntryIndex = 1;
                }

                // Read the new entry
                long entrySize = Tree.EntrySize;
                long entryOffset = GetBucketTreeEntryOffset(_entrySet.Info.Index, Tree.NodeSize, entrySize, entryIndex);

                rc = Tree.EntryStorage.Read(entryOffset, Entry);
                if (rc.IsFailure()) return rc;

                // Note that we changed index.
                EntryIndex = entryIndex;
                return Result.Success;
            }

            public Result MovePrevious()
            {
                Result rc;

                if (!IsValid())
                    return ResultFs.OutOfRange.Log();

                int entryIndex = EntryIndex;

                if (entryIndex == 0)
                {
                    if (_entrySet.Info.Index <= 0)
                        return ResultFs.OutOfRange.Log();

                    EntryIndex = -1;

                    long start = _entrySet.Info.Start;

                    long entrySetSize = Tree.NodeSize;
                    int entrySetIndex = _entrySet.Info.Index - 1;
                    long entrySetOffset = entrySetIndex * entrySetSize;

                    rc = Tree.EntryStorage.Read(entrySetOffset, SpanHelpers.AsByteSpan(ref _entrySet));
                    if (rc.IsFailure()) return rc;

                    rc = _entrySet.Header.Verify(entrySetIndex, entrySetSize, Tree.EntrySize);
                    if (rc.IsFailure()) return rc;

                    if (_entrySet.Info.End != start || _entrySet.Info.Start >= _entrySet.Info.End)
                        return ResultFs.InvalidBucketTreeEntrySetOffset.Log();

                    entryIndex = _entrySet.Info.Count;
                }
                else
                {
                    EntryIndex = -1;
                }

                // Read the new entry
                long entrySize = Tree.EntrySize;
                long entryOffset = GetBucketTreeEntryOffset(_entrySet.Info.Index, Tree.NodeSize, entrySize, entryIndex);

                rc = Tree.EntryStorage.Read(entryOffset, Entry);
                if (rc.IsFailure()) return rc;

                // Note that we changed index.
                EntryIndex = entryIndex;
                return Result.Success;
            }

            public Result Find(long virtualAddress)
            {
                Result rc;

                // Get the node.
                BucketTreeNode<long> node = Tree._nodeL1.GetNode<long>();

                if (virtualAddress >= node.GetEndOffset())
                    return ResultFs.OutOfRange.Log();

                int entrySetIndex;

                if (Tree.IsExistOffsetL2OnL1() && virtualAddress < node.GetBeginOffset())
                {
                    // The portion of the L2 offsets containing our target offset is stored in the L1 node
                    ReadOnlySpan<long> offsets = node.GetArray<long>().Slice(node.GetCount());
                    int index = offsets.BinarySearch(virtualAddress);
                    if (index < 0) index = (~index) - 1;

                    if (index < 0)
                        return ResultFs.OutOfRange.Log();

                    entrySetIndex = index;
                }
                else
                {
                    ReadOnlySpan<long> offsets = node.GetArray<long>().Slice(0, node.GetCount());
                    int index = offsets.BinarySearch(virtualAddress);
                    if (index < 0) index = (~index) - 1;

                    if (index < 0)
                        return ResultFs.OutOfRange.Log();

                    if (Tree.IsExistL2())
                    {
                        if (index >= Tree.OffsetCount)
                            return ResultFs.InvalidBucketTreeNodeOffset.Log();

                        rc = FindEntrySet(out entrySetIndex, virtualAddress, index);
                        if (rc.IsFailure()) return rc;
                    }
                    else
                    {
                        entrySetIndex = index;
                    }
                }

                // Validate the entry set index.
                if (entrySetIndex < 0 || entrySetIndex >= Tree.EntrySetCount)
                    return ResultFs.InvalidBucketTreeNodeOffset.Log();

                // Find the entry.
                rc = FindEntry(virtualAddress, entrySetIndex);
                if (rc.IsFailure()) return rc;

                // Set count.
                EntrySetCount = Tree.EntrySetCount;
                return Result.Success;
            }

            private Result FindEntrySet(out int entrySetIndex, long virtualAddress, int nodeIndex)
            {
                long nodeSize = Tree.NodeSize;

                using (var rented = new RentedArray<byte>((int)nodeSize))
                {
                    return FindEntrySetWithBuffer(out entrySetIndex, virtualAddress, nodeIndex, rented.Span);
                }
            }

            private Result FindEntrySetWithBuffer(out int outIndex, long virtualAddress, int nodeIndex,
                Span<byte> buffer)
            {
                UnsafeHelpers.SkipParamInit(out outIndex);

                // Calculate node extents.
                long nodeSize = Tree.NodeSize;
                long nodeOffset = (nodeIndex + 1) * nodeSize;
                SubStorage storage = Tree.NodeStorage;

                // Read the node.
                Result rc = storage.Read(nodeOffset, buffer.Slice(0, (int)nodeSize));
                if (rc.IsFailure()) return rc;

                // Validate the header.
                NodeHeader header = MemoryMarshal.Cast<byte, NodeHeader>(buffer)[0];
                rc = header.Verify(nodeIndex, nodeSize, sizeof(long));
                if (rc.IsFailure()) return rc;

                // Create the node and find.
                var node = new StorageNode(sizeof(long), header.Count);
                node.Find(buffer, virtualAddress);

                if (node.GetIndex() < 0)
                    return ResultFs.InvalidBucketTreeVirtualOffset.Log();

                // Return the index.
                outIndex = (int)Tree.GetEntrySetIndex(header.Index, node.GetIndex());
                return Result.Success;
            }

            private Result FindEntry(long virtualAddress, int entrySetIndex)
            {
                long entrySetSize = Tree.NodeSize;

                using (var rented = new RentedArray<byte>((int)entrySetSize))
                {
                    return FindEntryWithBuffer(virtualAddress, entrySetIndex, rented.Span);
                }
            }

            private Result FindEntryWithBuffer(long virtualAddress, int entrySetIndex, Span<byte> buffer)
            {
                // Calculate entry set extents.
                long entrySize = Tree.EntrySize;
                long entrySetSize = Tree.NodeSize;
                long entrySetOffset = entrySetIndex * entrySetSize;
                SubStorage storage = Tree.EntryStorage;

                // Read the entry set.
                Result rc = storage.Read(entrySetOffset, buffer.Slice(0, (int)entrySetSize));
                if (rc.IsFailure()) return rc;

                // Validate the entry set.
                EntrySetHeader entrySet = MemoryMarshal.Cast<byte, EntrySetHeader>(buffer)[0];
                rc = entrySet.Header.Verify(entrySetIndex, entrySetSize, entrySize);
                if (rc.IsFailure()) return rc;

                // Create the node, and find.
                var node = new StorageNode(entrySize, entrySet.Info.Count);
                node.Find(buffer, virtualAddress);

                if (node.GetIndex() < 0)
                    return ResultFs.InvalidBucketTreeVirtualOffset.Log();

                // Copy the data into entry.
                int entryIndex = node.GetIndex();
                long entryOffset = GetBucketTreeEntryOffset(0, entrySize, entryIndex);
                buffer.Slice((int)entryOffset, (int)entrySize).CopyTo(Entry);

                // Set our entry set/index.
                _entrySet = entrySet;
                EntryIndex = entryIndex;

                return Result.Success;
            }

            private struct StorageNode
            {
                private Offset _start;
                private int _count;
                private int _index;

                public StorageNode(long size, int count)
                {
                    _start = new Offset(NodeHeaderSize, (int)size);
                    _count = count;
                    _index = -1;
                }

                public int GetIndex() => _index;

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
        }
    }
}
