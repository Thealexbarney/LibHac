using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Util;

namespace LibHac.FsSystem;

public partial class BucketTree
{
    public class Builder
    {
        private NodeBuffer _l1Node;
        private NodeBuffer _l2Node;
        private NodeBuffer _entrySet;

        private ValueSubStorage _nodeStorage;
        private ValueSubStorage _entryStorage;

        private int _nodeSize;
        private int _entrySize;
        private int _entryCount;
        private int _entriesPerEntrySet;
        private int _offsetsPerNode;

        private int _currentL2OffsetIndex;
        private int _currentEntryIndex;
        private long _currentOffset;

        public Builder()
        {
            _currentOffset = -1;
        }

        /// <summary>
        /// Initializes the bucket tree builder.
        /// </summary>
        /// <param name="allocator">The <see cref="MemoryResource"/> to use for buffer allocation.</param>
        /// <param name="headerStorage">The <see cref="ValueSubStorage"/> the tree's header will be written to.Must be at least the size in bytes returned by <see cref="QueryHeaderStorageSize"/>.</param>
        /// <param name="nodeStorage">The <see cref="ValueSubStorage"/> the tree's nodes will be written to. Must be at least the size in bytes returned by <see cref="QueryNodeStorageSize"/>.</param>
        /// <param name="entryStorage">The <see cref="ValueSubStorage"/> the tree's entries will be written to. Must be at least the size in bytes returned by <see cref="QueryEntryStorageSize"/>.</param>
        /// <param name="nodeSize">The size of each node in the bucket tree. Must be a power of 2.</param>
        /// <param name="entrySize">The size of each entry that will be stored in the bucket tree.</param>
        /// <param name="entryCount">The exact number of entries that will be added to the bucket tree.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        public Result Initialize(MemoryResource allocator, in ValueSubStorage headerStorage,
            in ValueSubStorage nodeStorage, in ValueSubStorage entryStorage, int nodeSize, int entrySize,
            int entryCount)
        {
            Assert.NotNull(allocator);
            Assert.SdkRequiresLessEqual(sizeof(long), entrySize);
            Assert.SdkRequiresLessEqual(entrySize + Unsafe.SizeOf<NodeHeader>(), nodeSize);
            Assert.SdkRequiresWithinMinMax(nodeSize, NodeSizeMin, NodeSizeMax);
            Assert.SdkRequires(BitUtil.IsPowerOfTwo(nodeSize));

            // Set the builder parameters
            _nodeSize = nodeSize;
            _entrySize = entrySize;
            _entryCount = entryCount;

            _entriesPerEntrySet = GetEntryCount(nodeSize, entrySize);
            _offsetsPerNode = GetOffsetCount(nodeSize);
            _currentL2OffsetIndex = GetNodeL2Count(nodeSize, entrySize, entryCount);

            // Create and write the header
            var header = new Header();
            header.Format(entryCount);
            Result res = headerStorage.Write(0, SpanHelpers.AsByteSpan(ref header));
            if (res.IsFailure()) return res.Miss();

            // Allocate buffers for the L1 node and entry sets
            _l1Node.Allocate(allocator, nodeSize);
            _entrySet.Allocate(allocator, nodeSize);

            int entrySetCount = GetEntrySetCount(nodeSize, entrySize, entryCount);

            // Allocate an L2 node buffer if there are more entry sets than will fit in the L1 node
            if (_offsetsPerNode < entrySetCount)
            {
                _l2Node.Allocate(allocator, nodeSize);
            }

            _l1Node.FillZero();
            _l2Node.FillZero();
            _entrySet.FillZero();

            _nodeStorage.Set(in nodeStorage);
            _entryStorage.Set(in entryStorage);

            // Set the initial position
            _currentEntryIndex = 0;
            _currentOffset = -1;

            return Result.Success;
        }

        /// <summary>
        /// Adds a new entry to the bucket tree.
        /// </summary>
        /// <typeparam name="T">The type of the entry to add. Added entries should all be the same type.</typeparam>
        /// <param name="entry">The entry to add.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        public Result Add<T>(in T entry) where T : unmanaged
        {
            Assert.SdkRequiresEqual(Unsafe.SizeOf<T>(), _entrySize);

            if (_currentEntryIndex >= _entryCount)
                return ResultFs.OutOfRange.Log();

            // The entry offset must always be the first 8 bytes of the struct
            long entryOffset = BinaryPrimitives.ReadInt64LittleEndian(SpanHelpers.AsReadOnlyByteSpan(in entry));

            if (entryOffset <= _currentOffset)
                return ResultFs.InvalidOffset.Log();

            Result res = FinalizePreviousEntrySet(entryOffset);
            if (res.IsFailure()) return res.Miss();

            AddEntryOffset(entryOffset);

            // Write the new entry
            int indexInEntrySet = _currentEntryIndex % _entriesPerEntrySet;
            _entrySet.GetNode<T>().GetWritableArray()[indexInEntrySet] = entry;

            _currentOffset = entryOffset;
            _currentEntryIndex++;

            return Result.Success;
        }

        /// <summary>
        /// Checks if a new entry set is being started. If so, sets the end offset of the previous
        /// entry set and writes it to the output storage.
        /// </summary>
        /// <param name="endOffset">The end offset of the previous entry.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        private Result FinalizePreviousEntrySet(long endOffset)
        {
            int prevEntrySetIndex = _currentEntryIndex / _entriesPerEntrySet - 1;
            int indexInEntrySet = _currentEntryIndex % _entriesPerEntrySet;

            // If the previous Add finished an entry set
            if (_currentEntryIndex > 0 && indexInEntrySet == 0)
            {
                // Set the end offset of that entry set
                ref NodeHeader entrySetHeader = ref _entrySet.GetHeader();

                entrySetHeader.Index = prevEntrySetIndex;
                entrySetHeader.EntryCount = _entriesPerEntrySet;
                entrySetHeader.OffsetEnd = endOffset;

                // Write the entry set to the entry storage
                long storageOffset = (long)_nodeSize * prevEntrySetIndex;
                Result res = _entryStorage.Write(storageOffset, _entrySet.GetBuffer());
                if (res.IsFailure()) return res.Miss();

                // Clear the entry set buffer to begin the new entry set
                _entrySet.FillZero();

                // Check if we're writing in L2 nodes
                if (_currentL2OffsetIndex > _offsetsPerNode)
                {
                    int prevL2NodeIndex = _currentL2OffsetIndex / _offsetsPerNode - 2;
                    int indexInL2Node = _currentL2OffsetIndex % _offsetsPerNode;

                    // If the previous Add finished an L2 node
                    if (indexInL2Node == 0)
                    {
                        // Set the end offset of that node
                        ref NodeHeader l2NodeHeader = ref _l2Node.GetHeader();

                        l2NodeHeader.Index = prevL2NodeIndex;
                        l2NodeHeader.EntryCount = _offsetsPerNode;
                        l2NodeHeader.OffsetEnd = endOffset;

                        // Write the L2 node to the node storage
                        long nodeOffset = (long)_nodeSize * (prevL2NodeIndex + 1);
                        res = _nodeStorage.Write(nodeOffset, _l2Node.GetBuffer());
                        if (res.IsFailure()) return res.Miss();

                        // Clear the L2 node buffer to begin the new node
                        _l2Node.FillZero();
                    }
                }
            }

            return Result.Success;
        }

        /// <summary>
        /// If needed, adds a new entry set's start offset to the L1 or L2 nodes.
        /// </summary>
        /// <param name="entryOffset">The start offset of the entry being added.</param>
        private void AddEntryOffset(long entryOffset)
        {
            int entrySetIndex = _currentEntryIndex / _entriesPerEntrySet;
            int indexInEntrySet = _currentEntryIndex % _entriesPerEntrySet;

            // If we're starting a new entry set we need to add its start offset to the L1/L2 nodes
            if (indexInEntrySet == 0)
            {
                Span<long> l1Data = _l1Node.GetNode<long>().GetWritableArray();

                if (_currentL2OffsetIndex == 0)
                {
                    // There are no L2 nodes. Write the entry set end offset directly to L1
                    l1Data[entrySetIndex] = entryOffset;
                }
                else
                {
                    if (_currentL2OffsetIndex < _offsetsPerNode)
                    {
                        // The current L2 offset is stored in the L1 node
                        l1Data[_currentL2OffsetIndex] = entryOffset;
                    }
                    else
                    {
                        // Write the entry set offset to the current L2 node
                        int l2NodeIndex = _currentL2OffsetIndex / _offsetsPerNode;
                        int indexInL2Node = _currentL2OffsetIndex % _offsetsPerNode;

                        Span<long> l2Data = _l2Node.GetNode<long>().GetWritableArray();
                        l2Data[indexInL2Node] = entryOffset;

                        // If we're starting a new L2 node we need to add its start offset to the L1 node
                        if (indexInL2Node == 0)
                        {
                            l1Data[l2NodeIndex - 1] = entryOffset;
                        }
                    }

                    _currentL2OffsetIndex++;
                }
            }
        }

        /// <summary>
        /// Finalizes the bucket tree. Must be called after all entries are added.
        /// </summary>
        /// <param name="endOffset">The end offset of the bucket tree.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        public Result Finalize(long endOffset)
        {
            // Finalize must only be called after all entries are added
            if (_entryCount != _currentEntryIndex)
                return ResultFs.OutOfRange.Log();

            if (endOffset <= _currentOffset)
                return ResultFs.InvalidOffset.Log();

            if (_currentOffset == -1)
                return Result.Success;

            Result res = FinalizePreviousEntrySet(endOffset);
            if (res.IsFailure()) return res.Miss();

            int entrySetIndex = _currentEntryIndex / _entriesPerEntrySet;
            int indexInEntrySet = _currentEntryIndex % _entriesPerEntrySet;

            // Finalize the current entry set if needed
            if (indexInEntrySet != 0)
            {
                ref NodeHeader entrySetHeader = ref _entrySet.GetHeader();

                entrySetHeader.Index = entrySetIndex;
                entrySetHeader.EntryCount = indexInEntrySet;
                entrySetHeader.OffsetEnd = endOffset;

                long entryStorageOffset = (long)_nodeSize * entrySetIndex;
                res = _entryStorage.Write(entryStorageOffset, _entrySet.GetBuffer());
                if (res.IsFailure()) return res.Miss();
            }

            int l2NodeIndex = BitUtil.DivideUp(_currentL2OffsetIndex, _offsetsPerNode) - 2;
            int indexInL2Node = _currentL2OffsetIndex % _offsetsPerNode;

            // Finalize the current L2 node if needed
            if (_currentL2OffsetIndex > _offsetsPerNode && (indexInEntrySet != 0 || indexInL2Node != 0))
            {
                ref NodeHeader l2NodeHeader = ref _l2Node.GetHeader();
                l2NodeHeader.Index = l2NodeIndex;
                l2NodeHeader.EntryCount = indexInL2Node != 0 ? indexInL2Node : _offsetsPerNode;
                l2NodeHeader.OffsetEnd = endOffset;

                long l2NodeStorageOffset = _nodeSize * (l2NodeIndex + 1);
                res = _nodeStorage.Write(l2NodeStorageOffset, _l2Node.GetBuffer());
                if (res.IsFailure()) return res.Miss();
            }

            // Finalize the L1 node
            ref NodeHeader l1NodeHeader = ref _l1Node.GetHeader();
            l1NodeHeader.Index = 0;
            l1NodeHeader.OffsetEnd = endOffset;

            // L1 count depends on the existence or absence of L2 nodes
            if (_currentL2OffsetIndex == 0)
            {
                l1NodeHeader.EntryCount = BitUtil.DivideUp(_currentEntryIndex, _entriesPerEntrySet);
            }
            else
            {
                l1NodeHeader.EntryCount = l2NodeIndex + 1;
            }

            res = _nodeStorage.Write(0, _l1Node.GetBuffer());
            if (res.IsFailure()) return res.Miss();

            _currentOffset = long.MaxValue;
            return Result.Success;
        }
    }
}