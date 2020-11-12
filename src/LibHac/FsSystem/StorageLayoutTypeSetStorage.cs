using System;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    internal class StorageLayoutTypeSetStorage : IStorage
    {
        private ReferenceCountedDisposable<IStorage> BaseStorage { get; }
        private StorageType StorageFlag { get; }

        protected StorageLayoutTypeSetStorage(ref ReferenceCountedDisposable<IStorage> baseStorage,
            StorageType storageFlag)
        {
            BaseStorage = Shared.Move(ref baseStorage);
            StorageFlag = storageFlag;
        }

        public static ReferenceCountedDisposable<IStorage> CreateShared(
            ref ReferenceCountedDisposable<IStorage> baseStorage, StorageType storageFlag)
        {
            return new ReferenceCountedDisposable<IStorage>(
                new StorageLayoutTypeSetStorage(ref baseStorage, storageFlag));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
                BaseStorage?.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override Result DoRead(long offset, Span<byte> destination)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseStorage.Target.Read(offset, destination);
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseStorage.Target.Write(offset, source);
        }

        protected override Result DoFlush()
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseStorage.Target.Flush();
        }

        protected override Result DoSetSize(long size)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseStorage.Target.SetSize(size);
        }

        protected override Result DoGetSize(out long size)
        {
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(StorageFlag);
            return BaseStorage.Target.GetSize(out size);
        }
    }
}
