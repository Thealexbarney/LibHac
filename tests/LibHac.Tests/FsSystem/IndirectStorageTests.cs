using System;
using System.Linq;
using System.Runtime.InteropServices;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Tests.Common;
using LibHac.Tests.Fs;
using Xunit;

namespace LibHac.Tests.FsSystem;

public class IndirectStorageBuffers
{
    public IndirectStorageTests.IndirectStorageData[] Buffers { get; }

    public IndirectStorageBuffers()
    {
        IndirectStorageTests.IndirectStorageTestConfig[] storageConfig = IndirectStorageTests.IndirectStorageTestData;
        Buffers = new IndirectStorageTests.IndirectStorageData[storageConfig.Length];

        for (int i = 0; i < storageConfig.Length; i++)
        {
            IndirectStorageTests.IndirectStorageTestConfig config = storageConfig[i];

            SizeRange patchSizeRange = config.PatchEntrySizeRange.BlockSize == 0
                ? config.OriginalEntrySizeRange
                : config.PatchEntrySizeRange;

            Buffers[i] = IndirectStorageCreator.Create(config.RngSeed, config.OriginalEntrySizeRange, patchSizeRange,
                config.StorageSize);
        }
    }
}

public class IndirectStorageTests : IClassFixture<IndirectStorageBuffers>
{
    // Keep the generated data between tests so it only has to be generated once
    private readonly IndirectStorageData[] _storageBuffers;

    public IndirectStorageTests(IndirectStorageBuffers buffers)
    {
        _storageBuffers = buffers.Buffers;
    }

    public class IndirectStorageTestConfig
    {
        public ulong RngSeed { get; init; }
        public long StorageSize { get; init; }

        // If the patch size range is left blank, the same values will be used for both the original and patch entry sizes
        public SizeRange OriginalEntrySizeRange { get; init; }
        public SizeRange PatchEntrySizeRange { get; init; }
    }

    private class RandomAccessTestConfig
    {
        public int[] SizeClassProbs { get; init; }
        public int[] SizeClassMaxSizes { get; init; }
        public int[] TaskProbs { get; init; }
        public int[] AccessTypeProbs { get; init; }
        public ulong RngSeed { get; init; }
        public int FrequentAccessBlockCount { get; init; }
    }

    public static readonly IndirectStorageTestConfig[] IndirectStorageTestData =
    [
        // Small patched regions to force continuous reading
        new()
        {
            RngSeed = 948285,
            OriginalEntrySizeRange = new SizeRange(0x10000, 1, 5),
            PatchEntrySizeRange = new SizeRange(1, 0x20, 0xFFF),
            StorageSize = 1024 * 1024 * 10
        },
        // Small patch regions
        new()
        {
            RngSeed = 236956,
            OriginalEntrySizeRange = new SizeRange(0x1000, 1, 10),
            StorageSize = 1024 * 1024 * 10
        },
        // Medium patch regions
        new()
        {
            RngSeed = 352174,
            OriginalEntrySizeRange = new SizeRange(0x8000, 1, 10),
            StorageSize = 1024 * 1024 * 10
        },
        // Larger patch regions
        new()
        {
            RngSeed = 220754,
            OriginalEntrySizeRange = new SizeRange(0x10000, 10, 50),
            StorageSize = 1024 * 1024 * 10
        }
    ];

    private static readonly RandomAccessTestConfig[] AccessTestConfigs =
    [
        new()
        {
            SizeClassProbs = [50, 50, 5],
            SizeClassMaxSizes = [0x4000, 0x80000, 0x800000], // 16 KB, 512 KB, 8 MB
            TaskProbs = [1, 0, 0], // Read, Write, Flush
            AccessTypeProbs = [10, 10, 5], // Random, Sequential, Frequent block
            RngSeed = 35467,
            FrequentAccessBlockCount = 6,
        },
        new()
        {
            SizeClassProbs = [50, 50, 5],
            SizeClassMaxSizes = [0x800, 0x1000, 0x8000], // 2 KB, 4 KB, 32 KB
            TaskProbs = [1, 0, 0], // Read, Write, Flush
            AccessTypeProbs = [1, 10, 0], // Random, Sequential, Frequent block
            RngSeed = 13579
        },
    ];

    public static TheoryData<int> IndirectStorageTestTheoryData =
        TheoryDataCreator.CreateSequence(0, IndirectStorageTestData.Length);

    public class IndirectStorageData
    {
        public IndirectStorage.Entry[] TableEntries;
        public byte[] TableHeaderBuffer;
        public byte[] TableNodeBuffer;
        public byte[] TableEntryBuffer;

        public byte[] SparseTableHeaderBuffer;
        public byte[] SparseTableNodeBuffer;
        public byte[] SparseTableEntryBuffer;

        public byte[] OriginalStorageBuffer;
        public byte[] SparseOriginalStorageBuffer;
        public byte[] PatchStorageBuffer;
        public byte[] PatchedStorageBuffer;

        public IndirectStorage CreateIndirectStorage(bool useSparseOriginalStorage)
        {
            BucketTree.Header header = MemoryMarshal.Cast<byte, BucketTree.Header>(TableHeaderBuffer)[0];

            using var nodeStorage = new ValueSubStorage(new MemoryStorage(TableNodeBuffer), 0, TableNodeBuffer.Length);
            using var entryStorage = new ValueSubStorage(new MemoryStorage(TableEntryBuffer), 0, TableEntryBuffer.Length);

            IStorage originalStorageBase = useSparseOriginalStorage ? CreateSparseStorage() : new MemoryStorage(OriginalStorageBuffer);

            using var originalStorage = new ValueSubStorage(originalStorageBase, 0, OriginalStorageBuffer.Length);
            using var patchStorage = new ValueSubStorage(new MemoryStorage(PatchStorageBuffer), 0, PatchStorageBuffer.Length);

            var storage = new IndirectStorage();
            Assert.Success(storage.Initialize(new ArrayPoolMemoryResource(), in nodeStorage, in entryStorage, header.EntryCount));
            storage.SetStorage(0, in originalStorage);
            storage.SetStorage(1, in patchStorage);

            return storage;
        }

        public SparseStorage CreateSparseStorage()
        {
            BucketTree.Header header = MemoryMarshal.Cast<byte, BucketTree.Header>(SparseTableHeaderBuffer)[0];

            using var nodeStorage = new ValueSubStorage(new MemoryStorage(SparseTableNodeBuffer), 0, SparseTableNodeBuffer.Length);
            using var entryStorage = new ValueSubStorage(new MemoryStorage(SparseTableEntryBuffer), 0, SparseTableEntryBuffer.Length);

            using var sparseOriginalStorage = new ValueSubStorage(new MemoryStorage(SparseOriginalStorageBuffer), 0, SparseOriginalStorageBuffer.Length);

            var sparseStorage = new SparseStorage();
            Assert.Success(sparseStorage.Initialize(new ArrayPoolMemoryResource(), in nodeStorage, in entryStorage, header.EntryCount));
            sparseStorage.SetDataStorage(in sparseOriginalStorage);

            return sparseStorage;
        }
    }

    [Theory, MemberData(nameof(IndirectStorageTestTheoryData))]
    public void Read_EntireStorageInSingleRead_DataIsCorrect(int index)
    {
        ReadEntireStorageImpl(index, false);
    }

    [Theory, MemberData(nameof(IndirectStorageTestTheoryData))]
    public void Read_EntireStorageInSingleRead_OriginalStorageIsSparse_DataIsCorrect(int index)
    {
        ReadEntireStorageImpl(index, true);
    }

    private void ReadEntireStorageImpl(int index, bool useSparseOriginalStorage)
    {
        using IndirectStorage storage = _storageBuffers[index].CreateIndirectStorage(useSparseOriginalStorage);

        byte[] expectedPatchedData = _storageBuffers[index].PatchedStorageBuffer;
        byte[] actualPatchedData = new byte[expectedPatchedData.Length];

        Assert.Success(storage.GetSize(out long storageSize));
        Assert.Equal(actualPatchedData.Length, storageSize);

        Assert.Success(storage.Read(0, actualPatchedData));
        Assert.True(expectedPatchedData.SequenceEqual(actualPatchedData));
    }

    [Fact]
    public void Initialize_SingleTableStorage()
    {
        const int index = 1;
        IndirectStorageData buffers = _storageBuffers[index];

        byte[] tableBuffer = buffers.TableHeaderBuffer.Concat(buffers.TableNodeBuffer.Concat(buffers.TableEntryBuffer)).ToArray();
        using var tableStorage = new ValueSubStorage(new MemoryStorage(tableBuffer), 0, tableBuffer.Length);

        using var originalStorage = new ValueSubStorage(new MemoryStorage(buffers.OriginalStorageBuffer), 0, buffers.OriginalStorageBuffer.Length);
        using var patchStorage = new ValueSubStorage(new MemoryStorage(buffers.PatchStorageBuffer), 0, buffers.PatchStorageBuffer.Length);

        using var storage = new IndirectStorage();
        Assert.Success(storage.Initialize(new ArrayPoolMemoryResource(), in tableStorage));
        storage.SetStorage(0, in originalStorage);
        storage.SetStorage(1, in patchStorage);

        byte[] expectedPatchedData = _storageBuffers[index].PatchedStorageBuffer;
        byte[] actualPatchedData = new byte[expectedPatchedData.Length];

        Assert.Success(storage.GetSize(out long storageSize));
        Assert.Equal(actualPatchedData.Length, storageSize);

        Assert.Success(storage.Read(0, actualPatchedData));
        Assert.True(expectedPatchedData.SequenceEqual(actualPatchedData));
    }

    [Theory, MemberData(nameof(IndirectStorageTestTheoryData))]
    public void Read_RandomAccess_DataIsCorrect(int index)
    {
        foreach (RandomAccessTestConfig accessConfig in AccessTestConfigs)
        {
            StorageTester tester = SetupRandomAccessTest(index, accessConfig, true);
            tester.Run(0x1000);
        }
    }

    [Theory, MemberData(nameof(IndirectStorageTestTheoryData))]
    public void GetEntryList_GetAllEntries_ReturnsCorrectEntries(int index)
    {
        GetEntryListTestImpl(index, 0, _storageBuffers[index].PatchedStorageBuffer.Length);
    }

    [Theory, MemberData(nameof(IndirectStorageTestTheoryData))]
    public void GetEntryList_GetPartialEntries_ReturnsCorrectEntries(int index)
    {
        IndirectStorageData buffers = _storageBuffers[index];
        var random = new Random(IndirectStorageTestData[index].RngSeed);

        int endOffset = buffers.PatchedStorageBuffer.Length;
        int maxSize = endOffset / 2;
        const int testCount = 100;

        for (int i = 0; i < testCount; i++)
        {
            long offset = random.Next(0, endOffset);
            long size = Math.Min(endOffset - offset, random.Next(0, maxSize));

            GetEntryListTestImpl(index, offset, size);
        }

        GetEntryListTestImpl(index, 0, _storageBuffers[index].PatchedStorageBuffer.Length);
    }

    private void GetEntryListTestImpl(int index, long offset, long size)
    {
        Assert.True(size > 0);

        IndirectStorageData buffers = _storageBuffers[index];
        using IndirectStorage storage = buffers.CreateIndirectStorage(false);
        IndirectStorage.Entry[] entries = buffers.TableEntries;
        int endOffset = buffers.PatchedStorageBuffer.Length;

        int startIndex = FindEntry(entries, offset, endOffset);
        int endIndex = FindEntry(entries, offset + size - 1, endOffset);
        int count = endIndex - startIndex + 1;

        Span<IndirectStorage.Entry> expectedEntries = buffers.TableEntries.AsSpan(startIndex, count);
        var actualEntries = new IndirectStorage.Entry[expectedEntries.Length + 1];

        Assert.Success(storage.GetEntryList(actualEntries, out int entryCount, offset, size));

        Assert.Equal(expectedEntries.Length, entryCount);
        Assert.True(actualEntries.AsSpan(0, entryCount).SequenceEqual(expectedEntries));
    }

    private int FindEntry(IndirectStorage.Entry[] entries, long offset, long endOffset)
    {
        Assert.True(offset >= 0);
        Assert.True(offset < endOffset);

        for (int i = 0; i + 1 < entries.Length; i++)
        {
            if (offset >= entries[i].GetVirtualOffset() && offset < entries[i + 1].GetVirtualOffset())
                return i;
        }

        return entries.Length - 1;
    }

    [Theory, MemberData(nameof(IndirectStorageTestTheoryData))]
    public void SparseStorage_Read_EntireStorageInSingleRead_DataIsCorrect(int index)
    {
        IndirectStorageData buffers = _storageBuffers[index];
        using SparseStorage storage = buffers.CreateSparseStorage();

        byte[] expectedPatchedData = buffers.OriginalStorageBuffer;
        byte[] actualPatchedData = new byte[expectedPatchedData.Length];

        Assert.Success(storage.GetSize(out long storageSize));
        Assert.Equal(actualPatchedData.Length, storageSize);

        Assert.Success(storage.Read(0, actualPatchedData));
        Assert.True(expectedPatchedData.SequenceEqual(actualPatchedData));
    }

    [Theory, MemberData(nameof(IndirectStorageTestTheoryData))]
    public void SparseStorage_Read_RandomAccess_DataIsCorrect(int index)
    {
        foreach (RandomAccessTestConfig accessConfig in AccessTestConfigs)
        {
            StorageTester tester = SetupRandomAccessTest(index, accessConfig, true);
            tester.Run(0x1000);
        }
    }

    private StorageTester SetupRandomAccessTest(int storageConfigIndex, RandomAccessTestConfig accessConfig, bool getSparseStorage)
    {
        IStorage indirectStorage = getSparseStorage
            ? _storageBuffers[storageConfigIndex].CreateSparseStorage()
            : _storageBuffers[storageConfigIndex].CreateIndirectStorage(false);

        Assert.Success(indirectStorage.GetSize(out long storageSize));

        byte[] expectedStorageArray = new byte[storageSize];
        Assert.Success(indirectStorage.Read(0, expectedStorageArray));

        var memoryStorage = new MemoryStorage(expectedStorageArray);

        var memoryStorageEntry = new StorageTester.Entry(memoryStorage, expectedStorageArray);
        var indirectStorageEntry = new StorageTester.Entry(indirectStorage, expectedStorageArray);

        var testerConfig = new StorageTester.Configuration()
        {
            Entries = [memoryStorageEntry, indirectStorageEntry],
            SizeClassProbs = accessConfig.SizeClassProbs,
            SizeClassMaxSizes = accessConfig.SizeClassMaxSizes,
            TaskProbs = accessConfig.TaskProbs,
            AccessTypeProbs = accessConfig.AccessTypeProbs,
            RngSeed = accessConfig.RngSeed,
            FrequentAccessBlockCount = accessConfig.FrequentAccessBlockCount
        };

        return new StorageTester(testerConfig);
    }
}