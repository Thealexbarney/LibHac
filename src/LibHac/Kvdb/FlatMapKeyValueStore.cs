using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.Kvdb
{
    /// <summary>
    /// Represents a collection of keys and values that are sorted by the key,
    /// and may be saved and loaded from an archive file on disk.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the keys in the key-value store.</typeparam>
    public class FlatMapKeyValueStore<TKey> : IDisposable where TKey : unmanaged, IEquatable<TKey>, IComparable<TKey>
    {
        private const int Alignment = 0x10;

        private FileSystemClient _fsClient;
        private Index _index;
        private BoundedString<Size768> _archivePath;
        private MemoryResource _memoryResource;
        private MemoryResource _memoryResourceForAutoBuffers;

        private static ReadOnlySpan<byte> ArchiveFileName => // /imkvdb.arc
            new[]
            {
                (byte) '/', (byte) 'i', (byte) 'm', (byte) 'k', (byte) 'v', (byte) 'd', (byte) 'b', (byte) '.',
                (byte) 'a', (byte) 'r', (byte) 'c'
            };

        public int Count => _index.Count;

        public FlatMapKeyValueStore()
        {
            _index = new Index();

            Unsafe.SkipInit(out _archivePath);
            _archivePath.Get()[0] = 0;
        }

        /// <summary>
        /// Initializes a <see cref="FlatMapKeyValueStore{T}"/>. Reads and writes the store to and from the file imkvdb.arc
        /// in the specified <paramref name="rootPath"/> directory. This directory must exist when calling <see cref="Initialize"/>,
        /// but it is not required for the imkvdb.arc file to exist.
        /// </summary>
        /// <param name="fsClient">The <see cref="FileSystemClient"/> to use for reading and writing the archive.</param>
        /// <param name="rootPath">The directory path used to load and save the archive file. Directory must already exist.</param>
        /// <param name="capacity">The maximum number of entries that can be stored.</param>
        /// <param name="memoryResource"><see cref="MemoryResource"/> for allocating buffers to hold entries and values.</param>
        /// <param name="autoBufferMemoryResource"><see cref="MemoryResource"/> for allocating temporary buffers
        /// when reading and writing the store to a file.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        public Result Initialize(FileSystemClient fsClient, U8Span rootPath, int capacity,
            MemoryResource memoryResource, MemoryResource autoBufferMemoryResource)
        {
            // The root path must be an existing directory
            Result rc = fsClient.GetEntryType(out DirectoryEntryType rootEntryType, rootPath);
            if (rc.IsFailure()) return rc;

            if (rootEntryType == DirectoryEntryType.File)
                return ResultFs.PathNotFound.Log();

            var sb = new U8StringBuilder(_archivePath.Get());
            sb.Append(rootPath).Append(ArchiveFileName);

            rc = _index.Initialize(capacity, memoryResource);
            if (rc.IsFailure()) return rc;

            _fsClient = fsClient;
            _memoryResource = memoryResource;
            _memoryResourceForAutoBuffers = autoBufferMemoryResource;

            return Result.Success;
        }

        public void Dispose()
        {
            _index.Dispose();
        }

        /// <summary>
        /// Clears all entries in the <see cref="FlatMapKeyValueStore{T}"/> and loads all entries
        /// from the database archive file, if it exists.
        /// </summary>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        public Result Load()
        {
            // Clear any existing entries.
            _index.Clear();

            var buffer = new AutoBuffer();

            try
            {
                Result rc = ReadArchive(ref buffer);
                if (rc.IsFailure())
                {
                    // If the file is not found, we don't have any entries to load.
                    if (ResultFs.PathNotFound.Includes(rc))
                        return Result.Success.LogConverted(rc);

                    return rc;
                }

                rc = LoadFrom(buffer.Get());
                if (rc.IsFailure()) return rc;

                return Result.Success;
            }
            finally
            {
                buffer.Dispose();
            }
        }

        /// <summary>
        /// Writes all entries in the <see cref="FlatMapKeyValueStore{T}"/> to a database archive file.
        /// </summary>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        public Result Save()
        {
            // Create a buffer to hold the archive.
            var buffer = new AutoBuffer();
            Result rc = buffer.Initialize(CalculateArchiveSize(), _memoryResourceForAutoBuffers);
            if (rc.IsFailure()) return rc;

            try
            {
                // Write the archive to the buffer.
                Span<byte> span = buffer.Get();
                var writer = new KeyValueArchiveBufferWriter(span);
                SaveTo(ref writer);

                // Save the buffer to disk.
                return CommitArchive(span);
            }
            finally
            {
                buffer.Dispose();
            }
        }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="valueSize">If the method returns successfully, contains the size of
        /// the value written to <paramref name="valueBuffer"/>. This may be smaller than the
        /// actual length of the value if <paramref name="valueBuffer"/> was not large enough.</param>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="valueBuffer">If the method returns successfully, contains the value
        /// associated with the specified key. Otherwise, the buffer will not be modified.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        /// <remarks>Possible <see cref="Result"/>s:<br/>
        /// <see cref="ResultKvdb.KeyNotFound"/>
        /// The specified key was not found in the <see cref="FlatMapKeyValueStore{T}"/>.</remarks>
        public Result Get(out int valueSize, in TKey key, Span<byte> valueBuffer)
        {
            UnsafeHelpers.SkipParamInit(out valueSize);

            // Find entry.
            ConstIterator iterator = _index.GetLowerBoundConstIterator(in key);
            if (iterator.IsEnd())
                return ResultKvdb.KeyNotFound.Log();

            if (!key.Equals(iterator.Get().Key))
                return ResultKvdb.KeyNotFound.Log();

            // Truncate the output if the buffer is too small.
            ReadOnlySpan<byte> value = iterator.GetValue();
            int size = Math.Min(valueBuffer.Length, value.Length);

            value.Slice(0, size).CopyTo(valueBuffer);
            valueSize = size;
            return Result.Success;
        }

        /// <summary>
        /// Adds the specified key and value to the <see cref="FlatMapKeyValueStore{T}"/>.
        /// The existing value is replaced if the key already exists.
        /// </summary>
        /// <param name="key">The key to add.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        public Result Set(in TKey key, ReadOnlySpan<byte> value)
        {
            return _index.Set(in key, value);
        }

        /// <summary>
        /// Deletes an element from the <see cref="FlatMapKeyValueStore{T}"/>.
        /// </summary>
        /// <param name="key">The key of the element to delete.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        /// <remarks>Possible <see cref="Result"/>s:<br/>
        /// <see cref="ResultKvdb.KeyNotFound"/>
        /// The specified key was not found in the <see cref="FlatMapKeyValueStore{T}"/>.</remarks>
        public Result Delete(in TKey key)
        {
            if (!_index.Delete(in key))
                return ResultKvdb.KeyNotFound.Log();

            return Result.Success;
        }

        /// <summary>
        /// Creates an <see cref="Iterator"/> that starts at the first element in the <see cref="FlatMapKeyValueStore{T}"/>.
        /// </summary>
        /// <returns>The created iterator.</returns>
        public Iterator GetBeginIterator()
        {
            return _index.GetBeginIterator();
        }

        /// <summary>
        /// Creates an <see cref="Iterator"/> that starts at the first element equal to or greater than
        /// <paramref name="key"/> in the <see cref="FlatMapKeyValueStore{T}"/>.
        /// </summary>
        /// <param name="key">The key at which to begin iteration.</param>
        /// <returns>The created iterator.</returns>
        public Iterator GetLowerBoundIterator(in TKey key)
        {
            return _index.GetLowerBoundIterator(in key);
        }

        /// <summary>
        /// Fixes an iterator's current position and total length so that after an entry
        /// is added or removed, the iterator will still be on the same entry.
        /// </summary>
        /// <param name="iterator">The iterator to fix.</param>
        /// <param name="key">The key that was added or removed.</param>
        public void FixIterator(ref Iterator iterator, in TKey key)
        {
            _index.FixIterator(ref iterator, in key);
        }

        /// <summary>
        /// Reads the database archive file into the provided buffer.
        /// </summary>
        /// <param name="buffer">The buffer the file will be read into.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        private Result ReadArchive(ref AutoBuffer buffer)
        {
            Result rc = _fsClient.OpenFile(out FileHandle file, new U8Span(_archivePath.Get()), OpenMode.Read);
            if (rc.IsFailure()) return rc;

            try
            {
                rc = _fsClient.GetFileSize(out long archiveSize, file);
                if (rc.IsFailure()) return rc;

                rc = buffer.Initialize(archiveSize, _memoryResourceForAutoBuffers);
                if (rc.IsFailure()) return rc;

                rc = _fsClient.ReadFile(file, 0, buffer.Get());
                if (rc.IsFailure()) return rc;

                return Result.Success;
            }
            finally
            {
                _fsClient.CloseFile(file);
            }
        }

        /// <summary>
        /// Loads all key-value pairs from a key-value archive.
        /// All keys in the archive are assumed to be in ascending order.
        /// </summary>
        /// <param name="buffer">The buffer containing the key-value archive.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        private Result LoadFrom(ReadOnlySpan<byte> buffer)
        {
            var reader = new KeyValueArchiveBufferReader(buffer);

            Result rc = reader.ReadEntryCount(out int entryCount);
            if (rc.IsFailure()) return rc;

            for (int i = 0; i < entryCount; i++)
            {
                // Get size of key/value.
                rc = reader.GetKeyValueSize(out _, out int valueSize);
                if (rc.IsFailure()) return rc;

                // Allocate memory for value.
                MemoryResource.Buffer newValue = _memoryResource.Allocate(valueSize, Alignment);
                if (!newValue.IsValid)
                    return ResultKvdb.AllocationFailed.Log();

                bool success = false;
                try
                {
                    // Read key and value.
                    Unsafe.SkipInit(out TKey key);

                    rc = reader.ReadKeyValue(SpanHelpers.AsByteSpan(ref key), newValue.Get());
                    if (rc.IsFailure()) return rc;

                    rc = _index.AppendUnsafe(in key, newValue);
                    if (rc.IsFailure()) return rc;

                    success = true;
                }
                finally
                {
                    // Deallocate the buffer if we didn't succeed.
                    if (!success)
                        _memoryResource.Deallocate(ref newValue, Alignment);
                }
            }

            return Result.Success;
        }

        private void SaveTo(ref KeyValueArchiveBufferWriter writer)
        {
            writer.WriteHeader(_index.Count);

            ConstIterator iterator = _index.GetBeginConstIterator();
            while (!iterator.IsEnd())
            {
                ReadOnlySpan<byte> key = SpanHelpers.AsReadOnlyByteSpan(in iterator.Get().Key);
                writer.WriteEntry(key, iterator.GetValue());

                iterator.Next();
            }
        }

        private Result CommitArchive(ReadOnlySpan<byte> buffer)
        {
            var path = new U8Span(_archivePath.Get());

            // Try to delete the archive, but allow deletion failure.
            _fsClient.DeleteFile(path).IgnoreResult();

            // Create new archive.
            Result rc = _fsClient.CreateFile(path, buffer.Length);
            if (rc.IsFailure()) return rc;

            // Write data to the archive.
            rc = _fsClient.OpenFile(out FileHandle file, path, OpenMode.Write);
            if (rc.IsFailure()) return rc;

            try
            {
                rc = _fsClient.WriteFile(file, 0, buffer, WriteOption.Flush);
                if (rc.IsFailure()) return rc;
            }
            finally
            {
                _fsClient.CloseFile(file);
            }

            return Result.Success;
        }

        private long CalculateArchiveSize()
        {
            var calculator = new KeyValueArchiveSizeCalculator();
            calculator.Initialize();
            ConstIterator iterator = _index.GetBeginConstIterator();

            while (!iterator.IsEnd())
            {
                calculator.AddEntry(Unsafe.SizeOf<TKey>(), iterator.GetValue().Length);
                iterator.Next();
            }

            return calculator.Size;
        }

        /// <summary>
        /// Represents a key-value pair contained in a <see cref="FlatMapKeyValueStore{T}"/>.
        /// </summary>
        public struct KeyValue
        {
            public TKey Key;
            public MemoryResource.Buffer Value;

            public KeyValue(in TKey key, MemoryResource.Buffer value)
            {
                Key = key;
                Value = value;
            }
        }

        /// <summary>
        /// Manages the sorted list of <see cref="KeyValue"/> entries in a <see cref="FlatMapKeyValueStore{T}"/>.
        /// </summary>
        private struct Index : IDisposable
        {
            private int _count;
            private int _capacity;
            private KeyValue[] _entries;
            private MemoryResource _memoryResource;

            /// <summary>
            /// The number of elements currently in the <see cref="Index"/>.
            /// </summary>
            public int Count => _count;

            /// <summary>
            /// Initializes the <see cref="Index"/>
            /// </summary>
            /// <param name="capacity">The maximum number of elements the <see cref="Index"/> will be able to hold.</param>
            /// <param name="memoryResource">The <see cref="MemoryResource"/> that will be used to allocate
            /// memory for values added to the <see cref="Index"/>.</param>
            /// <returns>The <see cref="Result"/> of the operation.</returns>
            public Result Initialize(int capacity, MemoryResource memoryResource)
            {
                // Initialize must only be called once.
                Assert.SdkRequiresNull(_entries);
                Assert.SdkRequiresNotNull(memoryResource);

                // FS uses the provided MemoryResource to allocate the KeyValue array.
                // We can't do that here because the array will contain managed references.
                _entries = new KeyValue[capacity];
                _capacity = capacity;
                _memoryResource = memoryResource;

                return Result.Success;
            }

            public void Dispose()
            {
                if (_entries != null)
                {
                    Clear();
                    _entries = null;
                }
            }

            /// <summary>
            /// Adds the specified key and value to the <see cref="Index"/>.
            /// The existing value is replaced if the key already exists.
            /// </summary>
            /// <param name="key">The key to add.</param>
            /// <param name="value">The value to add.</param>
            /// <returns>The <see cref="Result"/> of the operation.</returns>
            public Result Set(in TKey key, ReadOnlySpan<byte> value)
            {
                // The list is sorted by key. Find the index to insert at.
                int index = GetLowerBoundIndex(in key);

                if (index != _count && _entries[index].Key.Equals(key))
                {
                    // Key already exists. Free the old value.
                    _memoryResource.Deallocate(ref _entries[index].Value, Alignment);
                }
                else
                {
                    // Need to insert a new entry. Check if there's room and shift the existing entries.
                    if (_count >= _capacity)
                        return ResultKvdb.OutOfKeyResource.Log();

                    Array.Copy(_entries, index, _entries, index + 1, _count - index);
                    _count++;
                }

                // Allocate new value.
                MemoryResource.Buffer newValue = _memoryResource.Allocate(value.Length, Alignment);
                if (!newValue.IsValid)
                    return ResultKvdb.AllocationFailed.Log();

                value.CopyTo(newValue.Get());

                // Add the new entry to the list.
                _entries[index] = new KeyValue(in key, newValue);

                return Result.Success;
            }

            /// <summary>
            /// Adds the specified key and value to the end of the list.
            /// Does not verify that the list will be sorted properly. The caller must make sure the sorting will be correct.
            /// Used when populating a new <see cref="Index"/> with already sorted entries.
            /// </summary>
            /// <param name="key">The key to add.</param>
            /// <param name="value">The value to add.</param>
            /// <returns>The <see cref="Result"/> of the operation.</returns>
            public Result AppendUnsafe(in TKey key, MemoryResource.Buffer value)
            {
                if (_count >= _capacity)
                    return ResultKvdb.OutOfKeyResource.Log();

                if (_count > 0)
                {
                    // The key being added must be greater than the last key in the list.
                    Assert.SdkGreater(key, _entries[_count - 1].Key);
                }

                _entries[_count] = new KeyValue(in key, value);
                _count++;

                return Result.Success;
            }

            /// <summary>
            /// Removes all keys and values from the <see cref="Index"/>.
            /// </summary>
            public void Clear()
            {
                Span<KeyValue> entries = _entries.AsSpan(0, _count);

                for (int i = 0; i < entries.Length; i++)
                {
                    _memoryResource.Deallocate(ref entries[i].Value, Alignment);
                }

                _count = 0;
            }

            /// <summary>
            /// Deletes an element from the <see cref="Index"/>.
            /// </summary>
            /// <param name="key">The key of the element to delete.</param>
            /// <returns><see langword="true"/> if the item was found and deleted.
            /// <see langword="false"/> if the key was not in the store.</returns>
            public bool Delete(in TKey key)
            {
                int index = GetLowerBoundIndex(in key);

                // Make sure the key was found.
                if (index == _count || !_entries[index].Key.Equals(key))
                {
                    return false;
                }

                // Free the value buffer and shift the remaining elements down
                _memoryResource.Deallocate(ref _entries[index].Value, Alignment);

                Array.Copy(_entries, index + 1, _entries, index, _count - (index + 1));
                _count--;

                return true;
            }

            /// <summary>
            /// Returns an iterator starting at the first element in the <see cref="Index"/>.
            /// </summary>
            /// <returns>The created iterator.</returns>
            public Iterator GetBeginIterator()
            {
                return new Iterator(_entries, 0, _count);
            }

            /// <summary>
            /// Returns a read-only iterator starting at the first element in the <see cref="Index"/>.
            /// </summary>
            /// <returns>The created iterator.</returns>
            public ConstIterator GetBeginConstIterator()
            {
                return new ConstIterator(_entries, 0, _count);
            }

            /// <summary>
            /// Returns an iterator starting at the first element greater than or equal to <paramref name="key"/>.
            /// </summary>
            /// <param name="key">The key at which to begin iteration.</param>
            /// <returns>The created iterator.</returns>
            public Iterator GetLowerBoundIterator(in TKey key)
            {
                int index = GetLowerBoundIndex(in key);

                return new Iterator(_entries, index, _count);
            }

            /// <summary>
            /// Returns a read-only iterator starting at the first element greater than or equal to <paramref name="key"/>.
            /// </summary>
            /// <param name="key">The key at which to begin iteration.</param>
            /// <returns>The created iterator.</returns>
            public ConstIterator GetLowerBoundConstIterator(in TKey key)
            {
                int index = GetLowerBoundIndex(in key);

                return new ConstIterator(_entries, index, _count);
            }

            /// <summary>
            /// Fixes an iterator's current position and total length so that after an entry
            /// is added or removed, the iterator will still be on the same entry.
            /// </summary>
            /// <param name="iterator">The iterator to fix.</param>
            /// <param name="key">The key that was added or removed.</param>
            public void FixIterator(ref Iterator iterator, in TKey key)
            {
                int keyIndex = GetLowerBoundIndex(in key);
                iterator.Fix(keyIndex, _count);
            }

            private int GetLowerBoundIndex(in TKey key)
            {
                // The AsSpan takes care of any bounds checking
                ReadOnlySpan<KeyValue> entries = _entries.AsSpan(0, _count);

                return BinarySearch(ref MemoryMarshal.GetReference(entries), entries.Length, in key);
            }

            private static int BinarySearch(ref KeyValue spanStart, int length, in TKey item)
            {
                // A tweaked version of .NET's SpanHelpers.BinarySearch
                int lo = 0;
                int hi = length - 1;

                TKey tempItem = item;

                while (lo <= hi)
                {
                    int i = (int)(((uint)hi + (uint)lo) >> 1);

                    int c = tempItem.CompareTo(Unsafe.Add(ref spanStart, i).Key);
                    if (c == 0)
                    {
                        return i;
                    }
                    else if (c > 0)
                    {
                        lo = i + 1;
                    }
                    else
                    {
                        hi = i - 1;
                    }
                }

                // If not found, return the index of the first element that is greater than item
                return lo;
            }
        }

        /// <summary>
        /// Iterates through the elements in a <see cref="FlatMapKeyValueStore{TKey}"/>.
        /// </summary>
        public struct Iterator
        {
            private KeyValue[] _entries;
            private int _index;
            private int _length;

            internal Iterator(KeyValue[] entries, int startIndex, int length)
            {
                _entries = entries;
                _index = startIndex;
                _length = length;
            }

            public ref KeyValue Get() => ref _entries[_index];
            public Span<byte> GetValue() => _entries[_index].Value.Get();

            public ref T GetValue<T>() where T : unmanaged
            {
                return ref SpanHelpers.AsStruct<T>(_entries[_index].Value.Get());
            }

            public void Next() => _index++;
            public bool IsEnd() => _index == _length;

            /// <summary>
            /// Fixes the iterator current position and total length so that after an entry
            /// is added or removed, the iterator will still be on the same entry.
            /// </summary>
            /// <param name="entryIndex">The index of the added or removed entry.</param>
            /// <param name="newLength">The new length of the list.</param>
            /// <remarks></remarks>
            public void Fix(int entryIndex, int newLength)
            {
                if (newLength > _length)
                {
                    // An entry was added. entryIndex is the index of the new entry.

                    // Only one entry can be added at a time.
                    Assert.SdkEqual(newLength, _length + 1);

                    if (entryIndex <= _index)
                    {
                        // The new entry was added at or before the iterator's current index.
                        // Increment the index so we continue to be on the same entry.
                        _index++;
                    }

                    _length = newLength;
                }
                else if (newLength < _length)
                {
                    // An entry was removed. entryIndex is the index where the removed entry used to be.

                    // Only one entry can be removed at a time.
                    Assert.SdkEqual(newLength, _length - 1);

                    if (entryIndex < _index)
                    {
                        // The removed entry was before the iterator's current index.
                        // Decrement the index so we continue to be on the same entry.
                        // If the entry at the iterator's current index was removed,
                        // the iterator will now be at the next entry.
                        _index--;
                    }

                    _length = newLength;
                }
            }
        }

        /// <summary>
        /// Iterates through the elements in a <see cref="FlatMapKeyValueStore{TKey}"/>.
        /// </summary>
        public struct ConstIterator
        {
            private KeyValue[] _entries;
            private int _index;
            private int _length;

            public ConstIterator(KeyValue[] entries, int startIndex, int length)
            {
                _entries = entries;
                _index = startIndex;
                _length = length;
            }

            public ref readonly KeyValue Get() => ref _entries[_index];
            public ReadOnlySpan<byte> GetValue() => _entries[_index].Value.Get();

            public void Next() => _index++;
            public bool IsEnd() => _index == _length;
        }
    }
}
