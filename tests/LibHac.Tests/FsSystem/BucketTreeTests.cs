using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Tests.Common;
using Xunit;

namespace LibHac.Tests.FsSystem;

public class BucketTreeBuffers
{
    public IndirectStorage.Entry[] Entries { get; }
    public BucketTreeTests.BucketTreeData[] TreeData { get; }

    public BucketTreeBuffers()
    {
        (int nodeSize, int entryCount)[] treeConfig = BucketTreeTests.BucketTreeTestParams;
        TreeData = new BucketTreeTests.BucketTreeData[treeConfig.Length];

        Entries = BucketTreeCreator.GenerateEntries(0, new SizeRange(0x1000, 1, 10), 2_000_001);

        for (int i = 0; i < treeConfig.Length; i++)
        {
            (int nodeSize, int entryCount) = treeConfig[i];
            TreeData[i] = BucketTreeCreator.Create(0, new SizeRange(0x1000, 1, 10), nodeSize, entryCount);
        }
    }
}

public class BucketTreeTests : IClassFixture<BucketTreeBuffers>
{
    // Keep the generated data between tests so it only has to be generated once
    private readonly IndirectStorage.Entry[] _entries;
    private readonly BucketTreeData[] _treeData;

    public BucketTreeTests(BucketTreeBuffers buffers)
    {
        _entries = buffers.Entries;
        _treeData = buffers.TreeData;
    }

    public static readonly (int nodeSize, int entryCount)[] BucketTreeTestParams =
    {
        (0x4000, 5),
        (0x4000, 10000),
        (0x4000, 2_000_000),
        (0x400, 50_000),
        (0x400, 793_800)
    };

    public static TheoryData<int> BucketTreeTestTheoryData =
        TheoryDataCreator.CreateSequence(0, BucketTreeTestParams.Length);

    public class BucketTreeData
    {
        public int NodeSize;
        public int EntryCount;
        public byte[] Header;
        public byte[] Nodes;
        public byte[] Entries;

        public BucketTree CreateBucketTree()
        {
            int entrySize = Unsafe.SizeOf<IndirectStorage.Entry>();

            BucketTree.Header header = MemoryMarshal.Cast<byte, BucketTree.Header>(Header.AsSpan())[0];
            using var nodeStorage = new ValueSubStorage(new MemoryStorage(Nodes), 0, Nodes.Length);
            using var entryStorage = new ValueSubStorage(new MemoryStorage(Entries), 0, Entries.Length);

            var tree = new BucketTree();
            Assert.Success(tree.Initialize(new ArrayPoolMemoryResource(), in nodeStorage, in entryStorage, NodeSize, entrySize, header.EntryCount));

            return tree;
        }
    }

    [Theory, MemberData(nameof(BucketTreeTestTheoryData))]
    private void MoveNext_IterateAllFromStart_ReturnsCorrectEntries(int treeIndex)
    {
        ReadOnlySpan<IndirectStorage.Entry> entries = _entries.AsSpan(0, _treeData[treeIndex].EntryCount);
        BucketTree tree = _treeData[treeIndex].CreateBucketTree();

        using var visitor = new BucketTree.Visitor();
        Assert.Success(tree.Find(ref visitor.Ref, 0));

        for (int i = 0; i < entries.Length; i++)
        {
            if (i != 0)
            {
                Result res = visitor.MoveNext();

                if (!res.IsSuccess())
                    Assert.Success(res);
            }

            // These tests run about 4x slower if we let Assert.Equal check the values every time
            if (visitor.CanMovePrevious() != (i != 0))
                Assert.Equal(i != 0, visitor.CanMovePrevious());

            if (visitor.CanMoveNext() != (i != entries.Length - 1))
                Assert.Equal(i != entries.Length - 1, visitor.CanMoveNext());

            ref readonly IndirectStorage.Entry entry = ref visitor.Get<IndirectStorage.Entry>();

            if (entries[i].GetVirtualOffset() != entry.GetVirtualOffset())
                Assert.Equal(entries[i].GetVirtualOffset(), entry.GetVirtualOffset());

            if (entries[i].GetPhysicalOffset() != entry.GetPhysicalOffset())
                Assert.Equal(entries[i].GetPhysicalOffset(), entry.GetPhysicalOffset());

            if (entries[i].StorageIndex != entry.StorageIndex)
                Assert.Equal(entries[i].StorageIndex, entry.StorageIndex);
        }
    }

    [Theory, MemberData(nameof(BucketTreeTestTheoryData))]
    private void MovePrevious_IterateAllFromEnd_ReturnsCorrectEntries(int treeIndex)
    {
        ReadOnlySpan<IndirectStorage.Entry> entries = _entries.AsSpan(0, _treeData[treeIndex].EntryCount);
        BucketTree tree = _treeData[treeIndex].CreateBucketTree();

        using var visitor = new BucketTree.Visitor();
        Assert.Success(tree.Find(ref visitor.Ref, entries[^1].GetVirtualOffset()));

        for (int i = entries.Length - 1; i >= 0; i--)
        {
            if (i != entries.Length - 1)
            {
                Result res = visitor.MovePrevious();

                if (!res.IsSuccess())
                    Assert.Success(res);
            }

            if (visitor.CanMovePrevious() != (i != 0))
                Assert.Equal(i != 0, visitor.CanMovePrevious());

            if (visitor.CanMoveNext() != (i != entries.Length - 1))
                Assert.Equal(i != entries.Length - 1, visitor.CanMoveNext());

            ref readonly IndirectStorage.Entry entry = ref visitor.Get<IndirectStorage.Entry>();

            if (entries[i].GetVirtualOffset() != entry.GetVirtualOffset())
                Assert.Equal(entries[i].GetVirtualOffset(), entry.GetVirtualOffset());

            if (entries[i].GetPhysicalOffset() != entry.GetPhysicalOffset())
                Assert.Equal(entries[i].GetPhysicalOffset(), entry.GetPhysicalOffset());

            if (entries[i].StorageIndex != entry.StorageIndex)
                Assert.Equal(entries[i].StorageIndex, entry.StorageIndex);
        }
    }

    [Theory, MemberData(nameof(BucketTreeTestTheoryData))]
    private void Find_RandomAccess_ReturnsCorrectEntries(int treeIndex)
    {
        const int findCount = 10000;

        ReadOnlySpan<IndirectStorage.Entry> entries = _entries.AsSpan(0, _treeData[treeIndex].EntryCount);
        BucketTree tree = _treeData[treeIndex].CreateBucketTree();

        var random = new Random(123456);

        for (int i = 0; i < findCount; i++)
        {
            int entryIndex = random.Next(0, entries.Length);
            ref readonly IndirectStorage.Entry expectedEntry = ref entries[entryIndex];

            // Add a random shift amount to test finding offsets in the middle of an entry
            int offsetShift = random.Next(0, 1) * 0x500;

            using var visitor = new BucketTree.Visitor();
            Assert.Success(tree.Find(ref visitor.Ref, expectedEntry.GetVirtualOffset() + offsetShift));

            ref readonly IndirectStorage.Entry actualEntry = ref visitor.Get<IndirectStorage.Entry>();

            Assert.Equal(entryIndex != 0, visitor.CanMovePrevious());
            Assert.Equal(entryIndex != entries.Length - 1, visitor.CanMoveNext());
            Assert.Equal(expectedEntry.GetVirtualOffset(), actualEntry.GetVirtualOffset());
            Assert.Equal(expectedEntry.GetPhysicalOffset(), actualEntry.GetPhysicalOffset());
            Assert.Equal(expectedEntry.StorageIndex, actualEntry.StorageIndex);
        }
    }

    [Theory, MemberData(nameof(BucketTreeTestTheoryData))]
    private void GetEntryCount_ReturnsCorrectCount(int treeIndex)
    {
        BucketTree tree = _treeData[treeIndex].CreateBucketTree();

        Assert.Equal(_treeData[treeIndex].EntryCount, tree.GetEntryCount());
    }
}