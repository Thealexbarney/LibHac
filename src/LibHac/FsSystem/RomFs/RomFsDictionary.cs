using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Fs;
using LibHac.Util;

namespace LibHac.FsSystem.RomFs
{
    internal class RomFsDictionary<T> where T : unmanaged
    {
        private int _count;
        private int _length;
        private int _capacity;

        private int[] Buckets { get; set; }
        private byte[] Entries { get; set; }

        private readonly int _sizeOfEntry = Unsafe.SizeOf<RomFsEntry>();

        public RomFsDictionary(IStorage bucketStorage, IStorage entryStorage)
        {
            Buckets = bucketStorage.ToArray<int>();
            Entries = entryStorage.ToArray();

            _length = Entries.Length;
            _capacity = Entries.Length;

            _count = CountEntries();
        }

        public RomFsDictionary() : this(0) { }

        public RomFsDictionary(int capacity)
        {
            int size = HashHelpers.GetRomFsPrime(capacity);

            Buckets = new int[size];
            Buckets.AsSpan().Fill(-1);
            Entries = new byte[EstimateEntryTableSize(size)];
            _capacity = Entries.Length;
        }

        public ReadOnlySpan<int> GetBucketData() => Buckets.AsSpan();
        public ReadOnlySpan<byte> GetEntryData() => Entries.AsSpan(0, _length);

        public bool TryGetValue(ref RomEntryKey key, out RomKeyValuePair<T> value)
        {
            return TryGetValue(GetOffsetFromKey(ref key), out value);
        }

        public bool TryGetValue(int offset, out RomKeyValuePair<T> value)
        {
            if (offset < 0 || offset + _sizeOfEntry > Entries.Length)
            {
                value = default;
                return false;
            }

            value = new RomKeyValuePair<T>();

            ref RomFsEntry entry = ref GetEntryReference(offset, out Span<byte> name);

            value.Key.Name = name;
            value.Value = entry.Value;
            value.Key.Parent = entry.Parent;
            value.Offset = offset;
            return true;
        }

        public ref T GetValueReference(int offset)
        {
            return ref Unsafe.As<byte, RomFsEntry>(ref Entries[offset]).Value;
        }

        public ref T GetValueReference(int offset, out Span<byte> name)
        {
            ref RomFsEntry entry = ref Unsafe.As<byte, RomFsEntry>(ref Entries[offset]);

            name = Entries.AsSpan(offset + _sizeOfEntry, entry.KeyLength);
            return ref entry.Value;
        }

        public bool ContainsKey(ref RomEntryKey key) => GetOffsetFromKey(ref key) >= 0;

        public int Add(ref RomEntryKey key, ref T value)
        {
            ref T entry = ref AddOrGet(ref key, out int offset, out bool alreadyExists, out _);

            if (alreadyExists)
            {
                throw new ArgumentException("Key already exists in dictionary.");
            }

            entry = value;

            return offset;
        }

        public ref T AddOrGet(ref RomEntryKey key, out int offset, out bool alreadyExists, out Span<byte> name)
        {
            int oldOffset = GetOffsetFromKey(ref key);

            if (oldOffset >= 0)
            {
                alreadyExists = true;
                offset = oldOffset;

                ref RomFsEntry entry = ref GetEntryReference(oldOffset, out name);
                return ref entry.Value;
            }
            else
            {
                int newOffset = CreateNewEntry(key.Name.Length);
                alreadyExists = false;
                offset = newOffset;

                ref RomFsEntry entry = ref GetEntryReference(newOffset, out name, key.Name.Length);

                entry.Parent = key.Parent;
                entry.KeyLength = key.Name.Length;
                key.Name.CopyTo(name);

                int bucket = (int)(key.GetRomHashCode() % Buckets.Length);

                entry.Next = Buckets[bucket];
                Buckets[bucket] = newOffset;
                _count++;

                if (Buckets.Length < _count)
                {
                    Resize();
                    entry = ref GetEntryReference(newOffset, out name, key.Name.Length);
                }

                return ref entry.Value;
            }
        }

        private int CreateNewEntry(int nameLength)
        {
            int bytesNeeded = Alignment.AlignUp(_sizeOfEntry + nameLength, 4);

            if (_length + bytesNeeded > _capacity)
            {
                EnsureCapacityBytes(_length + bytesNeeded);
            }

            int offset = _length;
            _length += bytesNeeded;

            return offset;
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
                byte[] newBuffer = new byte[value];
                System.Buffer.BlockCopy(Entries, 0, newBuffer, 0, _length);

                Entries = newBuffer;
                _capacity = value;
            }
        }

        private int CountEntries()
        {
            int count = 0;
            int nextStructOffset = (sizeof(int) + Unsafe.SizeOf<T>()) / 4;
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

        private void Resize() => Resize(HashHelpers.ExpandPrime(_count));

        private void Resize(int newSize)
        {
            int[] newBuckets = new int[newSize];
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

        public void TrimExcess()
        {
            Resize(HashHelpers.GetRomFsPrime(_count));
            SetCapacity(_length);
        }

        private ref RomFsEntry GetEntryReference(int offset, out Span<byte> name)
        {
            ref RomFsEntry entry = ref Unsafe.As<byte, RomFsEntry>(ref Entries[offset]);

            name = Entries.AsSpan(offset + _sizeOfEntry, entry.KeyLength);
            return ref entry;
        }

        private ref RomFsEntry GetEntryReference(int offset, out Span<byte> name, int nameLength)
        {
            ref RomFsEntry entry = ref Unsafe.As<byte, RomFsEntry>(ref Entries[offset]);

            name = Entries.AsSpan(offset + _sizeOfEntry, nameLength);
            return ref entry;
        }

        private List<int> GetEntryOffsets()
        {
            var offsets = new List<int>(_count);

            int nextStructOffset = (sizeof(int) + Unsafe.SizeOf<T>()) / 4;
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
