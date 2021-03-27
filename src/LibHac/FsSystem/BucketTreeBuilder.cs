using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Util;

namespace LibHac.FsSystem
{
    public partial class BucketTree
    {
        public class Builder
        {
            private SubStorage NodeStorage { get; set; }
            private SubStorage EntryStorage { get; set; }

            private NodeBuffer _l1Node;
            private NodeBuffer _l2Node;
            private NodeBuffer _entrySet;

            private int NodeSize { get; set; }
            private int EntrySize { get; set; }
            private int EntryCount { get; set; }
            private int EntriesPerEntrySet { get; set; }
            private int OffsetsPerNode { get; set; }

            private int CurrentL2OffsetIndex { get; set; }
            private int CurrentEntryIndex { get; set; }
            private long CurrentOffset { get; set; } = -1;

            /// <summary>
            /// Initializes the bucket tree builder.
            /// </summary>
            /// <param name="headerStorage">The <see cref="SubStorage"/> the tree's header will be written to.Must be at least the size in bytes returned by <see cref="QueryHeaderStorageSize"/>.</param>
            /// <param name="nodeStorage">The <see cref="SubStorage"/> the tree's nodes will be written to. Must be at least the size in bytes returned by <see cref="QueryNodeStorageSize"/>.</param>
            /// <param name="entryStorage">The <see cref="SubStorage"/> the tree's entries will be written to. Must be at least the size in bytes returned by <see cref="QueryEntryStorageSize"/>.</param>
            /// <param name="nodeSize">The size of each node in the bucket tree. Must be a power of 2.</param>
            /// <param name="entrySize">The size of each entry that will be stored in the bucket tree.</param>
            /// <param name="entryCount">The exact number of entries that will be added to the bucket tree.</param>
            /// <returns>The <see cref="Result"/> of the operation.</returns>
            public Result Initialize(SubStorage headerStorage, SubStorage nodeStorage, SubStorage entryStorage,
                int nodeSize, int entrySize, int entryCount)
            {
                Assert.SdkRequiresLessEqual(sizeof(long), entrySize);
                Assert.SdkRequiresLessEqual(entrySize + Unsafe.SizeOf<NodeHeader>(), nodeSize);
                Assert.SdkRequiresWithinMinMax(nodeSize, NodeSizeMin, NodeSizeMax);
                Assert.SdkRequires(BitUtil.IsPowerOfTwo(nodeSize));

                if (headerStorage is null || nodeStorage is null || entryStorage is null)
                    return ResultFs.NullptrArgument.Log();

                // Set the builder parameters
                NodeSize = nodeSize;
                EntrySize = entrySize;
                EntryCount = entryCount;

                EntriesPerEntrySet = GetEntryCount(nodeSize, entrySize);
                OffsetsPerNode = GetOffsetCount(nodeSize);
                CurrentL2OffsetIndex = GetNodeL2Count(nodeSize, entrySize, entryCount);

                // Create and write the header
                var header = new Header();
                header.Format(entryCount);
                Result rc = headerStorage.Write(0, SpanHelpers.AsByteSpan(ref header));
                if (rc.IsFailure()) return rc;

                // Allocate buffers for the L1 node and entry sets
                _l1Node.Allocate(nodeSize);
                _entrySet.Allocate(nodeSize);

                int entrySetCount = GetEntrySetCount(nodeSize, entrySize, entryCount);

                // Allocate an L2 node buffer if there are more entry sets than will fit in the L1 node
                if (OffsetsPerNode < entrySetCount)
                {
                    _l2Node.Allocate(nodeSize);
                }

                _l1Node.FillZero();
                _l2Node.FillZero();
                _entrySet.FillZero();

                NodeStorage = nodeStorage;
                EntryStorage = entryStorage;

                // Set the initial position
                CurrentEntryIndex = 0;
                CurrentOffset = -1;

                return Result.Success;
            }

            /// <summary>
            /// Adds a new entry to the bucket tree.
            /// </summary>
            /// <typeparam name="T">The type of the entry to add. Added entries should all be the same type.</typeparam>
            /// <param name="entry">The entry to add.</param>
            /// <returns>The <see cref="Result"/> of the operation.</returns>
            public Result Add<T>(ref T entry) where T : unmanaged
            {
                Assert.SdkRequiresEqual(Unsafe.SizeOf<T>(), EntrySize);

                if (CurrentEntryIndex >= EntryCount)
                    return ResultFs.OutOfRange.Log();

                // The entry offset must always be the first 8 bytes of the struct
                long entryOffset = BinaryPrimitives.ReadInt64LittleEndian(SpanHelpers.AsByteSpan(ref entry));

                if (entryOffset <= CurrentOffset)
                    return ResultFs.InvalidOffset.Log();

                Result rc = FinalizePreviousEntrySet(entryOffset);
                if (rc.IsFailure()) return rc;

                AddEntryOffset(entryOffset);

                // Write the new entry
                int indexInEntrySet = CurrentEntryIndex % EntriesPerEntrySet;
                _entrySet.GetNode<T>().GetWritableArray()[indexInEntrySet] = entry;

                CurrentOffset = entryOffset;
                CurrentEntryIndex++;

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
                int prevEntrySetIndex = CurrentEntryIndex / EntriesPerEntrySet - 1;
                int indexInEntrySet = CurrentEntryIndex % EntriesPerEntrySet;

                // If the previous Add finished an entry set
                if (CurrentEntryIndex > 0 && indexInEntrySet == 0)
                {
                    // Set the end offset of that entry set
                    ref NodeHeader entrySetHeader = ref _entrySet.GetHeader();

                    entrySetHeader.Index = prevEntrySetIndex;
                    entrySetHeader.Count = EntriesPerEntrySet;
                    entrySetHeader.Offset = endOffset;

                    // Write the entry set to the entry storage
                    long storageOffset = (long)NodeSize * prevEntrySetIndex;
                    Result rc = EntryStorage.Write(storageOffset, _entrySet.GetBuffer());
                    if (rc.IsFailure()) return rc;

                    // Clear the entry set buffer to begin the new entry set
                    _entrySet.FillZero();

                    // Check if we're writing in L2 nodes
                    if (CurrentL2OffsetIndex > OffsetsPerNode)
                    {
                        int prevL2NodeIndex = CurrentL2OffsetIndex / OffsetsPerNode - 2;
                        int indexInL2Node = CurrentL2OffsetIndex % OffsetsPerNode;

                        // If the previous Add finished an L2 node
                        if (indexInL2Node == 0)
                        {
                            // Set the end offset of that node
                            ref NodeHeader l2NodeHeader = ref _l2Node.GetHeader();

                            l2NodeHeader.Index = prevL2NodeIndex;
                            l2NodeHeader.Count = OffsetsPerNode;
                            l2NodeHeader.Offset = endOffset;

                            // Write the L2 node to the node storage
                            long nodeOffset = (long)NodeSize * (prevL2NodeIndex + 1);
                            rc = NodeStorage.Write(nodeOffset, _l2Node.GetBuffer());
                            if (rc.IsFailure()) return rc;

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
                int entrySetIndex = CurrentEntryIndex / EntriesPerEntrySet;
                int indexInEntrySet = CurrentEntryIndex % EntriesPerEntrySet;

                // If we're starting a new entry set we need to add its start offset to the L1/L2 nodes
                if (indexInEntrySet == 0)
                {
                    Span<long> l1Data = _l1Node.GetNode<long>().GetWritableArray();

                    if (CurrentL2OffsetIndex == 0)
                    {
                        // There are no L2 nodes. Write the entry set end offset directly to L1
                        l1Data[entrySetIndex] = entryOffset;
                    }
                    else
                    {
                        if (CurrentL2OffsetIndex < OffsetsPerNode)
                        {
                            // The current L2 offset is stored in the L1 node
                            l1Data[CurrentL2OffsetIndex] = entryOffset;
                        }
                        else
                        {
                            // Write the entry set offset to the current L2 node
                            int l2NodeIndex = CurrentL2OffsetIndex / OffsetsPerNode;
                            int indexInL2Node = CurrentL2OffsetIndex % OffsetsPerNode;

                            Span<long> l2Data = _l2Node.GetNode<long>().GetWritableArray();
                            l2Data[indexInL2Node] = entryOffset;

                            // If we're starting a new L2 node we need to add its start offset to the L1 node
                            if (indexInL2Node == 0)
                            {
                                l1Data[l2NodeIndex - 1] = entryOffset;
                            }
                        }

                        CurrentL2OffsetIndex++;
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
                if (EntryCount != CurrentEntryIndex)
                    return ResultFs.OutOfRange.Log();

                if (endOffset <= CurrentOffset)
                    return ResultFs.InvalidOffset.Log();

                if (CurrentOffset == -1)
                    return Result.Success;

                Result rc = FinalizePreviousEntrySet(endOffset);
                if (rc.IsFailure()) return rc;

                int entrySetIndex = CurrentEntryIndex / EntriesPerEntrySet;
                int indexInEntrySet = CurrentEntryIndex % EntriesPerEntrySet;

                // Finalize the current entry set if needed
                if (indexInEntrySet != 0)
                {
                    ref NodeHeader entrySetHeader = ref _entrySet.GetHeader();

                    entrySetHeader.Index = entrySetIndex;
                    entrySetHeader.Count = indexInEntrySet;
                    entrySetHeader.Offset = endOffset;

                    long entryStorageOffset = (long)NodeSize * entrySetIndex;
                    rc = EntryStorage.Write(entryStorageOffset, _entrySet.GetBuffer());
                    if (rc.IsFailure()) return rc;
                }

                int l2NodeIndex = BitUtil.DivideUp(CurrentL2OffsetIndex, OffsetsPerNode) - 2;
                int indexInL2Node = CurrentL2OffsetIndex % OffsetsPerNode;

                // Finalize the current L2 node if needed
                if (CurrentL2OffsetIndex > OffsetsPerNode && (indexInEntrySet != 0 || indexInL2Node != 0))
                {
                    ref NodeHeader l2NodeHeader = ref _l2Node.GetHeader();
                    l2NodeHeader.Index = l2NodeIndex;
                    l2NodeHeader.Count = indexInL2Node != 0 ? indexInL2Node : OffsetsPerNode;
                    l2NodeHeader.Offset = endOffset;

                    long l2NodeStorageOffset = NodeSize * (l2NodeIndex + 1);
                    rc = NodeStorage.Write(l2NodeStorageOffset, _l2Node.GetBuffer());
                    if (rc.IsFailure()) return rc;
                }

                // Finalize the L1 node
                ref NodeHeader l1NodeHeader = ref _l1Node.GetHeader();
                l1NodeHeader.Index = 0;
                l1NodeHeader.Offset = endOffset;

                // L1 count depends on the existence or absence of L2 nodes
                if (CurrentL2OffsetIndex == 0)
                {
                    l1NodeHeader.Count = BitUtil.DivideUp(CurrentEntryIndex, EntriesPerEntrySet);
                }
                else
                {
                    l1NodeHeader.Count = l2NodeIndex + 1;
                }

                rc = NodeStorage.Write(0, _l1Node.GetBuffer());
                if (rc.IsFailure()) return rc;

                CurrentOffset = long.MaxValue;
                return Result.Success;
            }
        }
    }
}
