using System.Runtime.CompilerServices;
using LibHac.Fs;
using LibHac.FsSystem;
using Xunit;

namespace LibHac.Tests.FsSystem;

public record struct SizeRange(int BlockSize, int MinBlockCount, int MaxBlockCount);

internal static class BucketTreeCreator
{
    public class EntryGenerator
    {
        private Random _random;

        private readonly SizeRange _originalEntrySizeRange;
        private readonly SizeRange _patchEntrySizeRange;

        private IndirectStorage.Entry _currentEntry;

        private long _originalStorageOffset;
        private long _patchStorageOffset;
        private long _patchedStorageOffset;
        private bool _isOriginal;

        public IndirectStorage.Entry CurrentEntry => _currentEntry;
        public int CurrentEntryIndex { get; private set; }
        public int CurrentEntrySize { get; private set; }

        public long OriginalStorageSize => _originalStorageOffset;
        public long PatchStorageSize => _patchStorageOffset;
        public long PatchedStorageSize => _patchedStorageOffset;

        public EntryGenerator(ulong rngSeed, SizeRange entrySizes)
        {
            _random = new Random(rngSeed);
            _originalEntrySizeRange = entrySizes;
            _patchEntrySizeRange = entrySizes;
            _isOriginal = false;

            CurrentEntryIndex = -1;
        }

        public EntryGenerator(ulong rngSeed, SizeRange originalEntrySizes, SizeRange patchEntrySizes)
        {
            _random = new Random(rngSeed);
            _originalEntrySizeRange = originalEntrySizes;
            _patchEntrySizeRange = patchEntrySizes;
            _isOriginal = false;

            CurrentEntryIndex = -1;
        }

        public void MoveNext()
        {
            _isOriginal = !_isOriginal;

            SizeRange range = _isOriginal ? _originalEntrySizeRange : _patchEntrySizeRange;

            int blockCount = _random.Next(range.MinBlockCount, range.MaxBlockCount);
            int entrySize = blockCount * range.BlockSize;

            CurrentEntryIndex++;
            CurrentEntrySize = entrySize;

            _currentEntry.SetVirtualOffset(_patchedStorageOffset);

            _patchedStorageOffset += entrySize;

            if (_isOriginal)
            {
                _currentEntry.SetPhysicalOffset(_originalStorageOffset);
                _currentEntry.StorageIndex = 0;

                _originalStorageOffset += entrySize;
            }
            else
            {
                _currentEntry.SetPhysicalOffset(_patchStorageOffset);
                _currentEntry.StorageIndex = 1;

                // Advance the original offset too to account for the data that's being replaced in the original storage
                _originalStorageOffset += entrySize;
                _patchStorageOffset += entrySize;
            }
        }
    }

    public static BucketTreeTests.BucketTreeData Create(ulong rngSeed, SizeRange entrySizes, int nodeSize, int entryCount)
    {
        byte[] headerBuffer = new byte[BucketTree2.QueryHeaderStorageSize()];
        byte[] nodeBuffer = new byte[(int)BucketTree2.QueryNodeStorageSize(nodeSize, Unsafe.SizeOf<IndirectStorage.Entry>(), entryCount)];
        byte[] entryBuffer = new byte[(int)BucketTree2.QueryEntryStorageSize(nodeSize, Unsafe.SizeOf<IndirectStorage.Entry>(), entryCount)];

        using var headerStorage = new ValueSubStorage(new MemoryStorage(headerBuffer), 0, headerBuffer.Length);
        using var nodeStorage = new ValueSubStorage(new MemoryStorage(nodeBuffer), 0, nodeBuffer.Length);
        using var entryStorage = new ValueSubStorage(new MemoryStorage(entryBuffer), 0, entryBuffer.Length);

        var generator = new EntryGenerator(rngSeed, entrySizes);
        var builder = new BucketTree2.Builder();

        Assert.Success(builder.Initialize(new ArrayPoolMemoryResource(), in headerStorage, in nodeStorage,
            in entryStorage, nodeSize, Unsafe.SizeOf<IndirectStorage.Entry>(), entryCount));

        for (int i = 0; i < entryCount; i++)
        {
            generator.MoveNext();
            IndirectStorage.Entry entry = generator.CurrentEntry;

            Assert.Success(builder.Add(in entry));
        }

        Assert.Success(builder.Finalize(generator.PatchedStorageSize));

        return new BucketTreeTests.BucketTreeData
        {
            NodeSize = nodeSize,
            EntryCount = entryCount,
            Header = headerBuffer,
            Nodes = nodeBuffer,
            Entries = entryBuffer
        };
    }

    public static IndirectStorage.Entry[] GenerateEntries(ulong rngSeed, SizeRange entrySizeRange, int entryCount)
    {
        var entries = new IndirectStorage.Entry[entryCount];

        var generator = new EntryGenerator(rngSeed, entrySizeRange);

        for (int i = 0; i < entryCount; i++)
        {
            generator.MoveNext();
            entries[i] = generator.CurrentEntry;
        }

        return entries;
    }
}