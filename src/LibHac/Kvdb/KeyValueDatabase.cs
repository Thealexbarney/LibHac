using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.Kvdb
{
    public class KeyValueDatabase<TKey> where TKey : unmanaged, IComparable<TKey>, IEquatable<TKey>
    {
        public Dictionary<TKey, byte[]> KvDict { get; } = new Dictionary<TKey, byte[]>();

        private FileSystemClient FsClient { get; }
        private string FileName { get; }

        public KeyValueDatabase() { }

        public KeyValueDatabase(FileSystemClient fsClient, string fileName)
        {
            FsClient = fsClient;
            FileName = fileName;
        }

        public Result Get(ref TKey key, Span<byte> valueBuffer)
        {
            Result rc = GetValue(ref key, out byte[] value);
            if (rc.IsFailure()) return rc;

            int size = Math.Min(valueBuffer.Length, value.Length);

            value.AsSpan(0, size).CopyTo(valueBuffer);
            return Result.Success;
        }

        public Result GetValue(ref TKey key, out byte[] value)
        {
            if (!KvDict.TryGetValue(key, out value))
            {
                return ResultKvdb.KeyNotFound.Log();
            }

            return Result.Success;
        }

        public Result Set(ref TKey key, ReadOnlySpan<byte> value)
        {
            KvDict[key] = value.ToArray();

            return Result.Success;
        }

        public Dictionary<TKey, byte[]>.Enumerator GetEnumerator()
        {
            return KvDict.GetEnumerator();
        }

        public Result ReadDatabaseFromBuffer(ReadOnlySpan<byte> data)
        {
            KvDict.Clear();

            var reader = new ImkvdbReader(data);

            Result rc = reader.ReadHeader(out int entryCount);
            if (rc.IsFailure()) return rc;

            for (int i = 0; i < entryCount; i++)
            {
                rc = reader.ReadEntry(out ReadOnlySpan<byte> keyBytes, out ReadOnlySpan<byte> valueBytes);
                if (rc.IsFailure()) return rc;

                Debug.Assert(keyBytes.Length == Unsafe.SizeOf<TKey>());

                var key = new TKey();
                keyBytes.CopyTo(SpanHelpers.AsByteSpan(ref key));

                byte[] value = valueBytes.ToArray();

                KvDict.Add(key, value);
            }

            return Result.Success;
        }

        public Result WriteDatabaseToBuffer(Span<byte> output)
        {
            var writer = new ImkvdbWriter(output);

            writer.WriteHeader(KvDict.Count);

            foreach (KeyValuePair<TKey, byte[]> entry in KvDict.OrderBy(x => x.Key))
            {
                TKey key = entry.Key;
                writer.WriteEntry(SpanHelpers.AsByteSpan(ref key), entry.Value);
            }

            return Result.Success;
        }

        public Result ReadDatabaseFromFile()
        {
            if (FsClient == null || FileName == null)
                return ResultFs.PreconditionViolation.Log();

            Result rc = ReadFile(out byte[] data);

            if (rc.IsFailure())
            {
                return rc == ResultFs.PathNotFound ? Result.Success : rc;
            }

            return ReadDatabaseFromBuffer(data);
        }

        public Result WriteDatabaseToFile()
        {
            if (FsClient == null || FileName == null)
                return ResultFs.PreconditionViolation.Log();

            var buffer = new byte[GetExportedSize()];

            Result rc = WriteDatabaseToBuffer(buffer);
            if (rc.IsFailure()) return rc;

            return WriteFile(buffer);
        }

        public int GetExportedSize()
        {
            int size = Unsafe.SizeOf<ImkvdbHeader>();

            foreach (byte[] value in KvDict.Values)
            {
                size += Unsafe.SizeOf<ImkvdbEntryHeader>();
                size += Unsafe.SizeOf<TKey>();
                size += value.Length;
            }

            return size;
        }

        private Result ReadFile(out byte[] data)
        {
            Debug.Assert(FsClient != null);
            Debug.Assert(!string.IsNullOrWhiteSpace(FileName));

            data = default;

            Result rc = FsClient.OpenFile(out FileHandle handle, FileName, OpenMode.Read);
            if (rc.IsFailure()) return rc;

            rc = FsClient.GetFileSize(out long fileSize, handle);

            if (rc.IsSuccess())
            {
                data = new byte[fileSize];

                rc = FsClient.ReadFile(handle, 0, data);
            }

            FsClient.CloseFile(handle);
            return rc;
        }

        private Result WriteFile(ReadOnlySpan<byte> data)
        {
            Debug.Assert(FsClient != null);
            Debug.Assert(!string.IsNullOrWhiteSpace(FileName));

            FsClient.DeleteFile(FileName);

            Result rc = FsClient.CreateFile(FileName, data.Length);
            if (rc.IsFailure()) return rc;

            rc = FsClient.OpenFile(out FileHandle handle, FileName, OpenMode.Write);
            if (rc.IsFailure()) return rc;

            rc = FsClient.WriteFile(handle, 0, data, WriteOption.Flush);
            FsClient.CloseFile(handle);

            return rc;
        }
    }
}
