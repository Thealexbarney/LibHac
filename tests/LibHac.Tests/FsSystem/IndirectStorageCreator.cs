using System;
using System.Runtime.CompilerServices;
using LibHac.Fs;
using LibHac.FsSystem;
using Xunit;

namespace LibHac.Tests.FsSystem;

internal class IndirectStorageCreator
{
    private const int NodeSize = 0x4000;

    private readonly ulong _rngSeed;
    private readonly long _targetSize;
    private readonly SizeRange _originalEntrySizeRange;
    private readonly SizeRange _patchEntrySizeRange;

    private int _maxEntrySize;
    private int _entryCount;

    private IndirectStorageTests.IndirectStorageData _buffers;

    public static IndirectStorageTests.IndirectStorageData Create(ulong rngSeed, SizeRange originalEntrySizeRange,
        SizeRange patchEntrySizeRange, long storageSize)
    {
        return new IndirectStorageCreator(rngSeed, originalEntrySizeRange, patchEntrySizeRange, storageSize)._buffers;
    }

    private IndirectStorageCreator(ulong rngSeed, SizeRange originalEntrySizeRange, SizeRange patchEntrySizeRange, long storageSize)
    {
        _rngSeed = rngSeed;
        _originalEntrySizeRange = originalEntrySizeRange;
        _patchEntrySizeRange = patchEntrySizeRange;
        _targetSize = storageSize;

        CreateBuffers();
        FillBuffers();
    }

    private void CreateBuffers()
    {
        var generator = new BucketTreeCreator.EntryGenerator(_rngSeed, _originalEntrySizeRange, _patchEntrySizeRange);
        generator.MoveNext();
        _maxEntrySize = 0;

        long originalSize = 0, patchSize = 0, sparseOriginalSize = 0;

        while (generator.PatchedStorageSize < _targetSize)
        {
            _maxEntrySize = Math.Max(_maxEntrySize, generator.CurrentEntrySize);
            originalSize = generator.OriginalStorageSize;
            patchSize = generator.PatchStorageSize;
            sparseOriginalSize = originalSize - patchSize;

            generator.MoveNext();
        }

        _entryCount = generator.CurrentEntryIndex;

        _buffers = new()
        {
            OriginalStorageBuffer = new byte[originalSize],
            SparseOriginalStorageBuffer = new byte[sparseOriginalSize],
            PatchStorageBuffer = new byte[patchSize],
            PatchedStorageBuffer = new byte[originalSize],
            TableEntries = new IndirectStorage.Entry[_entryCount],
            TableHeaderBuffer = new byte[BucketTree.QueryHeaderStorageSize()],
            TableNodeBuffer = new byte[(int)BucketTree.QueryNodeStorageSize(NodeSize, Unsafe.SizeOf<IndirectStorage.Entry>(), _entryCount)],
            TableEntryBuffer = new byte[(int)BucketTree.QueryEntryStorageSize(NodeSize, Unsafe.SizeOf<IndirectStorage.Entry>(), _entryCount)],
            SparseTableHeaderBuffer = new byte[BucketTree.QueryHeaderStorageSize()],
            SparseTableNodeBuffer = new byte[(int)BucketTree.QueryNodeStorageSize(NodeSize, Unsafe.SizeOf<IndirectStorage.Entry>(), _entryCount)],
            SparseTableEntryBuffer = new byte[(int)BucketTree.QueryEntryStorageSize(NodeSize, Unsafe.SizeOf<IndirectStorage.Entry>(), _entryCount)]
        };
    }

    private void FillBuffers()
    {
        byte[] randomBuffer = new byte[_maxEntrySize];
        var generator = new BucketTreeCreator.EntryGenerator(_rngSeed, _originalEntrySizeRange, _patchEntrySizeRange);

        using var headerStorage = new ValueSubStorage(new MemoryStorage(_buffers.TableHeaderBuffer), 0, _buffers.TableHeaderBuffer.Length);
        using var nodeStorage = new ValueSubStorage(new MemoryStorage(_buffers.TableNodeBuffer), 0, _buffers.TableNodeBuffer.Length);
        using var entryStorage = new ValueSubStorage(new MemoryStorage(_buffers.TableEntryBuffer), 0, _buffers.TableEntryBuffer.Length);

        using var sparseHeaderStorage = new ValueSubStorage(new MemoryStorage(_buffers.SparseTableHeaderBuffer), 0, _buffers.SparseTableHeaderBuffer.Length);
        using var sparseNodeStorage = new ValueSubStorage(new MemoryStorage(_buffers.SparseTableNodeBuffer), 0, _buffers.SparseTableNodeBuffer.Length);
        using var sparseEntryStorage = new ValueSubStorage(new MemoryStorage(_buffers.SparseTableEntryBuffer), 0, _buffers.SparseTableEntryBuffer.Length);

        var builder = new BucketTree.Builder();
        var sparseTableBuilder = new BucketTree.Builder();

        Assert.Success(builder.Initialize(new ArrayPoolMemoryResource(), in headerStorage, in nodeStorage,
            in entryStorage, NodeSize, Unsafe.SizeOf<IndirectStorage.Entry>(), _entryCount));

        Assert.Success(sparseTableBuilder.Initialize(new ArrayPoolMemoryResource(), in sparseHeaderStorage,
            in sparseNodeStorage, in sparseEntryStorage, NodeSize, Unsafe.SizeOf<IndirectStorage.Entry>(),
            _entryCount));

        var random = new Random(_rngSeed);

        int originalStorageOffset = 0;
        int sparseOriginalStorageOffset = 0;
        int patchStorageOffset = 0;
        int patchedStorageOffset = 0;

        for (int i = 0; i < _entryCount; i++)
        {
            generator.MoveNext();

            IndirectStorage.Entry entry = generator.CurrentEntry;

            IndirectStorage.Entry sparseEntry = generator.CurrentEntry;
            sparseEntry.SetPhysicalOffset(sparseOriginalStorageOffset);

            Assert.Success(builder.Add(in entry));
            Assert.Success(sparseTableBuilder.Add(in sparseEntry));

            _buffers.TableEntries[i] = entry;

            Span<byte> randomData = randomBuffer.AsSpan(0, generator.CurrentEntrySize);
            random.NextBytes(randomData);

            if (entry.StorageIndex == 0)
            {
                randomData.CopyTo(_buffers.OriginalStorageBuffer.AsSpan(originalStorageOffset));
                randomData.CopyTo(_buffers.SparseOriginalStorageBuffer.AsSpan(sparseOriginalStorageOffset));
                randomData.CopyTo(_buffers.PatchedStorageBuffer.AsSpan(patchedStorageOffset));

                originalStorageOffset += randomData.Length;
                sparseOriginalStorageOffset += randomData.Length;
                patchedStorageOffset += randomData.Length;
            }
            else
            {
                // Fill the unused portions of the original storage with zeros so it matches the sparse original storage
                _buffers.OriginalStorageBuffer.AsSpan(originalStorageOffset, generator.CurrentEntrySize);
                randomData.CopyTo(_buffers.PatchStorageBuffer.AsSpan(patchStorageOffset));
                randomData.CopyTo(_buffers.PatchedStorageBuffer.AsSpan(patchedStorageOffset));

                originalStorageOffset += randomData.Length;
                patchStorageOffset += randomData.Length;
                patchedStorageOffset += randomData.Length;
            }
        }

        Assert.Success(builder.Finalize(generator.PatchedStorageSize));
        Assert.Success(sparseTableBuilder.Finalize(generator.PatchedStorageSize));

        Assert.Equal(_buffers.OriginalStorageBuffer.Length, originalStorageOffset);
        Assert.Equal(_buffers.SparseOriginalStorageBuffer.Length, sparseOriginalStorageOffset);
        Assert.Equal(_buffers.PatchStorageBuffer.Length, patchStorageOffset);
        Assert.Equal(_buffers.PatchedStorageBuffer.Length, patchedStorageOffset);
    }
}