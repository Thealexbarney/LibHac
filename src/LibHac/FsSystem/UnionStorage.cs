using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Os;
using LibHac.Util;

namespace LibHac.FsSystem;

/// <summary>
/// An <see cref="IStorage"/> that allows modifying a base storage by writing any modifications to a separate
/// "log" storage.
/// </summary>
/// <remarks><para>The storage is split into equally-sized blocks. When a block is modified for the first time,
/// a new entry containing the block's data will be added to the log. Any subsequent read/write operations
/// on that block will use the log storage entry instead of the base storage.</para>
/// <para>Log format:
/// <code>
/// s64 BlockSize;
/// Entry Entries[];
///
/// Entry {
///    s64 BaseStorageOffset;
///    u8 Data[BlockSize];
/// };
/// </code></para>
/// <para>Based on nnSdk 13.4.0 (FS 13.1.0)</para></remarks>
public class UnionStorage : IStorage
{
    private const long Sentinel = -1;
    private const int LogHeaderSize = 8;
    private const int LogEntryHeaderSize = 8;

    private ValueSubStorage _baseStorage;
    private ValueSubStorage _logStorage;
    private long _blockSize;
    private byte[] _buffer;
    private int _blockCount;
    private SdkMutexType _mutex;

    public UnionStorage()
    {
        _mutex = new SdkMutexType();
    }

    public override void Dispose()
    {
        _baseStorage.Dispose();
        _logStorage.Dispose();
        _buffer = null;

        base.Dispose();
    }

    private static long GetLogSize(long blockSize)
    {
        return blockSize + LogEntryHeaderSize;
    }

    private static long GetDataOffset(long logOffset)
    {
        return logOffset + LogEntryHeaderSize;
    }

    private static long GetLogTailOffset(long blockSize, int blockCount)
    {
        return GetLogSize(blockSize) * blockCount + LogHeaderSize;
    }

    public static Result Format(in ValueSubStorage storage, long blockSize)
    {
        Assert.SdkRequiresGreater(blockSize, 1);
        Assert.SdkRequires(BitUtil.IsPowerOfTwo(blockSize));

        var header = new Array2<long>();
        header[0] = blockSize;
        header[1] = Sentinel;

        return storage.Write(0, SpanHelpers.AsReadOnlyByteSpan(in header));
    }

    public Result Initialize(in ValueSubStorage baseStorage, in ValueSubStorage logStorage, long blockSize)
    {
        Assert.SdkRequiresNull(_buffer);

        Result res = logStorage.Read(0, SpanHelpers.AsByteSpan(ref _blockSize));
        if (res.IsFailure()) return res.Miss();

        if (blockSize <= 1 || !BitUtil.IsPowerOfTwo(blockSize) || blockSize != _blockSize)
            return ResultFs.InvalidLogBlockSize.Log();

        // Read through the log to see if we already have any existing entries
        for (long offset = LogHeaderSize; ; offset += GetLogSize(_blockSize))
        {
            long offsetOriginal = 0;
            res = logStorage.Read(offset, SpanHelpers.AsByteSpan(ref offsetOriginal));
            if (res.IsFailure()) return res.Miss();

            if (offsetOriginal == Sentinel)
                break;

            if (offsetOriginal % _blockCount != 0)
                return ResultFs.InvalidLogOffset.Log();

            _blockCount++;
        }

        _baseStorage.Set(in baseStorage);
        _logStorage.Set(in logStorage);
        _buffer = new byte[_blockSize];

        return Result.Success;
    }

    public Result Freeze()
    {
        Assert.SdkRequiresNotNull(_buffer);

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        long tailOffset = GetLogTailOffset(_blockSize, _blockCount);
        long value = Sentinel;
        Result res = _logStorage.Write(tailOffset, SpanHelpers.AsReadOnlyByteSpan(in value));
        if (res.IsFailure()) return res.Miss();

        res = _logStorage.Flush();
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result Commit()
    {
        Assert.SdkRequiresNotNull(_buffer);

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        long tailOffset = GetLogTailOffset(_blockSize, _blockCount);
        long logSize = GetLogSize(_blockSize);

        // Read each block of data from the log storage and write it to the base storage
        for (long offset = LogHeaderSize; offset < tailOffset; offset += logSize)
        {
            long offsetOriginal = 0;
            Result res = _logStorage.Read(offset, SpanHelpers.AsByteSpan(ref offsetOriginal));
            if (res.IsFailure()) return res.Miss();

            if (offsetOriginal == Sentinel)
                return ResultFs.UnexpectedEndOfLog.Log();

            res = _logStorage.Read(GetDataOffset(offset), _buffer);
            if (res.IsFailure()) return res.Miss();

            res = _baseStorage.Write(offsetOriginal, _buffer);
            if (res.IsFailure()) return res.Miss();
        }

        return _baseStorage.Flush();
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        Assert.SdkRequiresNotNull(_buffer);

        if (destination.IsEmpty)
            return Result.Success;

        // Get the start offset of the block containing the requested offset
        long offsetBuffer = 0;
        long offsetOriginal = Alignment.AlignDownPow2(offset, _blockSize);
        long sizeSkipBlock = offset - offsetOriginal;

        while (offsetBuffer < destination.Length)
        {
            // Determine how much of the block we should read
            long sizeReadBlock = _blockSize - sizeSkipBlock;
            long sizeRemaining = destination.Length - offsetBuffer;
            long sizeToRead = Math.Min(sizeReadBlock, sizeRemaining);
            Span<byte> currentDestination = destination.Slice((int)offsetBuffer, (int)sizeToRead);

            // Check if the log contains the block we need
            Result res = FindLog(out bool found, out long offsetLog, offsetOriginal);
            if (res.IsFailure()) return res.Miss();

            // If it does, read from the log; otherwise read from the base storage
            if (found)
            {
                res = _logStorage.Read(GetDataOffset(offsetLog) + sizeSkipBlock, currentDestination);
                if (res.IsFailure()) return res.Miss();
            }
            else
            {
                res = _baseStorage.Read(offsetOriginal + sizeSkipBlock, currentDestination);
                if (res.IsFailure()) return res.Miss();
            }

            offsetBuffer += sizeToRead;
            offsetOriginal += _blockSize;
            sizeSkipBlock = 0;
        }

        return Result.Success;
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        Assert.SdkRequiresNotNull(_buffer);

        if (source.IsEmpty)
            return Result.Success;

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        // Get the start offset of the block containing the requested offset
        long offsetBuffer = 0;
        long offsetOriginal = Alignment.AlignDownPow2(offset, _blockSize);
        long sizeSkipBlock = offset - offsetOriginal;

        while (offsetBuffer < source.Length)
        {
            Assert.SdkNotEqual(Sentinel, offsetOriginal);

            long sizeWriteBlock = _blockSize - sizeSkipBlock;
            long sizeRemaining = source.Length - offsetBuffer;
            long sizeToWrite = Math.Min(sizeWriteBlock, sizeRemaining);
            ReadOnlySpan<byte> currentSource = source.Slice((int)offsetBuffer, (int)sizeToWrite);

            // Check if the log contains the block we need.
            Result res = FindLog(out bool found, out long offsetLog, offsetOriginal);
            if (res.IsFailure()) return res.Miss();

            if (found)
            {
                // If it does, write directly to the log.
                _logStorage.Write(GetDataOffset(offsetLog) + sizeSkipBlock, currentSource);
                if (res.IsFailure()) return res.Miss();
            }
            else
            {
                // Otherwise we need to add a new entry to the log.
                _logStorage.Write(offsetLog, SpanHelpers.AsReadOnlyByteSpan(in offsetOriginal));
                if (res.IsFailure()) return res.Miss();

                if (sizeToWrite == _blockSize)
                {
                    // If we're writing a complete block we can write the entire block directly to the log.
                    _logStorage.Write(GetDataOffset(offsetLog) + sizeSkipBlock, currentSource);
                    if (res.IsFailure()) return res.Miss();
                }
                else
                {
                    // If we're writing a partial block we need to read the existing data block from the base storage
                    // into a buffer first.
                    _baseStorage.Read(offsetOriginal, _buffer);
                    if (res.IsFailure()) return res.Miss();

                    // Fill in the appropriate parts of the buffer with our new data.
                    currentSource.CopyTo(_buffer.AsSpan((int)sizeSkipBlock, (int)sizeToWrite));

                    // Write the entire modified block to the new log entry.
                    _logStorage.Write(GetDataOffset(offsetLog), _buffer);
                    if (res.IsFailure()) return res.Miss();
                }

                _blockCount++;
            }

            offsetBuffer += sizeToWrite;
            offsetOriginal += _blockSize;
            sizeSkipBlock = 0;
        }

        return Result.Success;
    }

    public override Result Flush()
    {
        Assert.SdkRequiresNotNull(_buffer);

        Result res = _baseStorage.Flush();
        if (res.IsFailure()) return res.Miss();

        res = _logStorage.Flush();
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public override Result GetSize(out long size)
    {
        Assert.SdkRequiresNotNull(_buffer);

        return _baseStorage.GetSize(out size);
    }

    public override Result SetSize(long size)
    {
        return ResultFs.UnsupportedSetSizeForUnionStorage.Log();
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        for (long currentOffset = Alignment.AlignDownPow2(offset, _blockSize);
             currentOffset < offset + size;
             currentOffset += _blockSize)
        {
            Result res = FindLog(out bool found, out long offsetLog, currentOffset);
            if (res.IsFailure()) return res.Miss();

            if (found)
            {
                _logStorage.OperateRange(outBuffer, operationId, offsetLog, _blockSize, inBuffer);
                if (res.IsFailure()) return res.Miss();
            }
        }

        return _baseStorage.OperateRange(outBuffer, operationId, offset, size, inBuffer);
    }

    private Result FindLog(out bool logFound, out long outLogOffset, long offsetOriginal)
    {
        Assert.SdkRequiresNotNull(_buffer);

        outLogOffset = GetLogTailOffset(_blockSize, _blockCount);
        logFound = false;

        long logTailOffset = GetLogTailOffset(_blockSize, _blockCount);
        long logSize = GetLogSize(_blockSize);

        // Go through each log entry to see if any are at the requested offset
        for (long logOffset = LogHeaderSize; logOffset < logTailOffset; logOffset += logSize)
        {
            long offset = 0;
            Result res = _logStorage.Read(logOffset, SpanHelpers.AsByteSpan(ref offset));
            if (res.IsFailure()) return res.Miss();

            if (offset == Sentinel)
                return ResultFs.LogNotFound.Log();

            if (offset == offsetOriginal)
            {
                outLogOffset = logOffset;
                logFound = true;
                break;
            }
        }

        return Result.Success;
    }
}