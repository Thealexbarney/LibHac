using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public partial class BucketTree2
    {
        public ref struct Builder
        {
            private Span<byte> NodeBuffer { get; set; }
            private Span<byte> EntryBuffer { get; set; }

            private int NodeSize { get; set; }
            private int EntrySize { get; set; }
            private int EntryCount { get; set; }
            private int EntriesPerEntrySet { get; set; }
            private int OffsetsPerNode { get; set; }

            private int CurrentL2OffsetIndex { get; set; }
            private int CurrentEntryIndex { get; set; }
            private long CurrentOffset { get; set; }

            /// <summary>
            /// Initializes the bucket tree builder.
            /// </summary>
            /// <param name="headerBuffer">The buffer for the tree's header. Must be at least the size in bytes returned by <see cref="QueryHeaderStorageSize"/>.</param>
            /// <param name="nodeBuffer">The buffer for the tree's nodes. Must be at least the size in bytes returned by <see cref="QueryNodeStorageSize"/>.</param>
            /// <param name="entryBuffer">The buffer for the tree's entries. Must be at least the size in bytes returned by <see cref="QueryEntryStorageSize"/>.</param>
            /// <param name="nodeSize">The size of each node in the bucket tree.</param>
            /// <param name="entrySize">The size of each entry that will be stored in the bucket tree.</param>
            /// <param name="entryCount">The exact number of entries that will be added to the bucket tree.</param>
            /// <returns>The <see cref="Result"/> of the operation.</returns>
            public Result Initialize(Span<byte> headerBuffer, Span<byte> nodeBuffer, Span<byte> entryBuffer,
                int nodeSize, int entrySize, int entryCount)
            {
                Assert.AssertTrue(entrySize >= sizeof(long));
                Assert.AssertTrue(nodeSize >= entrySize + Unsafe.SizeOf<NodeHeader>());
                Assert.AssertTrue(NodeSizeMin <= nodeSize && nodeSize <= NodeSizeMax);
                Assert.AssertTrue(Util.IsPowerOfTwo(nodeSize));

                NodeSize = nodeSize;
                EntrySize = entrySize;
                EntryCount = entryCount;

                EntriesPerEntrySet = GetEntryCount(nodeSize, entrySize);
                OffsetsPerNode = GetOffsetCount(nodeSize);
                CurrentL2OffsetIndex = GetNodeL2Count(nodeSize, entrySize, entryCount);

                // Verify the provided buffers are large enough
                int nodeStorageSize = (int)QueryNodeStorageSize(nodeSize, entrySize, entryCount);
                int entryStorageSize = (int)QueryEntryStorageSize(nodeSize, entrySize, entryCount);

                if (headerBuffer.Length < QueryHeaderStorageSize() ||
                    nodeBuffer.Length < nodeStorageSize ||
                    entryBuffer.Length < entryStorageSize)
                {
                    return ResultFs.InvalidSize.Log();
                }

                // Set and clear the buffers
                NodeBuffer = nodeBuffer.Slice(0, nodeStorageSize);
                EntryBuffer = entryBuffer.Slice(0, entryStorageSize);

                nodeBuffer.Clear();
                entryBuffer.Clear();

                // Format the tree header
                ref Header header = ref SpanHelpers.AsStruct<Header>(headerBuffer);
                header.Format(entryCount);

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
                Assert.AssertTrue(Unsafe.SizeOf<T>() == EntrySize);

                if (CurrentEntryIndex >= EntryCount)
                    return ResultFs.OutOfRange.Log();

                long entryOffset = BinaryPrimitives.ReadInt64LittleEndian(SpanHelpers.AsByteSpan(ref entry));

                if (entryOffset <= CurrentOffset)
                    return ResultFs.InvalidOffset.Log();

                FinalizePreviousEntrySet(entryOffset);
                AddEntryOffset(entryOffset);

                int entrySetIndex = CurrentEntryIndex / EntriesPerEntrySet;
                int indexInEntrySet = CurrentEntryIndex % EntriesPerEntrySet;

                GetEntrySet<T>(entrySetIndex).GetWritableArray()[indexInEntrySet] = entry;

                CurrentOffset = entryOffset;
                CurrentEntryIndex++;

                return Result.Success;
            }

            /// <summary>
            /// Checks if a new entry set is being started. If so, sets the end offset of the previous entry set.
            /// </summary>
            /// <param name="endOffset">The end offset of the previous entry.</param>
            private void FinalizePreviousEntrySet(long endOffset)
            {
                int prevEntrySetIndex = CurrentEntryIndex / EntriesPerEntrySet - 1;
                int indexInEntrySet = CurrentEntryIndex % EntriesPerEntrySet;

                // If the previous Add finished an entry set
                if (CurrentEntryIndex > 0 && indexInEntrySet == 0)
                {
                    // Set the end offset of that entry set
                    BucketTreeNode<long> prevEntrySet = GetEntrySet<long>(prevEntrySetIndex);

                    prevEntrySet.GetHeader().Index = prevEntrySetIndex;
                    prevEntrySet.GetHeader().Count = EntriesPerEntrySet;
                    prevEntrySet.GetHeader().Offset = endOffset;

                    // Check if we're writing in L2 nodes
                    if (CurrentL2OffsetIndex > OffsetsPerNode)
                    {
                        int prevL2NodeIndex = CurrentL2OffsetIndex / OffsetsPerNode - 2;
                        int indexInL2Node = CurrentL2OffsetIndex % OffsetsPerNode;

                        // If the previous Add finished an L2 node
                        if (indexInL2Node == 0)
                        {
                            // Set the end offset of that node
                            BucketTreeNode<long> prevL2Node = GetL2Node(prevL2NodeIndex);

                            prevL2Node.GetHeader().Index = prevL2NodeIndex;
                            prevL2Node.GetHeader().Count = OffsetsPerNode;
                            prevL2Node.GetHeader().Offset = endOffset;
                        }
                    }
                }
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
                    BucketTreeNode<long> l1Node = GetL1Node();

                    if (CurrentL2OffsetIndex == 0)
                    {
                        // There are no L2 nodes. Write the entry set end offset directly to L1
                        l1Node.GetWritableArray()[entrySetIndex] = entryOffset;
                    }
                    else
                    {
                        if (CurrentL2OffsetIndex < OffsetsPerNode)
                        {
                            // The current L2 offset is stored in the L1 node
                            l1Node.GetWritableArray()[CurrentL2OffsetIndex] = entryOffset;
                        }
                        else
                        {
                            // Write the entry set offset to the current L2 node
                            int l2NodeIndex = CurrentL2OffsetIndex / OffsetsPerNode;
                            int indexInL2Node = CurrentL2OffsetIndex % OffsetsPerNode;

                            BucketTreeNode<long> l2Node = GetL2Node(l2NodeIndex - 1);

                            l2Node.GetWritableArray()[indexInL2Node] = entryOffset;

                            // If we're starting a new L2 node we need to add its start offset to the L1 node
                            if (indexInL2Node == 0)
                            {
                                l1Node.GetWritableArray()[l2NodeIndex - 1] = entryOffset;
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

                FinalizePreviousEntrySet(endOffset);

                int entrySetIndex = CurrentEntryIndex / EntriesPerEntrySet;
                int indexInEntrySet = CurrentEntryIndex % EntriesPerEntrySet;

                // Finalize the current entry set if needed
                if (indexInEntrySet != 0)
                {
                    ref NodeHeader entrySetHeader = ref GetEntrySetHeader(entrySetIndex);

                    entrySetHeader.Index = entrySetIndex;
                    entrySetHeader.Count = indexInEntrySet;
                    entrySetHeader.Offset = endOffset;
                }

                int l2NodeIndex = Util.DivideByRoundUp(CurrentL2OffsetIndex, OffsetsPerNode) - 2;
                int indexInL2Node = CurrentL2OffsetIndex % OffsetsPerNode;

                // Finalize the current L2 node if needed
                if (CurrentL2OffsetIndex > OffsetsPerNode && (indexInEntrySet != 0 || indexInL2Node != 0))
                {
                    ref NodeHeader l2NodeHeader = ref GetL2Node(l2NodeIndex).GetHeader();
                    l2NodeHeader.Index = l2NodeIndex;
                    l2NodeHeader.Count = indexInL2Node != 0 ? indexInL2Node : OffsetsPerNode;
                    l2NodeHeader.Offset = endOffset;
                }

                // Finalize the L1 node
                ref NodeHeader l1Header = ref GetL1Node().GetHeader();

                l1Header.Index = 0;
                l1Header.Offset = endOffset;

                // L1 count depends on the existence or absence of L2 nodes
                if (CurrentL2OffsetIndex == 0)
                {
                    l1Header.Count = Util.DivideByRoundUp(CurrentEntryIndex, EntriesPerEntrySet);
                }
                else
                {
                    l1Header.Count = l2NodeIndex + 1;
                }

                return Result.Success;
            }

            private ref NodeHeader GetEntrySetHeader(int index)
            {
                BucketTreeNode<long> entrySetNode = GetEntrySet<long>(index);
                return ref entrySetNode.GetHeader();
            }

            private BucketTreeNode<T> GetEntrySet<T>(int index) where T : unmanaged
            {
                Span<byte> entrySetBuffer = EntryBuffer.Slice(NodeSize * index, NodeSize);
                return new BucketTreeNode<T>(entrySetBuffer);
            }

            private BucketTreeNode<long> GetL1Node()
            {
                Span<byte> l1NodeBuffer = NodeBuffer.Slice(0, NodeSize);
                return new BucketTreeNode<long>(l1NodeBuffer);
            }

            private BucketTreeNode<long> GetL2Node(int index)
            {
                Span<byte> l2NodeBuffer = NodeBuffer.Slice(NodeSize * (index + 1), NodeSize);
                return new BucketTreeNode<long>(l2NodeBuffer);
            }
        }
    }
}
