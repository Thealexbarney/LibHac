using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace LibHac.IO.RomFs
{
    internal class RomFsDictionary<T> where T : unmanaged
    {
        private int _count;
        private int _length;
        private int _capacity;

        private int[] Buckets { get; set; }
        private byte[] Entries { get; set; }

        // Hack around not being able to get the size of generic structures
        private readonly int _sizeOfEntry = 12 + Marshal.SizeOf<T>();

        public RomFsDictionary(IStorage bucketStorage, IStorage entryStorage)
        {
            Buckets = bucketStorage.ToArray<int>();
            Entries = entryStorage.ToArray();

            _length = Entries.Length;
            _capacity = Entries.Length;

            _count = CountEntries();
        }

        public RomFsDictionary(int capacity)
        {
            int size = HashHelpers.GetHashTableEntryCount(capacity);

            Buckets = new int[size];
            Buckets.AsSpan().Fill(-1);
            Entries = new byte[EstimateEntryTableSize(size)];
            _capacity = Entries.Length;
        }

        public ReadOnlySpan<int> GetBucketData() => Buckets.AsSpan();
        public ReadOnlySpan<byte> GetEntryData() => Entries.AsSpan(0, _length);

        public bool TryGetValue(ref RomEntryKey key, out RomKeyValuePair<T> value)
        {
            int i = FindEntry(ref key);

            if (i >= 0)
            {
                ref RomFsEntry entry = ref GetEntryReference(i);

                value = new RomKeyValuePair<T> { Key = key, Value = entry.Value, Offset = i };
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGetValue(int offset, out RomKeyValuePair<T> value)
        {
            if (offset < 0 || offset + _sizeOfEntry >= Entries.Length)
            {
                value = default;
                return false;
            }

            value = new RomKeyValuePair<T>();

            ref RomFsEntry entry = ref GetEntryReference(offset, out Span<byte> name);

            value.Key.Name = name;
            value.Value = entry.Value;
            value.Key.Parent = entry.Parent;
            return true;
        }

        public bool TrySetValue(ref RomEntryKey key, ref T value)
        {
            int i = FindEntry(ref key);
            if (i < 0) return false;

            ref RomFsEntry entry = ref GetEntryReference(i);
            entry.Value = value;

            return true;
        }

        public ref T GetValue(int offset, out Span<byte> name)
        {
            ref RomFsEntry entry = ref GetEntryReference(offset, out name);

            return ref entry.Value;
        }

        public bool ContainsKey(ref RomEntryKey key) => FindEntry(ref key) >= 0;

        public int Insert(ref RomEntryKey key, ref T value)
        {
            if (ContainsKey(ref key))
            {
                throw new ArgumentException("Key already exists in dictionary.");
            }

            uint hashCode = key.GetRomHashCode();

            int bucket = (int)(hashCode % Buckets.Length);
            int newOffset = FindOffsetForInsert(key.Name.Length);

            ref RomFsEntry entry = ref GetEntryReference(newOffset, out Span<byte> name, key.Name.Length);

            entry.Next = Buckets[bucket];
            entry.Parent = key.Parent;
            entry.KeyLength = key.Name.Length;
            entry.Value = value;

            key.Name.CopyTo(name);

            Buckets[bucket] = newOffset;
            _count++;
            return newOffset;
        }

        public ref T Insert(ref RomEntryKey key, out int offset, out Span<byte> name)
        {
            uint hashCode = key.GetRomHashCode();

            int bucket = (int)(hashCode % Buckets.Length);
            int newOffset = FindOffsetForInsert(key.Name.Length);

            ref RomFsEntry entry = ref GetEntryReference(newOffset, out name, key.Name.Length);

            entry.KeyLength = key.Name.Length;

            entry.Next = Buckets[bucket];
            entry.Parent = key.Parent;
            key.Name.CopyTo(name);

            Buckets[bucket] = newOffset;
            _count++;

            offset = newOffset;
            return ref entry.Value;
        }

        private int FindOffsetForInsert(int nameLength)
        {
            int bytesNeeded = Util.AlignUp(_sizeOfEntry + nameLength, 4);

            if (_length + bytesNeeded > _capacity)
            {
                EnsureCapacityBytes(_length + bytesNeeded);
            }

            int offset = _length;
            _length += bytesNeeded;

            return offset;
        }

        private int FindEntry(ref RomEntryKey key)
        {
            uint hashCode = key.GetRomHashCode();
            int index = (int)(hashCode % Buckets.Length);
            int i = Buckets[index];

            while (i != -1)
            {
                ref RomFsEntry entry = ref GetEntryReference(i, out Span<byte> name);

                if (key.Parent == entry.Parent && key.Name.SequenceEqual(name))
                {
                    break;
                }

                i = entry.Next;
            }

            return i;
        }

        public int GetOffsetFromKey(ref RomEntryKey key)
        {
            uint hashCode = key.GetRomHashCode();
            int index = (int)(hashCode % Buckets.Length);
            int i = Buckets[index];

            while (i != -1)
            {
                ref RomFsEntry entry = ref GetEntryReference(i, out Span<byte> name);

                if (key.Parent == entry.Parent && key.Name.SequenceEqual(name))
                {
                    break;
                }

                i = entry.Next;
            }

            return i;
        }

        private void EnsureCapacityBytes(int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (value <= _capacity) return;

            long newCapacity = Math.Max(value, 256);
            newCapacity = Math.Max(newCapacity, _capacity * 2);

            SetCapacity((int)Math.Min(newCapacity, int.MaxValue));
        }

        private void SetCapacity(int value)
        {
            if (value < _length)
                throw new ArgumentOutOfRangeException(nameof(value), "Capacity is smaller than the current length.");

            if (value != _capacity)
            {
                var newBuffer = new byte[value];
                Buffer.BlockCopy(Entries, 0, newBuffer, 0, _length);

                Entries = newBuffer;
                _capacity = value;
            }
        }

        public int CountEntries()
        {
            int count = 0;
            int nextStructOffset = (sizeof(int) + Marshal.SizeOf<T>()) / 4;
            Span<int> data = MemoryMarshal.Cast<byte, int>(Entries.AsSpan());

            for (int i = 0; i < Buckets.Length; i++)
            {
                int next = Buckets[i];

                while (next != -1)
                {
                    next = data[next / 4 + nextStructOffset];
                    count++;
                }
            }

            return count;
        }

        public void Resize(int newSize)
        {
            var newBuckets = new int[newSize];
            newBuckets.AsSpan().Fill(-1);

            List<int> offsets = GetEntryOffsets();

            for (int i = 0; i < offsets.Count; i++)
            {
                ref RomFsEntry entry = ref GetEntryReference(offsets[i], out Span<byte> name);

                uint hashCode = RomEntryKey.GetRomHashCode(entry.Parent, name);
                int bucket = (int)(hashCode % newSize);

                entry.Next = newBuckets[bucket];
                newBuckets[bucket] = offsets[i];
            }

            Buckets = newBuckets;
        }

        public ref T GetValueReference(int offset)
        {
            ref RomFsEntry entry = ref MemoryMarshal.Cast<byte, RomFsEntry>(Entries.AsSpan(offset))[0];
            return ref entry.Value;
        }

        public ref T GetValueReference(int offset, out Span<byte> name)
        {
            ref RomFsEntry entry = ref MemoryMarshal.Cast<byte, RomFsEntry>(Entries.AsSpan(offset))[0];

            name = Entries.AsSpan(offset + _sizeOfEntry, entry.KeyLength);
            return ref entry.Value;
        }

        private ref RomFsEntry GetEntryReference(int offset)
        {
            return ref MemoryMarshal.Cast<byte, RomFsEntry>(Entries.AsSpan(offset))[0];
        }

        private ref RomFsEntry GetEntryReference(int offset, out Span<byte> name)
        {
            ref RomFsEntry entry = ref MemoryMarshal.Cast<byte, RomFsEntry>(Entries.AsSpan(offset))[0];

            name = Entries.AsSpan(offset + _sizeOfEntry, entry.KeyLength);
            return ref entry;
        }

        private ref RomFsEntry GetEntryReference(int offset, out Span<byte> name, int nameLength)
        {
            ref RomFsEntry entry = ref MemoryMarshal.Cast<byte, RomFsEntry>(Entries.AsSpan(offset))[0];

            name = Entries.AsSpan(offset + _sizeOfEntry, nameLength);
            return ref entry;
        }

        private List<int> GetEntryOffsets()
        {
            var offsets = new List<int>(_count);

            int nextStructOffset = (sizeof(int) + Marshal.SizeOf<T>()) / 4;
            Span<int> data = MemoryMarshal.Cast<byte, int>(Entries.AsSpan());

            for (int i = 0; i < Buckets.Length; i++)
            {
                int next = Buckets[i];

                while (next != -1)
                {
                    offsets.Add(next);
                    next = data[next / 4 + nextStructOffset];
                }
            }

            offsets.Sort();
            return offsets;
        }

        private int EstimateEntryTableSize(int count) => (_sizeOfEntry + 0x10) * count; // Estimate 0x10 bytes per name

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct RomFsEntry
        {
            public int Parent;
            public T Value;
            public int Next;
            public int KeyLength;
        }
    }
}
