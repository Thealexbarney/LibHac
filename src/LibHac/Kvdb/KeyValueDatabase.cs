using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace LibHac.Kvdb
{
    // Todo: Save and load from file
    public class KeyValueDatabase<TKey, TValue>
        where TKey : IComparable<TKey>, IEquatable<TKey>, IExportable, new()
        where TValue : IExportable, new()
    {
        private Dictionary<TKey, TValue> KvDict { get; } = new Dictionary<TKey, TValue>();

        public int Count => KvDict.Count;

        public Result Get(TKey key, out TValue value)
        {
            if (!KvDict.TryGetValue(key, out value))
            {
                return ResultKvdb.KeyNotFound;
            }

            return Result.Success;
        }

        public Result Set(TKey key, TValue value)
        {
            key.Freeze();

            KvDict[key] = value;

            return Result.Success;
        }

        public Result ReadDatabaseFromBuffer(ReadOnlySpan<byte> data)
        {
            var reader = new ImkvdbReader(data);

            Result rc = reader.ReadHeader(out int entryCount);
            if (rc.IsFailure()) return rc;

            for (int i = 0; i < entryCount; i++)
            {
                rc = reader.ReadEntry(out ReadOnlySpan<byte> keyBytes, out ReadOnlySpan<byte> valueBytes);
                if (rc.IsFailure()) return rc;

                var key = new TKey();
                var value = new TValue();

                key.FromBytes(keyBytes);
                value.FromBytes(valueBytes);

                key.Freeze();

                KvDict.Add(key, value);
            }

            return Result.Success;
        }

        public Result WriteDatabaseToBuffer(Span<byte> output)
        {
            var writer = new ImkvdbWriter(output);

            writer.WriteHeader(KvDict.Count);

            foreach (KeyValuePair<TKey, TValue> entry in KvDict.OrderBy(x => x.Key))
            {
                writer.WriteEntry(entry.Key, entry.Value);
            }

            return Result.Success;
        }

        public int GetExportedSize()
        {
            int size = Unsafe.SizeOf<ImkvdbHeader>();

            foreach (KeyValuePair<TKey, TValue> entry in KvDict)
            {
                size += Unsafe.SizeOf<ImkvdbEntryHeader>();
                size += entry.Key.ExportSize;
                size += entry.Value.ExportSize;
            }

            return size;
        }
    }
}
