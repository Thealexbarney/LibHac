using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Kvdb;
using LibHac.Tests.Fs.FileSystemClientTests;
using Xunit;

using TTest = System.Int32;

namespace LibHac.Tests.Kvdb
{
    public class FlatMapKeyValueStoreTests
    {
        private static readonly U8String MountName = new U8String("mount");
        private static readonly U8String RootPath = new U8String("mount:/");
        private static readonly U8String ArchiveFilePath = new U8String("mount:/imkvdb.arc");

        private static (FlatMapKeyValueStore<T> kvStore, FileSystemClient fsClient) Create<T>(int capacity)
            where T : unmanaged, IEquatable<T>, IComparable<T>
        {
            FileSystemClient fsClient = FileSystemServerFactory.CreateClient(false);

            var mountedFs = new InMemoryFileSystem();
            fsClient.Register(MountName, mountedFs).ThrowIfFailure();

            FlatMapKeyValueStore<T> kvStore = Create<T>(fsClient, capacity);

            return (kvStore, fsClient);
        }

        private static FlatMapKeyValueStore<T> Create<T>(FileSystemClient fsClient, int capacity)
            where T : unmanaged, IEquatable<T>, IComparable<T>
        {
            var memoryResource = new ArrayPoolMemoryResource();

            var kvStore = new FlatMapKeyValueStore<T>();
            kvStore.Initialize(fsClient, RootPath, capacity, memoryResource, memoryResource).ThrowIfFailure();

            return kvStore;
        }

        private static byte[][] GenerateValues(int count, int startingSize)
        {
            byte[][] values = new byte[count][];

            for (int i = 0; i < count; i++)
            {
                byte[] value = new byte[startingSize + i];
                value.AsSpan().Fill((byte)count);
                values[i] = value;
            }

            return values;
        }

        private static Result PopulateKvStore(FlatMapKeyValueStore<TTest> kvStore, out byte[][] addedValues, int count,
            int startingValueSize = 20, int seed = -1)
        {
            addedValues = null;
            byte[][] values = GenerateValues(count, startingValueSize);

            if (seed == -1)
            {
                for (TTest i = 0; i < count; i++)
                {
                    Result rc = kvStore.Set(in i, values[i]);
                    if (rc.IsFailure()) return rc;
                }
            }
            else
            {
                var rng = new FullCycleRandom(count, seed);

                for (int i = 0; i < count; i++)
                {
                    TTest index = rng.Next();
                    Result rc = kvStore.Set(in index, values[index]);
                    if (rc.IsFailure()) return rc;
                }
            }

            addedValues = values;
            return Result.Success;
        }

        [Fact]
        public void Count_EmptyStore_ReturnsZero()
        {
            (FlatMapKeyValueStore<TTest> kvStore, FileSystemClient _) = Create<TTest>(10);

            Assert.Equal(0, kvStore.Count);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        public void Count_PopulatedStore_ReturnsCorrectCount(int count)
        {
            (FlatMapKeyValueStore<TTest> kvStore, FileSystemClient _) = Create<TTest>(10);
            Assert.Success(PopulateKvStore(kvStore, out _, count));

            Assert.Equal(count, kvStore.Count);
        }

        [Fact]
        public void Load_FileDoesNotExist_ExistingEntriesAreCleared()
        {
            const int count = 10;

            (FlatMapKeyValueStore<TTest> kvStore, FileSystemClient _) = Create<TTest>(count);
            Assert.Success(PopulateKvStore(kvStore, out _, count));

            Assert.Success(kvStore.Load());
            Assert.Equal(0, kvStore.Count);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        public void Load_AfterArchiveHasBeenSaved_AllEntriesAreLoaded(int count)
        {
            (FlatMapKeyValueStore<TTest> kvStore, FileSystemClient fsClient) = Create<TTest>(count + 5);
            Assert.Success(PopulateKvStore(kvStore, out byte[][] values, count));

            Assert.Success(kvStore.Save());
            kvStore.Dispose();

            kvStore = Create<TTest>(fsClient, count + 5);
            Assert.Success(kvStore.Load());

            FlatMapKeyValueStore<TTest>.Iterator iterator = kvStore.GetBeginIterator();

            // Check if each key-value pair matches
            for (int i = 0; i < count; i++)
            {
                TTest expectedKey = i;
                byte[] expectedValue = values[i];

                ref FlatMapKeyValueStore<TTest>.KeyValue kv = ref iterator.Get();

                Assert.Equal(expectedKey, kv.Key);
                Assert.Equal(expectedValue, kv.Value.Get().ToArray());

                iterator.Next();
            }

            Assert.True(iterator.IsEnd());
        }

        [Fact]
        public void Load_CapacityIsTooSmall_ReturnsOutOfKeyResource()
        {
            const int count = 10;

            (FlatMapKeyValueStore<TTest> kvStore, FileSystemClient fsClient) = Create<TTest>(count);
            Assert.Success(PopulateKvStore(kvStore, out _, count));

            Assert.Success(kvStore.Save());
            kvStore.Dispose();


            kvStore = Create<TTest>(fsClient, count - 5);
            Assert.Result(ResultKvdb.OutOfKeyResource, kvStore.Load());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        public void Save_ArchiveFileIsWrittenToDisk(int count)
        {
            (FlatMapKeyValueStore<TTest> kvStore, FileSystemClient fsClient) = Create<TTest>(count + 5);
            Assert.Success(PopulateKvStore(kvStore, out _, count));

            Assert.Success(kvStore.Save());

            Assert.Success(fsClient.GetEntryType(out DirectoryEntryType entryType, ArchiveFilePath));
            Assert.Equal(DirectoryEntryType.File, entryType);
        }

        [Fact]
        public void Get_PopulatedStoreAndEntryDoesNotExist_ReturnsKeyNotFound()
        {
            const int count = 10;

            (FlatMapKeyValueStore<TTest> kvStore, FileSystemClient _) = Create<TTest>(count);
            Assert.Success(PopulateKvStore(kvStore, out _, count));

            TTest key = 20;
            byte[] value = new byte[20];

            Result rc = kvStore.Get(out int _, in key, value);
            Assert.Result(ResultKvdb.KeyNotFound, rc);
        }

        [Fact]
        public void Get_PopulatedStore_GetsCorrectValueSizes()
        {
            const int count = 10;
            const int startingValueSize = 20;
            const int rngSeed = 220;

            (FlatMapKeyValueStore<TTest> kvStore, FileSystemClient _) = Create<TTest>(count);
            Assert.Success(PopulateKvStore(kvStore, out _, count, startingValueSize, rngSeed));

            // Check the size of each entry
            byte[] value = new byte[100];

            for (TTest i = 0; i < count; i++)
            {
                Assert.Success(kvStore.Get(out int valueSize, in i, value));
                Assert.Equal(startingValueSize + i, valueSize);
            }
        }

        [Fact]
        public void Get_PopulatedStoreAndEntryExists_GetsCorrectValue()
        {
            const int count = 10;
            const int startingValueSize = 20;
            const int rngSeed = 188;

            (FlatMapKeyValueStore<TTest> kvStore, FileSystemClient _) = Create<TTest>(count);
            Assert.Success(PopulateKvStore(kvStore, out byte[][] values, count, startingValueSize, rngSeed));

            // Check if each value matches
            byte[] value = new byte[100];

            for (int i = 0; i < count; i++)
            {
                TTest key = i;
                Assert.Success(kvStore.Get(out int _, in key, value));
                Assert.Equal(values[i], value.AsSpan(0, startingValueSize + i).ToArray());
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        public void Set_StoreIsFullAndEntryDoesNotExist_ReturnsOutOfKeyResource(int count)
        {
            (FlatMapKeyValueStore<TTest> kvStore, FileSystemClient _) = Create<TTest>(count);
            Assert.Success(PopulateKvStore(kvStore, out byte[][] values, count));

            TTest key = count;
            Result rc = kvStore.Set(in key, values[0]);

            Assert.Result(ResultKvdb.OutOfKeyResource, rc);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        [InlineData(9)]
        public void Set_StoreIsFullAndEntryAlreadyExists_ReplacesOriginalValue(int entryToReplace)
        {
            const int count = 10;

            (FlatMapKeyValueStore<TTest> kvStore, FileSystemClient _) = Create<TTest>(count);
            Assert.Success(PopulateKvStore(kvStore, out _, count));

            TTest key = entryToReplace;
            byte[] value = new byte[15];
            value.AsSpan().Fill(0xFF);

            Assert.Success(kvStore.Set(in key, value));

            // Read back the value
            byte[] readValue = new byte[20];
            Assert.Success(kvStore.Get(out int valueSize, in key, readValue));

            // Check the value contents and size
            Assert.Equal(value.Length, valueSize);
            Assert.Equal(value, readValue.AsSpan(0, valueSize).ToArray());
        }

        [Theory]
        [InlineData(10, 89)]
        [InlineData(10, 50)]
        [InlineData(1000, 75367)]
        [InlineData(1000, 117331)]
        public void Set_StoreIsFilledInRandomOrder_EntriesAreSorted(int entryCount, int rngSeed)
        {
            (FlatMapKeyValueStore<TTest> kvStore, FileSystemClient _) = Create<TTest>(entryCount + 10);
            Assert.Success(PopulateKvStore(kvStore, out byte[][] values, entryCount, 20, rngSeed));

            FlatMapKeyValueStore<TTest>.Iterator iterator = kvStore.GetBeginIterator();

            // Check if each key-value pair matches
            for (int i = 0; i < entryCount; i++)
            {
                TTest expectedKey = i;
                byte[] expectedValue = values[i];

                ref FlatMapKeyValueStore<TTest>.KeyValue kv = ref iterator.Get();

                Assert.Equal(expectedKey, kv.Key);
                Assert.Equal(expectedValue, kv.Value.Get().ToArray());

                iterator.Next();
            }

            Assert.True(iterator.IsEnd());
        }

        [Fact]
        public void Delete_EmptyStore_ReturnsKeyNotFound()
        {
            const int count = 10;
            TTest keyToDelete = 4;

            (FlatMapKeyValueStore<TTest> kvStore, FileSystemClient _) = Create<TTest>(count);

            Result rc = kvStore.Delete(in keyToDelete);
            Assert.Result(ResultKvdb.KeyNotFound, rc);
        }

        [Fact]
        public void Delete_PopulatedStoreAndEntryDoesNotExist_ReturnsKeyNotFound()
        {
            const int count = 10;
            TTest keyToDelete = 44;

            (FlatMapKeyValueStore<TTest> kvStore, FileSystemClient _) = Create<TTest>(count);
            Assert.Success(PopulateKvStore(kvStore, out _, count));

            Result rc = kvStore.Delete(in keyToDelete);
            Assert.Result(ResultKvdb.KeyNotFound, rc);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        [InlineData(9)]
        public void Delete_PopulatedStoreAndEntryExists_CannotGetAfterDeletion(int entryToDelete)
        {
            const int count = 10;
            const int startingValueSize = 20;
            const int rngSeed = 114;

            (FlatMapKeyValueStore<TTest> kvStore, FileSystemClient _) = Create<TTest>(count);
            Assert.Success(PopulateKvStore(kvStore, out _, count, startingValueSize, rngSeed));

            TTest keyToDelete = entryToDelete;
            Assert.Success(kvStore.Delete(in keyToDelete));

            byte[] value = new byte[20];

            Result rc = kvStore.Get(out int _, in keyToDelete, value);
            Assert.Result(ResultKvdb.KeyNotFound, rc);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        [InlineData(9)]
        public void Delete_PopulatedStoreAndEntryExists_CountIsDecremented(int entryToDelete)
        {
            const int count = 10;

            (FlatMapKeyValueStore<TTest> kvStore, FileSystemClient _) = Create<TTest>(count);
            Assert.Success(PopulateKvStore(kvStore, out _, count));

            TTest keyToDelete = entryToDelete;
            Assert.Success(kvStore.Delete(in keyToDelete));

            Assert.Equal(count - 1, kvStore.Count);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        [InlineData(9)]
        public void Delete_PopulatedStoreAndEntryExists_RemainingEntriesAreSorted(int entryToDelete)
        {
            const int count = 10;

            (FlatMapKeyValueStore<TTest> kvStore, FileSystemClient _) = Create<TTest>(count);
            Assert.Success(PopulateKvStore(kvStore, out _, count));

            TTest keyToDelete = entryToDelete;
            Assert.Success(kvStore.Delete(in keyToDelete));

            FlatMapKeyValueStore<TTest>.Iterator iterator = kvStore.GetBeginIterator();

            // Check if the remaining keys exist in order
            for (int i = 0; i < count; i++)
            {
                if (i == entryToDelete)
                    continue;

                TTest expectedKey = i;

                Assert.Equal(expectedKey, iterator.Get().Key);

                iterator.Next();
            }

            Assert.True(iterator.IsEnd());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        [InlineData(9)]
        public void GetLowerBoundIterator_EntryExists_StartsIterationAtSpecifiedKey(int startEntry)
        {
            const int count = 10;

            (FlatMapKeyValueStore<TTest> kvStore, FileSystemClient _) = Create<TTest>(count);
            Assert.Success(PopulateKvStore(kvStore, out _, count));

            TTest startingKey = startEntry;
            FlatMapKeyValueStore<TTest>.Iterator iterator = kvStore.GetLowerBoundIterator(in startingKey);

            Assert.False(iterator.IsEnd());
            Assert.Equal(startingKey, iterator.Get().Key);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(9)]
        public void GetLowerBoundIterator_EntryDoesNotExist_StartsIterationAtNextLargestKey(int startIndex)
        {
            const int count = 10;
            const int startingValueSize = 20;

            (FlatMapKeyValueStore<TTest> kvStore, FileSystemClient _) = Create<TTest>(count);

            byte[][] values = GenerateValues(count, startingValueSize);

            for (int i = 0; i < count; i++)
            {
                TTest key = i * 2;
                Assert.Success(kvStore.Set(in key, values[i]));
            }

            TTest startingKey = startIndex;
            TTest nextLargestKey = startIndex + 1;
            FlatMapKeyValueStore<TTest>.Iterator iterator = kvStore.GetLowerBoundIterator(in startingKey);

            Assert.False(iterator.IsEnd());
            Assert.Equal(nextLargestKey, iterator.Get().Key);
        }

        [Fact]
        public void GetLowerBoundIterator_LargerThanAllKeysInStore_IteratorIsAtEnd()
        {
            const int count = 10;
            const int startIndex = 20;

            (FlatMapKeyValueStore<TTest> kvStore, FileSystemClient _) = Create<TTest>(count);
            Assert.Success(PopulateKvStore(kvStore, out _, count));

            TTest key = startIndex;
            FlatMapKeyValueStore<TTest>.Iterator iterator = kvStore.GetLowerBoundIterator(in key);

            Assert.True(iterator.IsEnd());
        }

        [Theory]
        [InlineData(2, 3, 2)]
        [InlineData(3, 3, 4)]
        [InlineData(5, 3, 5)]
        public void FixIterator_RemoveEntry_IteratorPointsToSameEntry(int positionWhenRemoving, int entryToRemove, int expectedNewPosition)
        {
            const int count = 10;

            (FlatMapKeyValueStore<TTest> kvStore, FileSystemClient _) = Create<TTest>(count);
            Assert.Success(PopulateKvStore(kvStore, out _, count));

            FlatMapKeyValueStore<TTest>.Iterator iterator = kvStore.GetBeginIterator();

            while (iterator.Get().Key != positionWhenRemoving)
            {
                iterator.Next();
            }

            TTest keyToRemove = entryToRemove;
            Assert.Success(kvStore.Delete(in keyToRemove));

            kvStore.FixIterator(ref iterator, in keyToRemove);

            TTest expectedKey = expectedNewPosition;
            Assert.Equal(expectedKey, iterator.Get().Key);
        }

        [Theory]
        [InlineData(6, 7, 6)]
        [InlineData(8, 7, 8)]
        public void FixIterator_AddEntry_IteratorPointsToSameEntry(int positionWhenAdding, int entryToAdd, int expectedNewPosition)
        {
            const int count = 10;
            const int startingValueSize = 20;

            (FlatMapKeyValueStore<TTest> kvStore, FileSystemClient _) = Create<TTest>(count + 5);

            byte[][] values = GenerateValues(count, startingValueSize);

            for (int i = 0; i < count; i++)
            {
                TTest key = i * 2;
                Assert.Success(kvStore.Set(in key, values[i]));
            }

            FlatMapKeyValueStore<TTest>.Iterator iterator = kvStore.GetBeginIterator();

            while (iterator.Get().Key != positionWhenAdding)
            {
                iterator.Next();
            }

            TTest keyToAdd = entryToAdd;
            byte[] valueToAdd = new byte[10];

            Assert.Success(kvStore.Set(in keyToAdd, valueToAdd));

            kvStore.FixIterator(ref iterator, in keyToAdd);

            TTest expectedKey = expectedNewPosition;
            Assert.Equal(expectedKey, iterator.Get().Key);
        }

        [Fact]
        public void IteratorIsEnd_EmptyStore_ReturnsTrue()
        {
            const int count = 10;

            (FlatMapKeyValueStore<TTest> kvStore, FileSystemClient _) = Create<TTest>(count);

            FlatMapKeyValueStore<TTest>.Iterator iterator = kvStore.GetBeginIterator();
            Assert.True(iterator.IsEnd());
        }

        [Fact]
        public void IteratorIsEnd_PopulatedStore_ReturnsFalseUntilFinishedIterating()
        {
            const int count = 10;

            (FlatMapKeyValueStore<TTest> kvStore, FileSystemClient _) = Create<TTest>(count);
            Assert.Success(PopulateKvStore(kvStore, out _, count));

            FlatMapKeyValueStore<TTest>.Iterator iterator = kvStore.GetBeginIterator();

            for (int i = 0; i < count; i++)
            {
                Assert.False(iterator.IsEnd());
                iterator.Next();
            }

            // Iterated all entries. Should return true now
            Assert.True(iterator.IsEnd());
        }

        [Fact]
        public void IteratorGet_PopulatedStore_ReturnsEntriesInOrder()
        {
            const int count = 10;

            (FlatMapKeyValueStore<TTest> kvStore, FileSystemClient _) = Create<TTest>(count);
            Assert.Success(PopulateKvStore(kvStore, out byte[][] values, count));

            FlatMapKeyValueStore<TTest>.Iterator iterator = kvStore.GetBeginIterator();

            // Check if each key-value pair matches
            for (int i = 0; i < count; i++)
            {
                TTest expectedKey = i;
                byte[] expectedValue = values[i];

                ref FlatMapKeyValueStore<TTest>.KeyValue kv = ref iterator.Get();

                Assert.Equal(expectedKey, kv.Key);
                Assert.Equal(expectedValue, kv.Value.Get().ToArray());

                iterator.Next();
            }
        }
    }
}
