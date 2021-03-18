using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Util;

namespace LibHac.FsSystem.Save
{
    // todo: Change constraint to "unmanaged" after updating to
    // a newer SDK https://github.com/dotnet/csharplang/issues/1937
    internal class SaveFsList<T> where T : struct
    {
        private const int FreeListHeadIndex = 0;
        private const int UsedListHeadIndex = 1;
        private const int CapacityIncrement = 0x4000;

        public int MaxNameLength { get; } = 0x40;

        private IStorage Storage { get; }

        private readonly int _sizeOfEntry = Unsafe.SizeOf<SaveFsEntry>();

        public SaveFsList(IStorage tableStorage)
        {
            Storage = tableStorage;
        }

        public (int Index, int PreviousIndex) GetIndexFromKey(ref SaveEntryKey key)
        {
            Span<byte> entryBytes = stackalloc byte[_sizeOfEntry];
            Span<byte> name = entryBytes.Slice(4, MaxNameLength);
            ref SaveFsEntry entry = ref GetEntryFromBytes(entryBytes);

            int capacity = GetListCapacity();

            ReadEntry(UsedListHeadIndex, entryBytes);
            int prevIndex = UsedListHeadIndex;
            int index = entry.Next;

            while (index > 0)
            {
                if (index > capacity) throw new IndexOutOfRangeException("Save entry index out of range");

                ReadEntry(index, out entry);

                if (entry.Parent == key.Parent && StringUtils.Compare(name, key.Name) == 0)
                {
                    return (index, prevIndex);
                }

                prevIndex = index;
                index = entry.Next;
            }

            return (-1, -1);
        }

        public int Add(ref SaveEntryKey key, ref T value)
        {
            int index = GetIndexFromKey(ref key).Index;

            if (index != -1)
            {
                SetValue(index, ref value);
                return index;
            }

            index = AllocateEntry();

            ReadEntry(index, out SaveFsEntry entry);
            entry.Value = value;
            WriteEntry(index, ref entry, ref key);

            return index;
        }

        private int AllocateEntry()
        {
            ReadEntry(FreeListHeadIndex, out SaveFsEntry freeListHead);
            ReadEntry(UsedListHeadIndex, out SaveFsEntry usedListHead);

            if (freeListHead.Next != 0)
            {
                ReadEntry(freeListHead.Next, out SaveFsEntry firstFreeEntry);

                int allocatedIndex = freeListHead.Next;

                freeListHead.Next = firstFreeEntry.Next;
                firstFreeEntry.Next = usedListHead.Next;
                usedListHead.Next = allocatedIndex;

                WriteEntry(FreeListHeadIndex, ref freeListHead);
                WriteEntry(UsedListHeadIndex, ref usedListHead);
                WriteEntry(allocatedIndex, ref firstFreeEntry);

                return allocatedIndex;
            }

            int length = GetListLength();
            int capacity = GetListCapacity();

            if (capacity == 0 || length >= capacity)
            {
                Storage.GetSize(out long currentSize).ThrowIfFailure();
                Storage.SetSize(currentSize + CapacityIncrement);

                Storage.GetSize(out long newSize).ThrowIfFailure();
                SetListCapacity((int)(newSize / _sizeOfEntry));
            }

            SetListLength(length + 1);

            ReadEntry(length, out SaveFsEntry newEntry);

            newEntry.Next = usedListHead.Next;
            usedListHead.Next = length;

            WriteEntry(UsedListHeadIndex, ref usedListHead);
            WriteEntry(length, ref newEntry);

            return length;
        }

        private void Free(int entryIndex)
        {
            ReadEntry(FreeListHeadIndex, out SaveFsEntry freeEntry);
            ReadEntry(entryIndex, out SaveFsEntry entry);

            entry.Next = freeEntry.Next;
            freeEntry.Next = entryIndex;

            WriteEntry(FreeListHeadIndex, ref freeEntry);
            WriteEntry(entryIndex, ref entry);
        }

        public bool TryGetValue(ref SaveEntryKey key, out T value)
        {
            UnsafeHelpers.SkipParamInit(out value);

            int index = GetIndexFromKey(ref key).Index;

            if (index < 0)
                return false;

            return TryGetValue(index, out value);
        }

        public bool TryGetValue(int index, out T value)
        {
            UnsafeHelpers.SkipParamInit(out value);

            if (index < 0 || index >= GetListCapacity())
                return false;

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
            UnsafeHelpers.SkipParamInit(out value);
            Debug.Assert(name.Length >= MaxNameLength);

            if (index < 0 || index >= GetListCapacity())
                return false;

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

        public void SetValue(int index, ref T value)
        {
            Span<byte> entryBytes = stackalloc byte[_sizeOfEntry];
            ref SaveFsEntry entry = ref GetEntryFromBytes(entryBytes);

            ReadEntry(index, out entry);

            entry.Value = value;

            WriteEntry(index, ref entry);
        }

        public void Remove(ref SaveEntryKey key)
        {
            (int index, int previousIndex) = GetIndexFromKey(ref key);

            ReadEntry(previousIndex, out SaveFsEntry prevEntry);
            ReadEntry(index, out SaveFsEntry entryToDel);

            prevEntry.Next = entryToDel.Next;
            WriteEntry(previousIndex, ref prevEntry);

            Free(index);
        }

        public void ChangeKey(ref SaveEntryKey oldKey, ref SaveEntryKey newKey)
        {
            int index = GetIndexFromKey(ref oldKey).Index;
            int newIndex = GetIndexFromKey(ref newKey).Index;

            if (index == -1) throw new KeyNotFoundException("Old key was not found.");
            if (newIndex != -1) throw new KeyNotFoundException("New key already exists.");

            Span<byte> entryBytes = stackalloc byte[_sizeOfEntry];
            Span<byte> name = entryBytes.Slice(4, MaxNameLength);
            ref SaveFsEntry entry = ref GetEntryFromBytes(entryBytes);

            ReadEntry(index, entryBytes);

            entry.Parent = newKey.Parent;
            newKey.Name.CopyTo(name);

            WriteEntry(index, entryBytes);
        }

        public void TrimFreeEntries()
        {
            Span<byte> entryBytes = stackalloc byte[_sizeOfEntry];
            Span<byte> name = entryBytes.Slice(4, MaxNameLength);
            ref SaveFsEntry entry = ref GetEntryFromBytes(entryBytes);

            ReadEntry(FreeListHeadIndex, out entry);

            int index = entry.Next;

            while (entry.Next > 0)
            {
                ReadEntry(index, out entry);

                entry.Parent = 0;
                entry.Value = new T();
                name.Fill(SaveDataFileSystem.TrimFillValue);

                WriteEntry(index, ref entry);

                index = entry.Next;
            }
        }

        private int GetListCapacity()
        {
            Span<byte> buf = stackalloc byte[sizeof(int)];
            Storage.Read(4, buf).ThrowIfFailure();

            return MemoryMarshal.Read<int>(buf);
        }

        private int GetListLength()
        {
            Span<byte> buf = stackalloc byte[sizeof(int)];
            Storage.Read(0, buf).ThrowIfFailure();

            return MemoryMarshal.Read<int>(buf);
        }

        private void SetListCapacity(int capacity)
        {
            Span<byte> buf = stackalloc byte[sizeof(int)];
            MemoryMarshal.Write(buf, ref capacity);

            Storage.Write(4, buf).ThrowIfFailure();
        }

        private void SetListLength(int length)
        {
            Span<byte> buf = stackalloc byte[sizeof(int)];
            MemoryMarshal.Write(buf, ref length);

            Storage.Write(0, buf).ThrowIfFailure();
        }

        private void ReadEntry(int index, out SaveFsEntry entry)
        {
            Span<byte> bytes = stackalloc byte[_sizeOfEntry];
            ReadEntry(index, bytes);

            entry = GetEntryFromBytes(bytes);
        }

        private void WriteEntry(int index, ref SaveFsEntry entry, ref SaveEntryKey key)
        {
            Span<byte> bytes = stackalloc byte[_sizeOfEntry];
            Span<byte> nameSpan = bytes.Slice(4, MaxNameLength);

            // Copy needed for .NET Framework compat
            ref SaveFsEntry newEntry = ref GetEntryFromBytes(bytes);
            newEntry = entry;

            newEntry.Parent = key.Parent;
            key.Name.CopyTo(nameSpan);

            nameSpan.Slice(key.Name.Length).Fill(0);

            WriteEntry(index, bytes);
        }

        private void WriteEntry(int index, ref SaveFsEntry entry)
        {
            Span<byte> bytes = stackalloc byte[_sizeOfEntry];

            // Copy needed for .NET Framework compat
            ref SaveFsEntry newEntry = ref GetEntryFromBytes(bytes);
            newEntry = entry;

            WriteEntry(index, bytes);
        }

        private void ReadEntry(int index, Span<byte> entry)
        {
            Debug.Assert(entry.Length == _sizeOfEntry);

            int offset = index * _sizeOfEntry;
            Storage.Read(offset, entry);
        }

        private void WriteEntry(int index, Span<byte> entry)
        {
            Debug.Assert(entry.Length == _sizeOfEntry);

            int offset = index * _sizeOfEntry;
            Storage.Write(offset, entry);
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
