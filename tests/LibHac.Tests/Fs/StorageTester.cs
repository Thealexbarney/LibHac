using System;
using System.IO;
using System.Linq;
using LibHac.Fs;
using LibHac.Util;

namespace LibHac.Tests.Fs;

public class StorageTester
{
    private Random _random;
    private byte[][] _backingArrays;
    private byte[][] _buffers;
    private int _size;

    private int[] _accessParamProbs;

    private int[] _frequentAccessOffsets;
    private int _lastAccessEnd;
    private int _totalAccessCount;
    private Configuration _config;

    public class Configuration
    {
        public Entry[] Entries { get; set; }
        public AccessParam[] AccessParams { get; set; }
        public int[] TaskProbs { get; set; }
        public int[] AccessTypeProbs { get; set; }
        public ulong RngSeed { get; set; }
        public int FrequentAccessBlockCount { get; set; }
    }

    public StorageTester(Configuration config)
    {
        Entry[] entries = config.Entries;

        if (entries.Length < 2)
        {
            throw new ArgumentException("At least 2 storage entries must be provided", nameof(config));
        }

        if (entries.Select(x => x.BackingArray.Length).Distinct().Count() != 1)
        {
            throw new ArgumentException("All storages must have the same size.", nameof(config));
        }

        if (entries[0].BackingArray.Length == 0)
        {
            throw new ArgumentException("The storage size must be greater than 0.", nameof(config));
        }

        if (config.AccessParams.SelectMany<AccessParam, int>(x => [x.OffsetAlignment, x.SizeAlignment]).Any(x => x != 0 && !BitUtil.IsPowerOfTwo(x)))
        {
            throw new ArgumentException("All alignments must be 0 or powers of 2.", nameof(config));
        }

        _config = config;
        _random = new Random(config.RngSeed);

        _accessParamProbs = config.AccessParams.Select(x => x.Prob).ToArray();
        _backingArrays = entries.Select(x => x.BackingArray).ToArray();

        _buffers = new byte[entries.Length][];
        for (int i = 0; i < entries.Length; i++)
        {
            _buffers[i] = new byte[config.AccessParams.Max(x => x.MaxSize)];
        }

        _size = entries[0].BackingArray.Length;
        _lastAccessEnd = 0;

        _frequentAccessOffsets = new int[config.FrequentAccessBlockCount];
        for (int i = 0; i < _frequentAccessOffsets.Length; i++)
        {
            _frequentAccessOffsets[i] = _random.Next(0, _size);
        }
    }

    public void Run(long accessCount)
    {
        long endCount = _totalAccessCount + accessCount;

        while (_totalAccessCount < endCount)
        {
            Task task = ChooseTask();
            switch (task)
            {
                case Task.Read:
                    RunRead();
                    break;
                case Task.Write:
                    RunWrite();
                    break;
                case Task.Flush:
                    RunFlush();
                    break;
            }

            _totalAccessCount++;
        }
    }

    private void RunRead()
    {
        AccessParam accessParams = ChooseAccessRangeParams();
        AccessType accessType = ChooseAccessType();

        int offset = ChooseOffset(accessType, accessParams);
        int size = ChooseSize(offset, accessParams);

        for (int i = 0; i < _config.Entries.Length; i++)
        {
            Entry entry = _config.Entries[i];
            entry.Storage.Read(offset, _buffers[i].AsSpan(0, size)).ThrowIfFailure();
        }

        if (!CompareBuffers(_buffers, size))
        {
            throw new InvalidDataException($"Read: Offset {offset}; Size {size}");
        }

        _lastAccessEnd = offset + size;
    }

    private void RunWrite()
    {
        AccessParam accessParams = ChooseAccessRangeParams();
        AccessType accessType = ChooseAccessType();

        int offset = ChooseOffset(accessType, accessParams);
        int size = ChooseSize(offset, accessParams);

        Span<byte> buffer = _buffers[0].AsSpan(0, size);
        _random.NextBytes(buffer);

        for (int i = 0; i < _config.Entries.Length; i++)
        {
            Entry entry = _config.Entries[i];
            entry.Storage.Write(offset, buffer).ThrowIfFailure();
        }

        _lastAccessEnd = offset + size;
    }

    private void RunFlush()
    {
        foreach (Entry entry in _config.Entries)
        {
            entry.Storage.Flush().ThrowIfFailure();
        }

        if (!CompareBuffers(_backingArrays, _size))
        {
            throw new InvalidDataException("Flush");
        }
    }

    private Task ChooseTask() => (Task)ChooseProb(_config.TaskProbs);
    private AccessParam ChooseAccessRangeParams() => _config.AccessParams[ChooseProb(_accessParamProbs)];
    private AccessType ChooseAccessType() => (AccessType)ChooseProb(_config.AccessTypeProbs);

    private int ChooseOffset(AccessType type, AccessParam accessParams)
    {
        int offset = type switch
        {
            AccessType.Random => _random.Next(0, _size),
            AccessType.Sequential => _lastAccessEnd == _size ? 0 : _lastAccessEnd,
            AccessType.FrequentBlock => _frequentAccessOffsets[_random.Next(0, _frequentAccessOffsets.Length)],
            _ => 0
        };
        return Align(offset, accessParams.OffsetAlignment);
    }

    private int ChooseSize(int offset, AccessParam sizeRange)
    {
        int availableSize = Math.Max(0, _size - offset);
        int randSize = Align(_random.Next(sizeRange.MinSize, sizeRange.MaxSize), sizeRange.SizeAlignment);
        return Math.Min(availableSize, randSize);
    }

    private int Align(int value, int alignment)
    {
        if (alignment == 0)
            return value;

        return Alignment.AlignDown(value, alignment);
    }

    private int ChooseProb(int[] weights)
    {
        int total = 0;
        foreach (int weight in weights)
        {
            total += weight;
        }

        int rand = _random.Next(0, total);
        int currentThreshold = 0;

        for (int i = 0; i < weights.Length; i++)
        {
            currentThreshold += weights[i];

            if (rand < currentThreshold)
                return i;
        }

        return 0;
    }

    private bool CompareBuffers(byte[][] buffers, int size)
    {
        Span<byte> baseBuffer = buffers[0].AsSpan(0, size);

        for (int i = 1; i < buffers.Length; i++)
        {
            Span<byte> testBuffer = buffers[i].AsSpan(0, size);
            if (!baseBuffer.SequenceEqual(testBuffer))
            {
                return false;
            }
        }

        return true;
    }

    public readonly struct Entry
    {
        public readonly IStorage Storage;
        public readonly byte[] BackingArray;

        public Entry(IStorage storage, byte[] backingArray)
        {
            Storage = storage;
            BackingArray = backingArray;
        }
    }

    public readonly struct AccessParam
    {
        public readonly int Prob;
        public readonly int MinSize;
        public readonly int MaxSize;
        public readonly int OffsetAlignment;
        public readonly int SizeAlignment;

        public AccessParam(int prob, int minSize, int maxSize, int offsetAlignment, int sizeAlignment)
        {
            Prob = prob;
            MinSize = minSize;
            MaxSize = maxSize;
            OffsetAlignment = offsetAlignment;
            SizeAlignment = sizeAlignment;
        }

        public AccessParam(int prob, int minSize, int maxSize)
        {
            Prob = prob;
            MinSize = minSize;
            MaxSize = maxSize;
        }

        public AccessParam(int prob, int maxSize)
        {
            Prob = prob;
            MinSize = 0;
            MaxSize = maxSize;
        }
    }

    private enum Task
    {
        Read = 0,
        Write = 1,
        Flush = 2
    }

    private enum AccessType
    {
        Random = 0,
        Sequential = 1,
        FrequentBlock = 2
    }
}