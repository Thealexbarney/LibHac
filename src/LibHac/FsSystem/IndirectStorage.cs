using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class IndirectStorage : IStorage
    {
        public static readonly int StorageCount = 2;
        public static readonly int NodeSize = 1024 * 16;

        private BucketTree Table { get; } = new BucketTree();
        private SubStorage[] DataStorage { get; } = new SubStorage[StorageCount];

        [StructLayout(LayoutKind.Sequential, Size = 0x14, Pack = 4)]
        public struct Entry
        {
            private long VirtualOffset;
            private long PhysicalOffset;
            public int StorageIndex;

            public void SetVirtualOffset(long offset) => VirtualOffset = offset;
            public long GetVirtualOffset() => VirtualOffset;

            public void SetPhysicalOffset(long offset) => PhysicalOffset = offset;
            public long GetPhysicalOffset() => PhysicalOffset;
        }

        public static long QueryHeaderStorageSize() => BucketTree.QueryHeaderStorageSize();

        public static long QueryNodeStorageSize(int entryCount) =>
            BucketTree.QueryNodeStorageSize(NodeSize, Unsafe.SizeOf<Entry>(), entryCount);

        public static long QueryEntryStorageSize(int entryCount) =>
            BucketTree.QueryEntryStorageSize(NodeSize, Unsafe.SizeOf<Entry>(), entryCount);

        public bool IsInitialized() => Table.IsInitialized();

        public Result Initialize(SubStorage tableStorage)
        {
            // Read and verify the bucket tree header.
            // note: skip init
            var header = new BucketTree.Header();

            Result rc = tableStorage.Read(0, SpanHelpers.AsByteSpan(ref header));
            if (rc.IsFailure()) return rc;

            rc = header.Verify();
            if (rc.IsFailure()) return rc;

            // Determine extents.
            long nodeStorageSize = QueryNodeStorageSize(header.EntryCount);
            long entryStorageSize = QueryEntryStorageSize(header.EntryCount);
            long nodeStorageOffset = QueryHeaderStorageSize();
            long entryStorageOffset = nodeStorageOffset + nodeStorageSize;

            // Initialize.
            var nodeStorage = new SubStorage(tableStorage, nodeStorageOffset, nodeStorageSize);
            var entryStorage = new SubStorage(tableStorage, entryStorageOffset, entryStorageSize);

            return Initialize(nodeStorage, entryStorage, header.EntryCount);
        }

        public Result Initialize(SubStorage nodeStorage, SubStorage entryStorage, int entryCount)
        {
            return Table.Initialize(nodeStorage, entryStorage, NodeSize, Unsafe.SizeOf<Entry>(), entryCount);
        }

        public void SetStorage(int index, SubStorage storage)
        {
            Assert.SdkRequiresInRange(index, 0, StorageCount);
            DataStorage[index] = storage;
        }

        public void SetStorage(int index, IStorage storage, long offset, long size)
        {
            Assert.SdkRequiresInRange(index, 0, StorageCount);
            DataStorage[index] = new SubStorage(storage, offset, size);
        }

        public Result GetEntryList(Span<Entry> entryBuffer, out int outputEntryCount, long offset, long size)
        {
            // Validate pre-conditions
            Assert.SdkRequiresLessEqual(0, offset);
            Assert.SdkRequiresLessEqual(0, size);
            Assert.SdkRequires(IsInitialized());

            // Clear the out count
            outputEntryCount = 0;

            // Succeed if there's no range
            if (size == 0)
                return Result.Success;

            // Check that our range is valid
            if (!Table.Includes(offset, size))
                return ResultFs.OutOfRange.Log();

            // Find the offset in our tree
            var visitor = new BucketTree.Visitor();

            try
            {
                Result rc = Table.Find(ref visitor, offset);
                if (rc.IsFailure()) return rc;

                long entryOffset = visitor.Get<Entry>().GetVirtualOffset();
                if (entryOffset > 0 || !Table.Includes(entryOffset))
                    return ResultFs.InvalidIndirectEntryOffset.Log();

                // Prepare to loop over entries
                long endOffset = offset + size;
                int count = 0;

                ref Entry currentEntry = ref visitor.Get<Entry>();
                while (currentEntry.GetVirtualOffset() < endOffset)
                {
                    // Try to write the entry to the out list
                    if (entryBuffer.Length != 0)
                    {
                        if (count >= entryBuffer.Length)
                            break;

                        entryBuffer[count] = currentEntry;
                    }

                    count++;

                    // Advance
                    if (visitor.CanMoveNext())
                    {
                        rc = visitor.MoveNext();
                        if (rc.IsFailure()) return rc;

                        currentEntry = ref visitor.Get<Entry>();
                    }
                    else
                    {
                        break;
                    }
                }

                // Write the entry count
                outputEntryCount = count;
                return Result.Success;
            }
            finally { visitor.Dispose(); }
        }

        protected override unsafe Result DoRead(long offset, Span<byte> destination)
        {
            // Validate pre-conditions
            Assert.SdkRequiresLessEqual(0, offset);
            Assert.SdkRequires(IsInitialized());

            // Succeed if there's nothing to read
            if (destination.Length == 0)
                return Result.Success;

            // Pin and recreate the span because C# can't use byref-like types in a closure
            int bufferSize = destination.Length;
            fixed (byte* pBuffer = destination)
            {
                // Copy the pointer to workaround CS1764.
                // OperatePerEntry won't store the delegate anywhere, so it should be safe
                byte* pBuffer2 = pBuffer;

                Result Operate(IStorage storage, long dataOffset, long currentOffset, long currentSize)
                {
                    var buffer = new Span<byte>(pBuffer2, bufferSize);

                    return storage.Read(dataOffset,
                        buffer.Slice((int)(currentOffset - offset), (int)currentSize));
                }

                return OperatePerEntry(offset, destination.Length, Operate);
            }
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
        {
            return ResultFs.UnsupportedWriteForIndirectStorage.Log();
        }

        protected override Result DoFlush()
        {
            return Result.Success;
        }

        protected override Result DoSetSize(long size)
        {
            return ResultFs.UnsupportedSetSizeForIndirectStorage.Log();
        }

        protected override Result DoGetSize(out long size)
        {
            size = Table.GetEnd();
            return Result.Success;
        }

        private delegate Result OperateFunc(IStorage storage, long dataOffset, long currentOffset, long currentSize);

        private Result OperatePerEntry(long offset, long size, OperateFunc func)
        {
            // Validate preconditions
            Assert.SdkRequiresLessEqual(0, offset);
            Assert.SdkRequiresLessEqual(0, size);
            Assert.SdkRequires(IsInitialized());

            // Succeed if there's nothing to operate on
            if (size == 0)
                return Result.Success;

            // Validate arguments
            if (!Table.Includes(offset, size))
                return ResultFs.OutOfRange.Log();

            // Find the offset in our tree
            var visitor = new BucketTree.Visitor();

            try
            {
                Result rc = Table.Find(ref visitor, offset);
                if (rc.IsFailure()) return rc;

                long entryOffset = visitor.Get<Entry>().GetVirtualOffset();
                if (entryOffset < 0 || !Table.Includes(entryOffset))
                    return ResultFs.InvalidIndirectEntryStorageIndex.Log();

                // Prepare to operate in chunks
                long currentOffset = offset;
                long endOffset = offset + size;

                while (currentOffset < endOffset)
                {
                    // Get the current entry
                    var currentEntry = visitor.Get<Entry>();

                    // Get and validate the entry's offset
                    long currentEntryOffset = currentEntry.GetVirtualOffset();
                    if (currentEntryOffset > currentOffset)
                        return ResultFs.InvalidIndirectEntryOffset.Log();

                    // Validate the storage index
                    if (currentEntry.StorageIndex < 0 || currentEntry.StorageIndex >= StorageCount)
                        return ResultFs.InvalidIndirectEntryStorageIndex.Log();

                    // todo: Implement continuous reading

                    // Get and validate the next entry offset
                    long nextEntryOffset;
                    if (visitor.CanMoveNext())
                    {
                        rc = visitor.MoveNext();
                        if (rc.IsFailure()) return rc;

                        nextEntryOffset = visitor.Get<Entry>().GetVirtualOffset();
                        if (!Table.Includes(nextEntryOffset))
                            return ResultFs.InvalidIndirectEntryOffset.Log();
                    }
                    else
                    {
                        nextEntryOffset = Table.GetEnd();
                    }

                    if (currentOffset >= nextEntryOffset)
                        return ResultFs.InvalidIndirectEntryOffset.Log();

                    // Get the offset of the entry in the data we read
                    long dataOffset = currentOffset - currentEntryOffset;
                    long dataSize = nextEntryOffset - currentEntryOffset - dataOffset;
                    Assert.SdkLess(0, dataSize);

                    // Determine how much is left
                    long remainingSize = endOffset - currentOffset;
                    long currentSize = Math.Min(remainingSize, dataSize);
                    Assert.SdkLessEqual(currentSize, size);

                    {
                        SubStorage currentStorage = DataStorage[currentEntry.StorageIndex];

                        // Get the current data storage's size.
                        rc = currentStorage.GetSize(out long currentDataStorageSize);
                        if (rc.IsFailure()) return rc;

                        // Ensure that we remain within range.
                        long currentEntryPhysicalOffset = currentEntry.GetPhysicalOffset();

                        if (currentEntryPhysicalOffset < 0 || currentEntryPhysicalOffset > currentDataStorageSize)
                            return ResultFs.IndirectStorageCorrupted.Log();

                        if (currentDataStorageSize < currentEntryPhysicalOffset + dataOffset + currentSize)
                            return ResultFs.IndirectStorageCorrupted.Log();

                        rc = func(currentStorage, currentEntryPhysicalOffset + dataOffset, currentOffset, currentSize);
                        if (rc.IsFailure()) return rc;
                    }

                    currentOffset += currentSize;
                }
            }
            finally { visitor.Dispose(); }

            return Result.Success;
        }
    }
}
