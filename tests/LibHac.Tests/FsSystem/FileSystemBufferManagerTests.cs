using LibHac.Fs;
using LibHac.FsSystem;
using Xunit;

namespace LibHac.Tests.FsSystem
{
    public class FileSystemBufferManagerTests
    {
        private FileSystemBufferManager CreateManager(int size, int blockSize = 0x4000, int maxCacheCount = 16)
        {
            int orderMax = FileSystemBuddyHeap.QueryOrderMax((nuint)size, (nuint)blockSize);
            nuint workBufferSize = FileSystemBuddyHeap.QueryWorkBufferSize(orderMax);
            byte[] workBuffer = new byte[workBufferSize];
            byte[] heapBuffer = new byte[size];

            var bufferManager = new FileSystemBufferManager();
            Assert.Success(bufferManager.Initialize(maxCacheCount, heapBuffer, blockSize, workBuffer));
            return bufferManager;
        }

        [Fact]
        public void AllocateBuffer_NoFreeSpace_ReturnsNull()
        {
            FileSystemBufferManager manager = CreateManager(0x20000);
            Buffer buffer1 = manager.AllocateBuffer(0x10000);
            Buffer buffer2 = manager.AllocateBuffer(0x10000);
            Buffer buffer3 = manager.AllocateBuffer(0x4000);

            Assert.True(!buffer1.IsNull);
            Assert.True(!buffer2.IsNull);
            Assert.True(buffer3.IsNull);
        }

        [Fact]
        public void AcquireCache_EntryNotEvicted_ReturnsEntry()
        {
            FileSystemBufferManager manager = CreateManager(0x20000);
            Buffer buffer1 = manager.AllocateBuffer(0x10000);

            long handle = manager.RegisterCache(buffer1, new IBufferManager.BufferAttribute());

            manager.AllocateBuffer(0x10000);
            Buffer buffer3 = manager.AcquireCache(handle);

            Assert.Equal(buffer1, buffer3);
        }

        [Fact]
        public void AcquireCache_EntryEvicted_ReturnsNull()
        {
            FileSystemBufferManager manager = CreateManager(0x20000);
            Buffer buffer1 = manager.AllocateBuffer(0x10000);

            long handle = manager.RegisterCache(buffer1, new IBufferManager.BufferAttribute());

            manager.AllocateBuffer(0x20000);
            Buffer buffer3 = manager.AcquireCache(handle);

            Assert.True(buffer3.IsNull);
        }

        [Fact]
        public void AcquireCache_MultipleEntriesEvicted_OldestAreEvicted()
        {
            FileSystemBufferManager manager = CreateManager(0x20000);
            Buffer buffer1 = manager.AllocateBuffer(0x8000);
            Buffer buffer2 = manager.AllocateBuffer(0x8000);
            Buffer buffer3 = manager.AllocateBuffer(0x8000);
            Buffer buffer4 = manager.AllocateBuffer(0x8000);

            long handle1 = manager.RegisterCache(buffer1, new IBufferManager.BufferAttribute());
            long handle2 = manager.RegisterCache(buffer2, new IBufferManager.BufferAttribute());
            long handle3 = manager.RegisterCache(buffer3, new IBufferManager.BufferAttribute());
            long handle4 = manager.RegisterCache(buffer4, new IBufferManager.BufferAttribute());

            manager.AllocateBuffer(0x10000);

            Buffer buffer1B = manager.AcquireCache(handle1);
            Buffer buffer2B = manager.AcquireCache(handle2);
            Buffer buffer3B = manager.AcquireCache(handle3);
            Buffer buffer4B = manager.AcquireCache(handle4);

            Assert.True(buffer1B.IsNull);
            Assert.True(buffer2B.IsNull);
            Assert.Equal(buffer3, buffer3B);
            Assert.Equal(buffer4, buffer4B);
        }
    }
}
