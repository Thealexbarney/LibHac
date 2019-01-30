using System;
using System.IO;
using System.Runtime.InteropServices;

namespace LibHac.IO.RomFs
{
    internal class RomFsDictionary<T> where T : unmanaged
    {
        private int[] BucketTable { get; }
        private byte[] EntryTable { get; }

        // Hack around not being able to get the size of generic structures
        private readonly int _sizeOfEntry = 12 + Marshal.SizeOf<T>();

        public RomFsDictionary(IStorage bucketStorage, IStorage entryStorage)
        {
            BucketTable = bucketStorage.ToArray<int>();
            EntryTable = entryStorage.ToArray();
        }

        public bool TryGetValue(ref RomEntryKey key, out RomKeyValuePair<T> value)
        {
            int i = FindEntry(ref key);

            if (i >= 0)
            {
                GetEntryInternal(i, out RomFsEntry<T> entry);

                value = new RomKeyValuePair<T> { Key = key, Value = entry.Value, Offset = i };
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGetValue(int offset, out RomKeyValuePair<T> value)
        {
            if (offset < 0 || offset + _sizeOfEntry >= EntryTable.Length)
            {
                value = default;
                return false;
            }

            value = new RomKeyValuePair<T>();

            GetEntryInternal(offset, out RomFsEntry<T> entry, out value.Key.Name);
            value.Value = entry.Value;
            return true;
        }

        private int FindEntry(ref RomEntryKey key)
        {
            uint hashCode = key.GetRomHashCode();
            int index = (int)(hashCode % BucketTable.Length);
            int i = BucketTable[index];

            while (i != -1)
            {
                GetEntryInternal(i, out RomFsEntry<T> entry, out ReadOnlySpan<byte> name);

                if (key.Parent == entry.Parent && key.Name.SequenceEqual(name))
                {
                    break;
                }

                i = entry.Next;
            }

            return i;
        }

        private void GetEntryInternal(int offset, out RomFsEntry<T> outEntry)
        {
            outEntry = MemoryMarshal.Read<RomFsEntry<T>>(EntryTable.AsSpan(offset));
        }

        private void GetEntryInternal(int offset, out RomFsEntry<T> outEntry, out ReadOnlySpan<byte> entryName)
        {
            GetEntryInternal(offset, out outEntry);

            if (outEntry.KeyLength > 0x300)
            {
                throw new InvalidDataException("Rom entry name is too long.");
            }

            entryName = EntryTable.AsSpan(offset + _sizeOfEntry, outEntry.KeyLength);
        }
    }
}
