using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Tests.Common;
using LibHac.Util;
using Xunit;

namespace LibHac.Tests.FsSystem;

public class BucketTreeBuilderTests
{
    public class BucketTreeBuilderTestConfig
    {
        public string ExpectedHeaderDigest { get; init; }
        public string ExpectedNodeDigest { get; init; }
        public string ExpectedEntryDigest { get; init; }
        public ulong RngSeed { get; init; }
        public SizeRange EntrySizeRange { get; init; }
        public int NodeSize { get; init; }
        public int EntryCount { get; init; }
    }

    // Bucket tree builder parameters and output digests that have been verified manually
    private static readonly BucketTreeBuilderTestConfig[] BucketTreeBuilderTestData =
    {
        // Tiny tree
        new()
        {
            ExpectedHeaderDigest = "34C3355A9C67F91A978FD8CD51A1FB69FB4A6575FA93EEA03FF94E3FDA4FF918",
            ExpectedNodeDigest = "38B1BAA521BBD24204A2846A184C276DAA46065964910A5FC132BED73187B9F2",
            ExpectedEntryDigest = "7D61723D0A332128713120961E607188F50A8870360328594F4A5CC1731B10EE",
            RngSeed = 0,
            EntrySizeRange = new SizeRange(0x1000, 1, 10),
            NodeSize = 0x4000,
            EntryCount = 5
        },
        // Slightly larger tree
        new()
        {
            ExpectedHeaderDigest = "B297BF6EE037B9179CA78618D73B1F51F4C980DF18CA79D00BD99EA3CB801491",
            ExpectedNodeDigest = "DEB446E4EF36937ED253D912D48BCB74C9745E55647E3B900B3730379285580F",
            ExpectedEntryDigest = "ED8CBA7E42A03D9399562A577E5FE3203DCA6CDAEA44F9EB9D6EFEC174638AE1",
            RngSeed = 0,
            EntrySizeRange = new SizeRange(0x1000, 1, 10),
            NodeSize = 0x4000,
            EntryCount = 10000
        },
        // Very large tree that contains a L2 node
        new()
        {
            ExpectedHeaderDigest = "D36E9BC6C618637F3C615A861826DEE9CA8E0AB37C51D7124D0112E2B2D666C2",
            ExpectedNodeDigest = "FBB238FFAF8A7585A1413CA9BF12E0C70BCF2B12DA3399F1077C6E3D364886B9",
            ExpectedEntryDigest = "F3A452EC58B7C937E6AACC31680CAFAEEA63B0BA4D26F7A2EAEAF2FF11ABCF26",
            RngSeed = 0,
            EntrySizeRange = new SizeRange(0x1000, 1, 10),
            NodeSize = 0x4000,
            EntryCount = 2_000_000
        },
        // Tree with node size of 0x400 containing multiple L2 nodes
        new()
        {
            ExpectedHeaderDigest = "B0520728AAD615F48BD45EAD1D8BC953AE0B912C5DB9429DD8DF2BC7B656FBEC",
            ExpectedNodeDigest = "F785D455960298F7EABAD6E1997CE1FD298BFD802788E84E35FBA4E65FCE90E9",
            ExpectedEntryDigest = "B467120D77D2ECBD039D9E171F8D604D3F3ED7C60C3551878EF21ED52B02690C",
            RngSeed = 0,
            EntrySizeRange = new SizeRange(0x1000, 1, 10),
            NodeSize = 0x400,
            EntryCount = 50_000
        },
        // Tree with node size of 0x400 containing the maximum number of entries possible with that node size
        new()
        {
            ExpectedHeaderDigest = "33C6DBFDC95C8F5DC75DFE1BD027E9943FAA1B90DEB33039827860BCEC31CAA2",
            ExpectedNodeDigest = "A732F462E8D545C7409FFB5DE6BDB460A3D466BDBD730173A453FD81C82AA38C",
            ExpectedEntryDigest = "9EE6FBA4E0D336A7082EF46EC64FD8CEC2BAA5C8CF760C357B9193FE37A04CE3",
            RngSeed = 0,
            EntrySizeRange = new SizeRange(0x1000, 1, 10),
            NodeSize = 0x400,
            EntryCount = 793_800
        }
    };

    public static TheoryData<int> BucketTreeBuilderTestTheoryData =
        TheoryDataCreator.CreateSequence(0, BucketTreeBuilderTestData.Length);

    [Theory, MemberData(nameof(BucketTreeBuilderTestTheoryData))]
    public void BuildTree_TreeIsGeneratedCorrectly(int index)
    {
        BucketTreeBuilderTestConfig config = BucketTreeBuilderTestData[index];

        BucketTreeTests.BucketTreeData data = BucketTreeCreator.Create(config.RngSeed, config.EntrySizeRange,
            config.NodeSize, config.EntryCount);

        byte[] headerDigest = new byte[0x20];
        byte[] nodeDigest = new byte[0x20];
        byte[] entryDigest = new byte[0x20];

        Crypto.Sha256.GenerateSha256Hash(data.Header, headerDigest);
        Crypto.Sha256.GenerateSha256Hash(data.Nodes, nodeDigest);
        Crypto.Sha256.GenerateSha256Hash(data.Entries, entryDigest);

        Assert.Equal(config.ExpectedHeaderDigest, headerDigest.ToHexString());
        Assert.Equal(config.ExpectedNodeDigest, nodeDigest.ToHexString());
        Assert.Equal(config.ExpectedEntryDigest, entryDigest.ToHexString());
    }

    [Fact]
    public void Initialize_TooManyEntries_ReturnsException()
    {
        Assert.Throws<HorizonResultException>(() =>
            BucketTreeCreator.Create(0, new SizeRange(0x1000, 1, 10), 0x400, 793_801));
    }

    [Fact]
    public void Finalize_NotAllEntriesAdded_ReturnsOutOfRange()
    {
        const int nodeSize = 0x4000;
        const int entryCount = 10;

        byte[] headerBuffer = new byte[BucketTree.QueryHeaderStorageSize()];
        byte[] nodeBuffer = new byte[(int)BucketTree.QueryNodeStorageSize(nodeSize, Unsafe.SizeOf<IndirectStorage.Entry>(), entryCount)];
        byte[] entryBuffer = new byte[(int)BucketTree.QueryEntryStorageSize(nodeSize, Unsafe.SizeOf<IndirectStorage.Entry>(), entryCount)];

        using var headerStorage = new ValueSubStorage(new MemoryStorage(headerBuffer), 0, headerBuffer.Length);
        using var nodeStorage = new ValueSubStorage(new MemoryStorage(nodeBuffer), 0, nodeBuffer.Length);
        using var entryStorage = new ValueSubStorage(new MemoryStorage(entryBuffer), 0, entryBuffer.Length);

        var builder = new BucketTree.Builder();

        Assert.Success(builder.Initialize(new ArrayPoolMemoryResource(), in headerStorage, in nodeStorage, in entryStorage, nodeSize, Unsafe.SizeOf<IndirectStorage.Entry>(), entryCount));

        var entry = new IndirectStorage.Entry();
        Assert.Success(builder.Add(in entry));

        Assert.Result(ResultFs.OutOfRange, builder.Finalize(0x1000));
    }

    [Fact]
    public void Finalize_InvalidEndOffset_ReturnsInvalidOffset()
    {
        const int nodeSize = 0x4000;
        const int entryCount = 2;

        byte[] headerBuffer = new byte[BucketTree.QueryHeaderStorageSize()];
        byte[] nodeBuffer = new byte[(int)BucketTree.QueryNodeStorageSize(nodeSize, Unsafe.SizeOf<IndirectStorage.Entry>(), entryCount)];
        byte[] entryBuffer = new byte[(int)BucketTree.QueryEntryStorageSize(nodeSize, Unsafe.SizeOf<IndirectStorage.Entry>(), entryCount)];

        using var headerStorage = new ValueSubStorage(new MemoryStorage(headerBuffer), 0, headerBuffer.Length);
        using var nodeStorage = new ValueSubStorage(new MemoryStorage(nodeBuffer), 0, nodeBuffer.Length);
        using var entryStorage = new ValueSubStorage(new MemoryStorage(entryBuffer), 0, entryBuffer.Length);

        var builder = new BucketTree.Builder();

        Assert.Success(builder.Initialize(new ArrayPoolMemoryResource(), in headerStorage, in nodeStorage, in entryStorage, nodeSize, Unsafe.SizeOf<IndirectStorage.Entry>(), entryCount));

        var entry = new IndirectStorage.Entry();
        Assert.Success(builder.Add(in entry));

        entry.SetVirtualOffset(0x10000);
        Assert.Success(builder.Add(in entry));

        Assert.Result(ResultFs.InvalidOffset, builder.Finalize(0x1000));
    }
}