﻿using System;
using System.Collections.Generic;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Tests.Fs;
using Xunit;

namespace LibHac.Tests.FsSystem;

public class BufferedStorageTests
{
    [Fact]
    public void Write_SingleBlock_CanReadBack()
    {
        byte[] buffer = new byte[0x18000];
        byte[] workBuffer = new byte[0x18000];
        var bufferManager = new FileSystemBufferManager();
        Assert.Success(bufferManager.Initialize(5, buffer, 0x4000, workBuffer));

        byte[] storageBuffer = new byte[0x80000];
        using var baseStorage = new ValueSubStorage(new MemoryStorage(storageBuffer), 0, storageBuffer.Length);

        var bufferedStorage = new BufferedStorage();
        Assert.Success(bufferedStorage.Initialize(in baseStorage, bufferManager, 0x4000, 4));

        byte[] writeBuffer = new byte[0x400];
        byte[] readBuffer = new byte[0x400];

        writeBuffer.AsSpan().Fill(0xAA);
        Assert.Success(bufferedStorage.Write(0x10000, writeBuffer));
        Assert.Success(bufferedStorage.Read(0x10000, readBuffer));

        Assert.Equal(writeBuffer, readBuffer);
    }

    public class AccessTestConfig
    {
        public int[] SizeClassProbs { get; set; }
        public int[] SizeClassMaxSizes { get; set; }
        public int[] TaskProbs { get; set; }
        public int[] AccessTypeProbs { get; set; }
        public ulong RngSeed { get; set; }
        public int FrequentAccessBlockCount { get; set; }
        public int BlockSize { get; set; }
        public int StorageCacheCount { get; set; }
        public bool EnableBulkRead { get; set; }
        public int StorageSize { get; set; }
        public int HeapSize { get; set; }
        public int HeapBlockSize { get; set; }
        public int BufferManagerCacheCount { get; set; }
    }

    public static AccessTestConfig[] AccessTestConfigs =
    [
        new()
        {
            SizeClassProbs = [50, 50, 5],
            SizeClassMaxSizes = [0x4000, 0x80000, 0x800000], // 16 KB, 512 KB, 8 MB
            TaskProbs = [50, 50, 1], // Read, Write, Flush
            AccessTypeProbs = [10, 10, 5], // Random, Sequential, Frequent block
            RngSeed = 35467,
            FrequentAccessBlockCount = 6,
            BlockSize = 0x4000,
            StorageCacheCount = 40,
            EnableBulkRead = true,
            StorageSize = 0x1000000,
            HeapSize = 0x180000,
            HeapBlockSize = 0x4000,
            BufferManagerCacheCount = 50
        },
        new()
        {
            SizeClassProbs = [50, 50, 5],
            SizeClassMaxSizes = [0x4000, 0x80000, 0x800000], // 16 KB, 512 KB, 8 MB
            TaskProbs = [50, 50, 1], // Read, Write, Flush
            AccessTypeProbs = [10, 10, 5], // Random, Sequential, Frequent block
            RngSeed = 6548433,
            FrequentAccessBlockCount = 6,
            BlockSize = 0x4000,
            StorageCacheCount = 40,
            EnableBulkRead = false,
            StorageSize = 0x1000000,
            HeapSize = 0x180000,
            HeapBlockSize = 0x4000,
            BufferManagerCacheCount = 50
        },
        new()
        {
            SizeClassProbs = [50, 50, 0],
            SizeClassMaxSizes = [0x4000, 0x80000, 0x800000], // 16 KB, 512 KB, 8 MB
            TaskProbs = [50, 0, 0],
            AccessTypeProbs = [10, 10, 5], // Random, Sequential, Frequent block
            RngSeed = 756478,
            FrequentAccessBlockCount = 16,
            BlockSize = 0x4000,
            StorageCacheCount = 8,
            EnableBulkRead = true,
            StorageSize = 0x1000000,
            HeapSize = 0xE00000,
            HeapBlockSize = 0x4000,
            BufferManagerCacheCount = 0x400
        },
        new()
        {
            SizeClassProbs = [50, 50, 0],
            SizeClassMaxSizes = [0x4000, 0x80000, 0x800000], // 16 KB, 512 KB, 8 MB
            TaskProbs = [50, 0, 0],
            AccessTypeProbs = [0, 0, 5], // Random, Sequential, Frequent block
            RngSeed = 38197549,
            FrequentAccessBlockCount = 16,
            BlockSize = 0x4000,
            StorageCacheCount = 16,
            EnableBulkRead = false,
            StorageSize = 0x1000000,
            HeapSize = 0xE00000,
            HeapBlockSize = 0x4000,
            BufferManagerCacheCount = 0x400
        },
        new()
        {
            SizeClassProbs = [50, 50, 0],
            SizeClassMaxSizes = [0x4000, 0x80000, 0x800000], // 16 KB, 512 KB, 8 MB
            TaskProbs = [50, 50, 1], // Read, Write, Flush
            AccessTypeProbs = [10, 10, 5], // Random, Sequential, Frequent block
            RngSeed = 567365,
            FrequentAccessBlockCount = 6,
            BlockSize = 0x4000,
            StorageCacheCount = 8,
            EnableBulkRead = false,
            StorageSize = 0x100000,
            HeapSize = 0x180000,
            HeapBlockSize = 0x4000,
            BufferManagerCacheCount = 50
        },
        new()
        {
            SizeClassProbs = [50, 50, 0],
            SizeClassMaxSizes = [0x4000, 0x80000, 0x800000], // 16 KB, 512 KB, 8 MB
            TaskProbs = [50, 50, 1], // Read, Write, Flush
            AccessTypeProbs = [10, 10, 5], // Random, Sequential, Frequent block
            RngSeed = 949365,
            FrequentAccessBlockCount = 6,
            BlockSize = 0x4000,
            StorageCacheCount = 8,
            EnableBulkRead = false,
            StorageSize = 0x100000,
            HeapSize = 0x180000,
            HeapBlockSize = 0x4000,
            BufferManagerCacheCount = 50
        },
        new()
        {
            SizeClassProbs = [50, 50, 10],
            SizeClassMaxSizes = [0x4000, 0x80000, 0x800000], // 16 KB, 512 KB, 8 MB
            TaskProbs = [50, 50, 1], // Read, Write, Flush
            AccessTypeProbs = [10, 10, 5], // Random, Sequential, Frequent block
            RngSeed = 670670,
            FrequentAccessBlockCount = 16,
            BlockSize = 0x4000,
            StorageCacheCount = 8,
            EnableBulkRead = true,
            StorageSize = 0x1000000,
            HeapSize = 0xE00000,
            HeapBlockSize = 0x4000,
            BufferManagerCacheCount = 0x400
        }
    ];

    private static TheoryData<T> CreateTheoryData<T>(IEnumerable<T> items)
    {
        var output = new TheoryData<T>();

        foreach (T item in items)
        {
            output.Add(item);
        }

        return output;
    }

    public static TheoryData<AccessTestConfig> AccessTestTheoryData = CreateTheoryData(AccessTestConfigs);

    [Theory]
    [MemberData(nameof(AccessTestTheoryData))]
    public void ReadWrite_AccessCorrectnessTestAgainstMemoryStorage(AccessTestConfig config)
    {
        int orderMax = FileSystemBuddyHeap.QueryOrderMax((nuint)config.HeapSize, (nuint)config.HeapBlockSize);
        int workBufferSize = (int)FileSystemBuddyHeap.QueryWorkBufferSize(orderMax);
        byte[] workBuffer = GC.AllocateArray<byte>(workBufferSize, true);
        byte[] heapBuffer = new byte[config.HeapSize];

        var bufferManager = new FileSystemBufferManager();
        Assert.Success(bufferManager.Initialize(config.BufferManagerCacheCount, heapBuffer, config.HeapBlockSize, workBuffer));

        byte[] memoryStorageArray = new byte[config.StorageSize];
        byte[] bufferedStorageArray = new byte[config.StorageSize];

        var memoryStorage = new MemoryStorage(memoryStorageArray);
        using var baseBufferedStorage = new ValueSubStorage(new MemoryStorage(bufferedStorageArray), 0, bufferedStorageArray.Length);

        var bufferedStorage = new BufferedStorage();
        Assert.Success(bufferedStorage.Initialize(in baseBufferedStorage, bufferManager, config.BlockSize, config.StorageCacheCount));

        if (config.EnableBulkRead)
        {
            bufferedStorage.EnableBulkRead();
        }

        var memoryStorageEntry = new StorageTester.Entry(memoryStorage, memoryStorageArray);
        var bufferedStorageEntry = new StorageTester.Entry(bufferedStorage, bufferedStorageArray);

        var accessParams = new StorageTester.AccessParam[config.SizeClassProbs.Length];
        for (int i = 0; i < accessParams.Length; i++)
        {
            accessParams[i] = new StorageTester.AccessParam(config.SizeClassProbs[i], config.SizeClassMaxSizes[i]);
        }

        var testerConfig = new StorageTester.Configuration
        {
            Entries = [memoryStorageEntry, bufferedStorageEntry],
            AccessParams = accessParams,
            TaskProbs = config.TaskProbs,
            AccessTypeProbs = config.AccessTypeProbs,
            RngSeed = config.RngSeed,
            FrequentAccessBlockCount = config.FrequentAccessBlockCount
        };

        var tester = new StorageTester(testerConfig);
        tester.Run(0x100);
    }
}