using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.IO.Save
{
    internal class SaveFsList<T> where T : unmanaged
    {
        private const int FreeListHeadIndex = 0;
        private const int UsedListHeadIndex = 1;
        public int MaxNameLength { get; } = 0x40;

        private IStorage Storage { get; }

        private readonly int _sizeOfEntry = Unsafe.SizeOf<SaveFsEntry>();

        public SaveFsList(IStorage tableStorage)
        {
            Storage = tableStorage;
        }

        public int GetOffsetFromKey(ref SaveEntryKey key)
        {
            Span<byte> entryBytes = stackalloc byte[_sizeOfEntry];
            Span<byte> name = entryBytes.Slice(4, MaxNameLength);
            ref SaveFsEntry entry = ref GetEntryFromBytes(entryBytes);

            int capacity = GetListCapacity();
            int entryId = -1;

            ReadEntry(UsedListHeadIndex, entryBytes);

            while (entry.Next > 0)
            {
                if (entry.Next > capacity) throw new IndexOutOfRangeException("Save entry index out of range");

                entryId = entry.Next;
                ReadEntry(entry.Next, out entry);

                if (entry.Parent == key.Parent && Util.StringSpansEqual(name, key.Name))
                {
                    break;
                }
            }

            return entryId;
        }

        public bool TryGetValue(ref SaveEntryKey key, out T value)
        {
            int index = GetOffsetFromKey(ref key);

            if (index < 0)
            {
                value = default;
                return false;
            }

            return TryGetValue(index, out value);
        }

        public bool TryGetValue(int index, out T value)
        {
            if (index < 0 || index >= GetListCapacity())
            {
                value = default;
                return false;
            }

            GetValue(index, out value);

            return true;
        }

        public void GetValue(int index, out T value)
        {
            ReadEntry(index, out SaveFsEntry entry);
            value = entry.Value;
        }

        /// <summary>
        /// Gets the value and name associated with the specific index.
        /// </summary>
        /// <param name="index">The index of the value to get.</param>
        /// <param name="value">Contains the corresponding value if the method returns <see langword="true"/>.</param>
        /// <param name="name">The name of the given index will be written to this span if the method returns <see langword="true"/>.
        /// This span must be at least <see cref="MaxNameLength"/> bytes long.</param>
        /// <returns><see langword="true"/> if the <see cref="SaveFsList{T}"/> contains an element with
        /// the specified key; otherwise, <see langword="false"/>.</returns>
        public bool TryGetValue(int index, out T value, ref Span<byte> name)
        {
            Debug.Assert(name.Length >= MaxNameLength);

            if (index < 0 || index >= GetListCapacity())
            {
                value = default;
                return false;
            }

            GetValue(index, out value, ref name);

            return true;
        }

        /// <summary>
        /// Gets the value and name associated with the specific index.
        /// </summary>
        /// <param name="index">The index of the value to get.</param>
        /// <param name="value">Contains the corresponding value when the method returns.</param>
        /// <param name="name">The name of the given index will be written to this span when the method returns.
        /// This span must be at least <see cref="MaxNameLength"/> bytes long.</param>
        public void GetValue(int index, out T value, ref Span<byte> name)
        {
            Debug.Assert(name.Length >= MaxNameLength);

            Span<byte> entryBytes = stackalloc byte[_sizeOfEntry];
            Span<byte> nameSpan = entryBytes.Slice(4, MaxNameLength);
            ref SaveFsEntry entry = ref GetEntryFromBytes(entryBytes);

            ReadEntry(index, out entry);

            nameSpan.CopyTo(name);
            value = entry.Value;
        }

        private int GetListCapacity()
        {
            Span<byte> buf = stackalloc byte[sizeof(int)];
            Storage.Read(buf, 4);

            return MemoryMarshal.Read<int>(buf);
        }

        private int GetListLength()
        {
            Span<byte> buf = stackalloc byte[sizeof(int)];
            Storage.Read(buf, 0);

            return MemoryMarshal.Read<int>(buf);
        }

        private void ReadEntry(int index, out SaveFsEntry entry)
        {
            Span<byte> bytes = stackalloc byte[_sizeOfEntry];
            ReadEntry(index, bytes);

            entry = GetEntryFromBytes(bytes);
        }

        private void ReadEntry(int index, Span<byte> entry)
        {
            Debug.Assert(entry.Length == _sizeOfEntry);

            int offset = index * _sizeOfEntry;
            Storage.Read(entry, offset);
        }

        private ref SaveFsEntry GetEntryFromBytes(Span<byte> entry)
        {
            return ref MemoryMarshal.Cast<byte, SaveFsEntry>(entry)[0];
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SaveFsEntry
        {
            public int Parent;
            private NameDummy Name;
            public T Value;
            public int Next;
        }

        [StructLayout(LayoutKind.Sequential, Size = 0x40)]
        private struct NameDummy { }
    }
}
