using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace LibHac.IO
{
    internal class RomFsDictionary<T> where T : unmanaged
    {
        private int HashBucketCount { get; }
        private IStorage BucketStorage { get; }
        private IStorage EntryStorage { get; }

        // Hack around not being able to get the size of generic structures
        private readonly int _sizeOfEntry = 12 + Marshal.SizeOf<T>();

        public RomFsDictionary(IStorage bucketStorage, IStorage entryStorage)
        {
            BucketStorage = bucketStorage;
            EntryStorage = entryStorage;
            HashBucketCount = (int)(bucketStorage.Length / 4);
        }

        public bool TryGetValue(ref RomEntryKey key, out T value, out int offset)
        {
            int i = FindEntry(ref key);
            offset = i;

            if (i >= 0)
            {
                GetEntryInternal(i, out RomFsEntry<T> entry);
                value = entry.Value;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGetValue(int offset, out T value, out string entryName)
        {
            if (offset < 0 || offset + _sizeOfEntry >= EntryStorage.Length)
            {
                value = default;
                entryName = default;
                return false;
            }

            GetEntryInternal(offset, out RomFsEntry<T> entry, out entryName);
            value = entry.Value;
            return true;
        }

        public bool TryGetValue(int offset, out T value)
        {
            if (offset < 0 || offset + _sizeOfEntry >= EntryStorage.Length)
            {
                value = default;
                return false;
            }

            GetEntryInternal(offset, out RomFsEntry<T> entry);
            value = entry.Value;
            return true;
        }

        private int FindEntry(ref RomEntryKey key)
        {
            uint hashCode = key.GetRomHashCode();
            int i = GetBucket((int)(hashCode % HashBucketCount));

            while (i != -1)
            {
                GetEntryInternal(i, out RomFsEntry<T> entry);

                if (IsEqual(ref key, ref entry, i))
                {
                    break;
                }

                i = entry.Next;
            }

            return i;
        }

        private bool IsEqual(ref RomEntryKey key, ref RomFsEntry<T> entry, int entryOffset)
        {
            if (key.Parent != entry.Parent) return false;
            if (key.Name.Length != entry.KeyLength) return false;

            GetEntryInternal(entryOffset, out RomFsEntry<T> _, out string name);

            return key.Name.Equals(name.AsSpan(), StringComparison.Ordinal);
        }

        private void GetEntryInternal(int offset, out RomFsEntry<T> outEntry)
        {
            Span<byte> b = stackalloc byte[_sizeOfEntry];
            EntryStorage.Read(b, offset);
            outEntry = MemoryMarshal.Read<RomFsEntry<T>>(b);
        }

        private void GetEntryInternal(int offset, out RomFsEntry<T> outEntry, out string entryName)
        {
            GetEntryInternal(offset, out outEntry);

            if (outEntry.KeyLength > 0x300)
            {
                throw new InvalidDataException("Rom entry name is too long.");
            }

            var buf = new byte[outEntry.KeyLength];
            EntryStorage.Read(buf, offset + _sizeOfEntry);
            entryName = Encoding.ASCII.GetString(buf);
        }

        private int GetBucket(int index)
        {
            Debug.Assert(index < HashBucketCount);

            Span<byte> buf = stackalloc byte[4];
            BucketStorage.Read(buf, index * 4);
            return MemoryMarshal.Read<int>(buf);
        }
    }
}
