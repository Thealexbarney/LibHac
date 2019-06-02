using System;
using System.Collections.Generic;

using static LibHac.Results;
using static LibHac.Kvdb.ResultsKvdb;

namespace LibHac.Kvdb
{
    public class KeyValueDatabase<TKey, TValue>
        where TKey : IComparable<TKey>, IComparable, IEquatable<TKey>, IExportable, new()
        where TValue : IExportable, new()
    {
        private Dictionary<TKey, TValue> KvDict { get; } = new Dictionary<TKey, TValue>();

        public Result ReadDatabase(ReadOnlySpan<byte> data)
        {
            var reader = new ImkvdbReader(data);

            Result headerResult = reader.ReadHeader(out int entryCount);
            if (headerResult.IsFailure()) return headerResult;

            for (int i = 0; i < entryCount; i++)
            {
                Result entryResult = reader.ReadEntry(out ReadOnlySpan<byte> keyBytes, out ReadOnlySpan<byte> valueBytes);
                if (entryResult.IsFailure()) return entryResult;

                var key = new TKey();
                var value = new TValue();

                key.FromBytes(keyBytes);
                value.FromBytes(valueBytes);

                KvDict.Add(key, value);
            }

            return ResultSuccess;
        }
    }
}
